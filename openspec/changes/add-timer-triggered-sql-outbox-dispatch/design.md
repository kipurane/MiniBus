## Context

`SqlMiniBusOutboxDispatcher` already owns the durable SQL outbox drain behavior: claim pending rows, dispatch through the configured transport abstraction, and record dispatch success or failure metadata. `SqlMiniBusOutboxHostedDispatcher` wraps that dispatcher as an optional `IHostedService`, but Azure Functions deployments have different lifecycle expectations than a continuously running worker service.

A timer-triggered Function gives Azure Functions users a scheduling model that is explicit, visible in the host's trigger metadata, and naturally compatible with scale-out and recycle behavior. It also keeps the important outbox boundary clear: inbound Service Bus trigger processing commits durable SQL work first; a separate scheduled activity drains that work later.

## Decision: show a separate dispatcher Function App first

The reference path should prefer a separate dispatcher Function App rather than adding the timer to the existing Billing processing Function App.

That shape makes the operational ownership easier to see:

```text
Billing Function App
  ServiceBusTrigger -> MiniBusProcessor
    -> SQL inbox/outbox commit

Billing Outbox Dispatcher Function App
  TimerTrigger -> SqlMiniBusOutboxDispatcher
    -> Azure Service Bus transport dispatch
```

The separate host is clearer because it can be scaled, restarted, deployed, and observed independently from message processing. It also avoids implying that the dispatcher must share the same Functions process as the Service Bus triggers.

The documentation should still call out the colocated option. For a small deployment, adding a timer-triggered drain function to the existing Function App is reasonable when the team deliberately wants one deployable unit and accepts shared scaling and failure domains.

## Decision: keep dispatch behavior centered on `SqlMiniBusOutboxDispatcher`

The timer-triggered function should resolve `SqlMiniBusOutboxDispatcher` from dependency injection and run a bounded drain, such as the existing multi-batch dispatch API. The timer function should not duplicate SQL claim logic, transport dispatch mapping, or retry metadata handling.

This keeps manual CLI drain, hosted-service drain, and timer-triggered drain as scheduling choices over the same durable dispatch primitive.

## Decision: distinguish same-process wake-up from cross-host discovery

The existing hosted-service path can be low-latency when it runs in the same process as message processing:

```text
Handler Function App
  ServiceBusTrigger
    -> MiniBusProcessor
      -> SQL inbox/outbox commit
      -> best-effort in-process wake-up
      -> SqlMiniBusOutboxHostedDispatcher
      -> SqlMiniBusOutboxDispatcher
```

That wake-up happens only after the MiniBus-owned SQL commit succeeds. It is a local hint to shorten latency, not part of the transaction and not required for correctness.

A separate dispatcher host uses the same `SqlMiniBusOutboxDispatcher`, but it does not receive the built-in in-memory wake-up from the handler process:

```text
Handler Function App
  ServiceBusTrigger -> MiniBusProcessor -> SQL inbox/outbox commit

Separate dispatcher host
  TimerTrigger or hosted worker -> SqlMiniBusOutboxDispatcher
```

The separate host discovers work through timer or polling cadence. This remains correct because SQL claim and claim-lease recovery coordinate dispatch across processes. Multiple dispatcher instances can coexist for scale or recovery, but the reference path should avoid encouraging overlapping scheduler types unless the application intentionally wants that operational model.

## Decision: timer cadence is a sample/app concern

The feature should avoid baking a universal timer interval into MiniBus runtime APIs. The sample can choose a conservative local cadence and document the tradeoff:

- shorter intervals reduce outbox latency but increase idle polling cost
- longer intervals reduce idle work but increase time-to-dispatch
- multiple dispatcher instances are acceptable because SQL claims coordinate work, but delivery remains at-least-once

If implementation finds repeated boilerplate in the timer function, a small helper can be considered, but the first slice should bias toward explicit sample code.

## Alternatives Considered

- Add the timer trigger to the existing Billing Function App only: simpler, but hides the host boundary and makes the reference path less useful for production reasoning.
- Recommend `AddMiniBusSqlHostedOutboxDispatch(...)` for Azure Functions as the primary path: technically plausible in isolated worker, but less Functions-native and less explicit than a timer trigger.
- Build a new runtime package abstraction for timer dispatch: premature unless the sample reveals a real duplication or correctness problem.

## Risks / Trade-offs

- A separate dispatcher Function App adds another project and local run step. Keep the sample documentation tight and make colocation guidance explicit.
- Timer cadence can be misconfigured for either high latency or excessive polling. Document bounded drains and cadence tradeoffs.
- Dispatcher instances can still produce at-least-once duplicates after crash windows. Reuse the existing SQL claim lease, deterministic outgoing message id, broker duplicate detection guidance, and idempotent receiver guidance.
- Local emulator verification may not perfectly model cloud timer behavior. Keep normal tests infrastructure-light and document what the sample proves.

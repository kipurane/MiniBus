## Context

MiniBus has reached a point where the main runtime surfaces are stable enough to invest in developer experience. Handlers and saga handlers already receive `MiniBusContext`, which keeps them independent from Azure Functions, Azure Service Bus, SQL persistence, and other infrastructure. In practice, users still need a local test double for `MiniBusContext` before they can assert handler outgoing behavior.

The repository already contains several private `RecordingMiniBusContext` implementations in unit and integration tests. They capture sends, publishes, schedules, or metadata in slightly different ways. This change should turn that repeated pattern into a small supported package rather than introduce a broad processor harness.

## Goals / Non-Goals

**Goals:**

- Add a new `MiniBus.Testing` project and focused test project.
- Provide `TestableMiniBusContext` as the first testing helper.
- Let tests configure inbound context metadata and headers.
- Capture outgoing send, publish, and schedule requests with message objects and message types.
- Provide small typed query helpers if they remain dependency-free and simple.
- Document direct handler testing.

**Non-Goals:**

- Processor or Azure Functions integration harnesses.
- SQL persistence fixtures or live Azure Service Bus helpers.
- Test-framework-specific assertions or dependencies.
- Mocking-framework integrations.
- Source generators, analyzers, templates, or NuGet publishing metadata.
- Changes to `MiniBus.Core` runtime behavior.

## Decisions

### Create a dedicated Core-only testing package

`MiniBus.Testing` should reference `MiniBus.Core` and no host, transport, storage, observability, or test-framework packages. This keeps it usable in any application test suite without dragging infrastructure dependencies into handler tests.

Alternative considered: keep test helpers inside `MiniBus.Core`. That would reduce project count, but it would mix production runtime APIs with test-only conveniences and make later testing helpers harder to package separately.

### Start with `TestableMiniBusContext`

The first public surface should be a concrete `MiniBusContext` implementation for direct handler and saga handler tests. It should expose configurable metadata such as endpoint name, message id, correlation id, causation id, and headers. Defaults should be deterministic and useful so simple tests can instantiate it without setup.

Alternative considered: start with a full `HandlerTestHarness` that resolves handlers from dependency injection and invokes them. That may be useful later, but it risks freezing a larger testing model before the smallest common need has been validated.

### Use captured operation models instead of tuple-first APIs

Captured outgoing work should be represented by small public record/class types such as sent, published, and scheduled operation models. These should preserve the original message object, concrete message type, and due time for schedules. Public collection properties can expose read-only views of captured operations.

Alternative considered: expose mutable tuple lists as the primary API, mirroring the current private test doubles. Tuples are convenient internally, but named models make a public testing package clearer and easier to evolve.

### Keep query helpers dependency-free

Typed helpers such as `Sent<TCommand>()`, `Published<TEvent>()`, `Scheduled<TMessage>()`, and single-result variants can be included if they are simple LINQ-style methods and do not depend on a test assertion library. If a helper would need assertion semantics or framework-specific failure messages, leave it out of the first slice.

Alternative considered: provide rich assertion methods. That would make tests terse, but it would force opinions about test frameworks and assertion styles.

## Risks / Trade-offs

- [Risk] Public testing APIs can calcify quickly. -> Mitigation: keep the first surface small and centered on `MiniBusContext`.
- [Risk] Typed single-result helpers may duplicate assertion libraries. -> Mitigation: make them optional, framework-agnostic query helpers that throw ordinary exceptions only when necessary.
- [Risk] Users may expect full integration testing from the package name. -> Mitigation: document the first package scope clearly as direct handler and saga handler testing.
- [Risk] Operation capture might omit useful metadata. -> Mitigation: preserve message object, message type, and schedule due time now; avoid adding transport-specific metadata.

## 1. Acceptance Test Shape

- [x] 1.1 Review the existing SQL-backed reference acceptance test, fixture helpers, and recording transport assertions.
- [x] 1.2 Add a Tier 2 SQL-backed acceptance test that processes the billing command and event with SQL persistence enabled while asserting no outgoing transport calls occur before drain.
- [x] 1.3 Resolve `SqlMiniBusOutboxDispatcher` from the configured service provider and run `DispatchPendingAsync`.

## 2. Dispatch And SQL Assertions

- [x] 2.1 Assert the dispatcher reports the expected dispatched operation count for the captured send, publish, and schedule operations.
- [x] 2.2 Assert the recording transport receives the billing receipt command, invoice-created event, and invoice-payment-timeout schedule with the expected destinations.
- [x] 2.3 Add minimal SQL fixture helper queries needed to verify dispatched rows are marked dispatched and no longer pending or reclaimable.
- [x] 2.4 Assert the SQL outbox state after drain matches the acceptance-level contract without duplicating low-level persistence tests.

## 3. Validation

- [x] 3.1 Run the targeted acceptance test project.
- [x] 3.2 Run the full test suite with `dotnet test --no-restore`.
- [x] 3.3 Confirm the OpenSpec change status is apply-complete and ready for archive after implementation.

## 1. SQL-Backed Billing Shape

- [x] 1.1 Review the current Billing sample registration, local run helpers, SQL acceptance fixtures, and existing SQL schema application guidance to choose the explicit SQL-backed sample entry path.
- [x] 1.2 Add opt-in Billing sample configuration that registers SQL inbox/outbox and SQL saga persistence while keeping the existing lightweight emulator workflow and handler-facing Billing APIs intact.
- [x] 1.3 Add the sample-owned configuration or local infrastructure assets needed to supply the SQL-backed Billing path with an explicit SQL Server connection and schema setup path.

## 2. Reliable Billing Workflow

- [x] 2.1 Add a repository-owned way to apply the packaged MiniBus SQL schema scripts for the SQL-backed Billing reference workflow.
- [x] 2.2 Add an explicit Billing outbox drain path that uses the existing `SqlMiniBusOutboxDispatcher` after SQL-backed processing captures outgoing send, publish, and scheduled timeout work.
- [x] 2.3 Keep the SQL-backed workflow aligned with the existing Billing routes, Azure Functions wrappers, event flow, and timeout scheduling behavior rather than duplicating the Billing sample in a second host.

## 3. Reference Verification

- [x] 3.1 Extend SQL-backed reference acceptance coverage to prove the Billing workflow persists inbox, outbox, and SQL saga state through sample-style SQL configuration.
- [x] 3.2 Add acceptance coverage for duplicate Billing delivery with SQL inbox state so successful handler, saga, and outbox effects are not captured again.
- [x] 3.3 Verify outbox draining still dispatches the captured Billing receipt command, invoice-created event, and invoice-payment-timeout schedule through the configured transport path while leaving low-level SQL mechanics to SQL persistence tests.
- [x] 3.4 Add or update sample verification for the SQL-backed local workflow when the required local SQL and Service Bus emulator dependencies are available, without making live Azure Service Bus a test requirement.

## 4. Guidance And Validation

- [x] 4.1 Update the Billing sample documentation to distinguish the simple emulator workflow from the SQL-backed reliable workflow, including schema setup, SQL registration, outbox draining, expected observations, and local limitations.
- [x] 4.2 Update root or adjacent guidance so the next-step production-readiness story points from the emulator Billing sample to the SQL-backed reference path without implying automatic provisioning or background outbox dispatch.
- [x] 4.3 Run focused build and acceptance verification for the changed Billing and SQL-backed reference paths.
- [x] 4.4 Run the broader repository verification that is practical for the change and confirm OpenSpec status is apply-ready afterward.

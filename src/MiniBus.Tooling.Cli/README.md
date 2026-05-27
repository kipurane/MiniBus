# MiniBus.Tooling.Cli

Command-line tooling for local MiniBus troubleshooting.

Read-only commands list SQL inbox, outbox, and saga records or show a best-effort SQL timeline for a message id or correlation id. The CLI supports compact table output by default and JSON output for scripts.

Bounded outbox drain is modeled as an explicit action and requires a host-provided `SqlMiniBusOutboxDispatcher` with transport dispatch dependencies. The standalone CLI command reports that drain is unsupported unless those dependencies are supplied.

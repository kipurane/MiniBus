namespace MiniBus.Tooling.Cli;

internal static class MiniBusToolingCliHelp
{
    public const string Text = """
        MiniBus local tooling

        Usage:
          minibus --connection-string <value> [--schema MiniBus] [--format table|json] <command>

        Commands:
          inbox list                    List SQL inbox records
          outbox list                   List SQL outbox records
          sagas list                    List SQL saga records
          show message --message-id ID  Show best-effort SQL timeline for a message
          show correlation --correlation-id ID
                                        Show best-effort SQL timeline for a correlation
          outbox drain --max-batches N  Run an explicit bounded outbox drain when a host supplies dispatch dependencies

        Common filters:
          --endpoint NAME
          --message-id ID
          --correlation-id ID
          --status STATUS
          --from UTC
          --to UTC
          --limit N
        """;
}

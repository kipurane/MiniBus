namespace MiniBus.Tooling.Cli;

public sealed record MiniBusToolingCliParsedCommand(
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string?> Options,
    bool HelpRequested);

public static class MiniBusToolingCliParser
{
    public static MiniBusToolingCliParsedCommand Parse(IReadOnlyList<string> args)
    {
        var arguments = new List<string>();
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var help = false;

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            if (token is "--help" or "-h")
            {
                help = true;
                continue;
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                arguments.Add(token);
                continue;
            }

            var name = token[2..];
            string? value = null;
            if (index + 1 < args.Count
                && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++index];
            }

            options[name] = value;
        }

        return new MiniBusToolingCliParsedCommand(arguments, options, help);
    }
}

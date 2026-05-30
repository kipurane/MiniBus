namespace MiniBus.Tooling.Web;

public sealed class MiniBusToolingWebOptions
{
    public string? ConnectionString { get; set; }

    public string SchemaName { get; set; } = "MiniBus";
}

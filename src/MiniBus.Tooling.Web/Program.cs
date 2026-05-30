using MiniBus.Tooling.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMiniBusToolingWeb(builder.Configuration);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapMiniBusToolingWebApi();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program
{
}

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using MiniBus.FunctionApp.Template;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddStarterMiniBus(builder.Configuration);

builder.Build().Run();

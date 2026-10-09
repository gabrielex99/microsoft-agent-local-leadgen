using Serilog;
using LocalLeadGen.Infrastructure;
using LocalLeadGen.Worker;

// Inizializzazione Logger Serilog (Console strutturata + Rolling File log)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/leadgen-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Inizializzazione Host .NET 9 per LocalLeadGen Worker...");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

    // Integrazione Serilog
    builder.Services.AddSerilog();

    // Registrazione Clean Architecture Infrastructure Services (EF Core, Playwright, OpenRouter AI, Gmail)
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // Registrazione Background Service
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Arresto anomalo dell'applicazione durante l'avvio.");
}
finally
{
    await Log.CloseAndFlushAsync();
}

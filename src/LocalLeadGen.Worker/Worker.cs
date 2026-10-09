using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LocalLeadGen.Application.UseCases;
using LocalLeadGen.Domain.Repositories;
using LocalLeadGen.Infrastructure.Configuration;
using LocalLeadGen.Infrastructure.Persistence;

namespace LocalLeadGen.Worker;

/// <summary>
/// Background Service che ospita il ciclo di esecuzione della pipeline autonoma di lead generation.
/// </summary>
public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LeadGenSettings _settings;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IServiceScopeFactory scopeFactory,
        IOptions<LeadGenSettings> settings,
        IHostApplicationLifetime hostLifetime,
        ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _hostLifetime = hostLifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("==================================================================");
        _logger.LogInformation("LOCAL LEAD GENERATION & WEB AUDIT PIPELINE (.NET 9 C# 13)");
        _logger.LogInformation("Territorio Target: {Cities}", string.Join(", ", _settings.TargetCities));
        _logger.LogInformation("Categorie: {Categories}", string.Join(", ", _settings.TargetCategories));
        _logger.LogInformation("Quota Giornaliera: {Quota} lead", _settings.MaxDailyLeads);
        _logger.LogInformation("==================================================================");

        try
        {
            // 1. Inizializzazione Database SQLite e cartella dati
            Directory.CreateDirectory("data");
            Directory.CreateDirectory("credentials");

            using (var initScope = _scopeFactory.CreateScope())
            {
                var dbContext = initScope.ServiceProvider.GetRequiredService<AppDbContext>();
                _logger.LogInformation("Verifica e inizializzazione schema database SQLite...");
                await dbContext.Database.EnsureCreatedAsync(stoppingToken);
                _logger.LogInformation("Database pronto su data/leads.db.");
            }

            // 2. Esecuzione Use Case
            using (var executionScope = _scopeFactory.CreateScope())
            {
                var workflow = executionScope.ServiceProvider.GetRequiredService<ProcessDailyLeadsWorkflow>();
                var leadRepo = executionScope.ServiceProvider.GetRequiredService<ILeadRepository>();

                var completedLeads = await workflow.ExecuteAsync(
                    _settings.TargetCities,
                    _settings.TargetCategories,
                    _settings.MaxDailyLeads,
                    stoppingToken);

                _logger.LogInformation("==================================================================");
                _logger.LogInformation("PIPELINE COMPLETATA CON SUCCESSO!");
                _logger.LogInformation("Totale Lead Qualificati ed elaborati: {Count}", completedLeads);

                var recent = await leadRepo.GetRecentLeadsAsync(10, stoppingToken);
                _logger.LogInformation("Riepilogo ultimi lead:");
                foreach (var l in recent)
                {
                    _logger.LogInformation(" - [{Status}] {Business} ({City}) -> Email: {Email} | Oggetto: {Subject}",
                        l.Status, l.BusinessName, l.City, l.ContactEmail?.Value ?? "N/D", l.DraftSubject ?? "-");
                }

                _logger.LogInformation("==================================================================");
                _logger.LogInformation("HUMAN-IN-THE-LOOP: Le bozze sono disponibili su Gmail.");
                _logger.LogInformation("Accedi alla cartella 'Bozze' (Drafts) per revisionare e decidere se inviare.");
                _logger.LogInformation("==================================================================");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Worker interrotto da richiesta di cancellazione.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Errore fatale non gestito durante l'esecuzione del Worker.");
        }
        finally
        {
            // Arresto controllato del processo una volta terminata la sessione batch
            _hostLifetime.StopApplication();
        }
    }
}

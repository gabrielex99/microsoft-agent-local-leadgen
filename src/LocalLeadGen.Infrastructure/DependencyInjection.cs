using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LocalLeadGen.Application.Interfaces;
using LocalLeadGen.Application.UseCases;
using LocalLeadGen.Domain.Repositories;
using LocalLeadGen.Infrastructure.AI;
using LocalLeadGen.Infrastructure.Configuration;
using LocalLeadGen.Infrastructure.Email;
using LocalLeadGen.Infrastructure.Persistence;
using LocalLeadGen.Infrastructure.Persistence.Repositories;
using LocalLeadGen.Infrastructure.Scraping;

namespace LocalLeadGen.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra i servizi di Infrastructure, Persistence, Scraping, AI Agent ed Email nel container IoC.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Configurazione strongly-typed con validazione automatica all'avvio
        services.AddOptions<OpenRouterOptions>()
            .Bind(configuration.GetSection(OpenRouterOptions.SectionName));

        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName));

        services.AddOptions<GmailOptions>()
            .Bind(configuration.GetSection(GmailOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LeadGenSettings>()
            .Bind(configuration.GetSection(LeadGenSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // 2. Persistenza SQLite con EF Core 9
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=data/leads.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        services.AddScoped<ILeadRepository, LeadRepository>();

        // 3. Browser Automation / Scraping
        services.AddScoped<IScraperService, PlaywrightScraperService>();

        // 4. Microsoft Agent Framework AI Agent (OpenRouter / Microsoft.Agents.AI)
        services.AddScoped<IAuditCopywriterAgent, MicrosoftAgentService>();

        // 5. Google APIs Gmail Drafts (Human-in-the-Loop)
        services.AddScoped<IEmailDraftService, GmailDraftService>();

        // 6. Application Use Cases
        services.AddScoped<ProcessDailyLeadsWorkflow>();

        return services;
    }
}

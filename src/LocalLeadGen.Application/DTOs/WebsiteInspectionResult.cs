namespace LocalLeadGen.Application.DTOs;

/// <summary>
/// Record DTO che trasporta l'esito dell'ispezione tecnica del sito web tramite Playwright.
/// </summary>
public sealed record WebsiteInspectionResult(
    bool IsReachable,
    bool UsesHttps,
    bool HasViewportMetaTag,
    bool HasHorizontalScrollFailure,
    int? CopyrightYear,
    IReadOnlyList<string> DiscoveredEmails,
    string? Notes);

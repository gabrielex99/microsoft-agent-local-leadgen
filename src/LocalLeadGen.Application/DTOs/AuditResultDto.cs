namespace LocalLeadGen.Application.DTOs;

/// <summary>
/// Record DTO che trasporta l'output strutturato generato dall'AI Copywriter Agent.
/// </summary>
public sealed record AuditResultDto(
    IReadOnlyList<string> IdentifiedIssues,
    string PitchAngle,
    string EmailSubject,
    string EmailHtmlBody);

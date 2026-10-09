using Microsoft.EntityFrameworkCore;
using LocalLeadGen.Domain.Entities;
using LocalLeadGen.Domain.Repositories;

namespace LocalLeadGen.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementazione concreta di ILeadRepository tramite EF Core 9 e SQLite.
/// </summary>
public class LeadRepository : ILeadRepository
{
    private readonly AppDbContext _context;

    public LeadRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(
        string businessName,
        string city,
        TimeSpan duplicateWindow,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - duplicateWindow;
        var normalizedName = businessName.Trim().ToLower();
        var normalizedCity = city.Trim().ToLower();

        return await _context.Leads
            .AsNoTracking()
            .AnyAsync(l =>
                l.BusinessName.ToLower() == normalizedName &&
                l.City.ToLower() == normalizedCity &&
                l.CreatedAtUtc >= cutoff,
                ct);
    }

    public async Task<Lead?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Leads.FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<Lead>> GetRecentLeadsAsync(int count, CancellationToken ct = default)
    {
        return await _context.Leads
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Lead lead, CancellationToken ct = default)
    {
        await _context.Leads.AddAsync(lead, ct);
    }

    public Task UpdateAsync(Lead lead, CancellationToken ct = default)
    {
        _context.Leads.Update(lead);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}

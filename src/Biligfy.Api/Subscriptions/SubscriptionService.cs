using Biligfy.Api.Data;
using Biligfy.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Biligfy.Api.Subscriptions;

public interface ISubscriptionService
{
    Task<PagedResponse<SubscriptionResponse>> ListAsync(Guid userId, int page, int pageSize, bool? isActive, CancellationToken ct);
    Task<SubscriptionResponse?> GetAsync(Guid userId, Guid id, CancellationToken ct);
    Task<SubscriptionResponse> CreateAsync(Guid userId, SubscriptionRequest request, CancellationToken ct);
    Task<SubscriptionResponse?> UpdateAsync(Guid userId, Guid id, SubscriptionRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}

public sealed class SubscriptionLimitExceededException : Exception
{
    public SubscriptionLimitExceededException() : base("En fazla 100 abonelik ekleyebilirsiniz.") { }
}

public sealed class SubscriptionService(AppDbContext db) : ISubscriptionService
{
    public const int MaxSubscriptionsPerUser = 100;

    public async Task<PagedResponse<SubscriptionResponse>> ListAsync(
        Guid userId, int page, int pageSize, bool? isActive, CancellationToken ct)
    {
        var query = db.Subscriptions.AsNoTracking().Where(s => s.UserId == userId);
        if (isActive is not null) query = query.Where(s => s.IsActive == isActive);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(s => s.NextRenewalDate).ThenBy(s => s.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<SubscriptionResponse>(
            rows.Select(SubscriptionMapper.ToResponse).ToList(), page, pageSize, total);
    }

    public async Task<SubscriptionResponse?> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var s = await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        return s is null ? null : SubscriptionMapper.ToResponse(s);
    }

    public async Task<SubscriptionResponse> CreateAsync(Guid userId, SubscriptionRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var s = new Subscription { Id = Guid.NewGuid(), UserId = userId, CreatedAtUtc = now, UpdatedAtUtc = now };
        SubscriptionMapper.Apply(s, request);
        if (await db.Subscriptions.CountAsync(x => x.UserId == userId, ct) >= MaxSubscriptionsPerUser)
            throw new SubscriptionLimitExceededException();
        db.Subscriptions.Add(s);
        await db.SaveChangesAsync(ct);
        return SubscriptionMapper.ToResponse(s);
    }

    public async Task<SubscriptionResponse?> UpdateAsync(Guid userId, Guid id, SubscriptionRequest request, CancellationToken ct)
    {
        var s = await db.Subscriptions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (s is null) return null;
        SubscriptionMapper.Apply(s, request);
        s.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return SubscriptionMapper.ToResponse(s);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var s = await db.Subscriptions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (s is null) return false;
        db.Subscriptions.Remove(s);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

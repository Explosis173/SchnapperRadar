using Microsoft.EntityFrameworkCore;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Domain.Entities;

namespace SchnapperRadar.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<AiUsageEntry> AiUsage => Set<AiUsageEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    Task IUnitOfWork.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);
}

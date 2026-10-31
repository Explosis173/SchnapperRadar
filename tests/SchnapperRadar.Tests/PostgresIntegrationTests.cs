using Microsoft.EntityFrameworkCore;
using SchnapperRadar.Application.Options;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Infrastructure.Persistence;
using SchnapperRadar.Infrastructure.Persistence.Repositories;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SchnapperRadar.Tests;

/// <summary>Läuft nur mit TEST_POSTGRES=&lt;connection string&gt; (leere Test-DB, wird neu angelegt).</summary>
public class PostgresIntegrationTests
{
    private static readonly string? ConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES");

    [Fact]
    public async Task Repositories_work_against_real_postgres()
    {
        if (ConnectionString is null) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options;
        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.MigrateAsync();
        }

        var interest = new Interest { Query = "ps5" };
        var paused = new Interest { Query = "alt", Enabled = false };
        Listing Make(string id, double score, PipelineStage stage, Interest? i, bool excluded = false, int ageDays = 0)
        {
            var l = TestData.Listing("PS5 " + id, 300, Platform.Vinted, id: id);
            l.InterestId = i?.Id;
            l.Assessment.FinalScore = score;
            l.Assessment.Stage = stage;
            l.Assessment.IsExcluded = excluded;
            l.Assessment.RedFlags = ["x"];
            l.LastSeenAt = DateTimeOffset.UtcNow.AddDays(-ageDays);
            return l;
        }

        await using (var db = new AppDbContext(options))
        {
            db.Interests.AddRange(interest, paused);
            db.Listings.AddRange(
                Make("top", 90, PipelineStage.AiAssessed, interest),
                Make("ok", 60, PipelineStage.Enriched, interest),
                Make("low", 40, PipelineStage.Enriched, interest),
                Make("unchecked", 95, PipelineStage.Collected, interest),
                Make("fake", 99, PipelineStage.AiAssessed, interest, excluded: true),
                Make("paused", 90, PipelineStage.AiAssessed, paused),
                Make("search-only", 90, PipelineStage.AiAssessed, null),
                Make("stale", 90, PipelineStage.AiAssessed, interest, ageDays: 30));
            db.AiUsage.Add(new AiUsageEntry { Model = "m", InputTokens = 100, OutputTokens = 10 });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var repo = new ListingRepository(db, MsOptions.Create(new FeedOptions()));

            var feed = await repo.GetFeedAsync(0, 10, CancellationToken.None);
            Assert.Equal(["top", "ok"], feed.Select(l => l.ExternalId));
            Assert.Equal(["x"], feed[0].Assessment.RedFlags);
            Assert.NotNull(feed[0].Seller);

            var existing = await repo.FindExistingAsync(Platform.Vinted, ["top", "nope", "fake"], CancellationToken.None);
            Assert.Equal(2, existing.Count);

            existing[0].ToggleSaved();
            await db.SaveChangesAsync();
            Assert.Single(await repo.GetSavedAsync(CancellationToken.None));

            Assert.Equal(1, await repo.DeleteStaleAsync(DateTimeOffset.UtcNow.AddDays(-14), CancellationToken.None));

            var usage = await new AiUsageRepository(db).GetSummaryAsync(DateTimeOffset.UtcNow.AddDays(-1), CancellationToken.None);
            Assert.Equal(new SchnapperRadar.Application.Abstractions.AiUsageSummary(1, 100, 10), usage);
        }
    }
}

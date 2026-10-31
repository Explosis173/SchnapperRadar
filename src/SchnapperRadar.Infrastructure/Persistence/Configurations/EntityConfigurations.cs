using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchnapperRadar.Domain.Entities;

namespace SchnapperRadar.Infrastructure.Persistence.Configurations;

internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> b)
    {
        b.ToTable("listings");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.Platform, x.ExternalId }).IsUnique();
        b.HasIndex(x => new { x.InterestId, x.IsHidden });
        b.HasIndex(x => x.LastSeenAt);

        b.Property(x => x.ExternalId).HasMaxLength(64);
        b.Property(x => x.Url).HasMaxLength(1024);
        b.Property(x => x.Title).HasMaxLength(512);
        b.Property(x => x.Description).HasMaxLength(20_000);
        b.Property(x => x.PostalCode).HasMaxLength(10);
        b.Property(x => x.City).HasMaxLength(128);
        b.Property(x => x.Brand).HasMaxLength(128);
        b.Property(x => x.Size).HasMaxLength(64);
        b.Property(x => x.Price).HasPrecision(12, 2);
        b.Property(x => x.ShippingPrice).HasPrecision(10, 2);
        b.Property(x => x.PlatformBuyerFee).HasPrecision(10, 2);
        b.Property(x => x.IsHidden);
        b.Property(x => x.IsSaved);
        b.Ignore(x => x.IsDetailLoaded);

        b.OwnsOne(x => x.Seller, s =>
        {
            s.Property(p => p.ExternalId).HasMaxLength(64);
            s.Property(p => p.Name).HasMaxLength(128);
        });

        b.OwnsOne(x => x.Assessment, a =>
        {
            a.Property(p => p.TotalCost).HasPrecision(12, 2);
            a.Property(p => p.ShippingCost).HasPrecision(10, 2);
            a.Property(p => p.BuyerFee).HasPrecision(10, 2);
            a.Property(p => p.PickupTravelCost).HasPrecision(10, 2);
            a.Property(p => p.MarketReferencePrice).HasPrecision(12, 2);
            a.Property(p => p.AiEstimatedMarketPrice).HasPrecision(12, 2);
            a.Property(p => p.AiSummary).HasMaxLength(512);
            a.Property(p => p.AiModel).HasMaxLength(128);
            a.Property(p => p.AiContentHash).HasMaxLength(64);
            a.HasIndex(p => p.FinalScore);
        });

        // Owned Types immer materialisieren (sonst null bei lauter Default-Werten).
        b.Navigation(x => x.Seller).IsRequired();
        b.Navigation(x => x.Assessment).IsRequired();
    }
}

internal sealed class InterestConfiguration : IEntityTypeConfiguration<Interest>
{
    public void Configure(EntityTypeBuilder<Interest> b)
    {
        b.ToTable("interests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Query).HasMaxLength(100);
        b.Property(x => x.MinPrice).HasPrecision(12, 2);
        b.Property(x => x.MaxPrice).HasPrecision(12, 2);
    }
}

internal sealed class AiUsageConfiguration : IEntityTypeConfiguration<AiUsageEntry>
{
    public void Configure(EntityTypeBuilder<AiUsageEntry> b)
    {
        b.ToTable("ai_usage");
        b.HasKey(x => x.Id);
        b.Property(x => x.Model).HasMaxLength(128);
        b.HasIndex(x => x.CreatedAt);
    }
}

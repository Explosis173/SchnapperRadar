using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SchnapperRadar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_usage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "interests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Query = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    MaxPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Platforms = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "listings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    IsNegotiable = table.Column<bool>(type: "boolean", nullable: false),
                    ImageUrls = table.Column<List<string>>(type: "text[]", nullable: false),
                    ImageCount = table.Column<int>(type: "integer", nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    City = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    Brand = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Size = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ShippingAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    PickupAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    ShippingPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    PlatformBuyerFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    PackageSize = table.Column<int>(type: "integer", nullable: true),
                    Seller_ExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Seller_Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Seller_IsCommercial = table.Column<bool>(type: "boolean", nullable: false),
                    Seller_RatingPercent = table.Column<double>(type: "double precision", nullable: true),
                    Seller_RatingCount = table.Column<int>(type: "integer", nullable: true),
                    Seller_Badges = table.Column<List<string>>(type: "text[]", nullable: false),
                    Seller_MemberSince = table.Column<DateOnly>(type: "date", nullable: true),
                    Seller_ActiveListings = table.Column<int>(type: "integer", nullable: true),
                    Seller_SoldItems = table.Column<int>(type: "integer", nullable: true),
                    Seller_IsLoaded = table.Column<bool>(type: "boolean", nullable: false),
                    Assessment_Stage = table.Column<int>(type: "integer", nullable: false),
                    Assessment_TotalCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Assessment_ChosenDelivery = table.Column<int>(type: "integer", nullable: false),
                    Assessment_ShippingCost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Assessment_ShippingCostIsEstimate = table.Column<bool>(type: "boolean", nullable: false),
                    Assessment_BuyerFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Assessment_PickupTravelCost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Assessment_DistanceKm = table.Column<double>(type: "double precision", nullable: true),
                    Assessment_MarketReferencePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Assessment_DealScore = table.Column<double>(type: "double precision", nullable: false),
                    Assessment_TrustScore = table.Column<double>(type: "double precision", nullable: false),
                    Assessment_PreScore = table.Column<double>(type: "double precision", nullable: false),
                    Assessment_RedFlags = table.Column<List<string>>(type: "text[]", nullable: false),
                    Assessment_IsExcluded = table.Column<bool>(type: "boolean", nullable: false),
                    Assessment_AiLegitimacy = table.Column<double>(type: "double precision", nullable: true),
                    Assessment_AiDealQuality = table.Column<double>(type: "double precision", nullable: true),
                    Assessment_AiEstimatedMarketPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Assessment_AiVerdict = table.Column<int>(type: "integer", nullable: false),
                    Assessment_AiSummary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Assessment_AiModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Assessment_AiContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Assessment_FinalScore = table.Column<double>(type: "double precision", nullable: false),
                    Assessment_AssessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetailLoadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InterestId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    IsSaved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_CreatedAt",
                table: "ai_usage",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_listings_Assessment_FinalScore",
                table: "listings",
                column: "Assessment_FinalScore");

            migrationBuilder.CreateIndex(
                name: "IX_listings_InterestId_IsHidden",
                table: "listings",
                columns: new[] { "InterestId", "IsHidden" });

            migrationBuilder.CreateIndex(
                name: "IX_listings_LastSeenAt",
                table: "listings",
                column: "LastSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_listings_Platform_ExternalId",
                table: "listings",
                columns: new[] { "Platform", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage");

            migrationBuilder.DropTable(
                name: "interests");

            migrationBuilder.DropTable(
                name: "listings");
        }
    }
}

using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Application.Options;
using SchnapperRadar.Infrastructure.Ai;
using SchnapperRadar.Infrastructure.Background;
using SchnapperRadar.Infrastructure.Geo;
using SchnapperRadar.Infrastructure.Http;
using SchnapperRadar.Infrastructure.Persistence;
using SchnapperRadar.Infrastructure.Persistence.Repositories;
using SchnapperRadar.Infrastructure.Sources.Kleinanzeigen;
using SchnapperRadar.Infrastructure.Sources.Vinted;

namespace SchnapperRadar.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Postgres";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, bool withBackgroundJobs = true)
    {
        services.Configure<HomeOptions>(config.GetSection(HomeOptions.Section));
        services.Configure<CostOptions>(config.GetSection(CostOptions.Section));
        services.Configure<PipelineOptions>(config.GetSection(PipelineOptions.Section));
        services.Configure<FeedOptions>(config.GetSection(FeedOptions.Section));
        services.Configure<ScraperOptions>(config.GetSection(ScraperOptions.Section));
        services.Configure<AiOptions>(config.GetSection(AiOptions.Section));

        var connectionString = config.GetConnectionString(ConnectionStringName)
                               ?? throw new InvalidOperationException($"ConnectionStrings__{ConnectionStringName} fehlt.");
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IListingRepository, ListingRepository>();
        services.AddScoped<IInterestRepository, InterestRepository>();
        services.AddScoped<IAiUsageRepository, AiUsageRepository>();

        services.AddSingleton<IGeoService, OfflineGeoService>();
        services.AddSingleton<HostThrottle>();
        services.AddSingleton<VintedCookieJar>();

        AddScraperClient(services, KleinanzeigenSource.HttpClientName, cookies: null);
        services.AddHttpClient(VintedSource.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp => CreateHandler(sp.GetRequiredService<VintedCookieJar>().Container))
            .ConfigureHttpClient((sp, c) => ConfigureBrowserHeaders(c, sp.GetRequiredService<IOptions<ScraperOptions>>().Value))
            .AddStandardResilienceHandler(ConfigureResilience);

        services.AddScoped<IListingSource, KleinanzeigenSource>();
        services.AddScoped<IListingSource, VintedSource>();

        services.AddHttpClient(AiChatClientFactory.HttpClientName)
            .AddStandardResilienceHandler(o =>
            {
                // Free-Tiers antworten gern mit 429 → mit Backoff erneut versuchen.
                o.Retry.MaxRetryAttempts = 3;
                o.Retry.Delay = TimeSpan.FromSeconds(3);
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(120);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(400);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(240);
            });
        services.AddScoped<IAiChatClient>(sp => AiChatClientFactory.Create(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(AiChatClientFactory.HttpClientName),
            sp.GetRequiredService<IOptions<AiOptions>>().Value));

        if (withBackgroundJobs) services.AddHostedService<FeedRefreshWorker>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);
    }

    private static void AddScraperClient(IServiceCollection services, string name, CookieContainer? cookies) =>
        services.AddHttpClient(name)
            .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(cookies ?? new CookieContainer()))
            .ConfigureHttpClient((sp, c) => ConfigureBrowserHeaders(c, sp.GetRequiredService<IOptions<ScraperOptions>>().Value))
            .AddStandardResilienceHandler(ConfigureResilience);

    private static SocketsHttpHandler CreateHandler(CookieContainer cookies) => new()
    {
        CookieContainer = cookies,
        UseCookies = true,
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    };

    private static void ConfigureBrowserHeaders(HttpClient c, ScraperOptions o)
    {
        c.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
        c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de-DE,de;q=0.9,en;q=0.6");
        c.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8");
    }

    private static void ConfigureResilience(HttpStandardResilienceOptions o)
    {
        o.Retry.MaxRetryAttempts = 2;
        o.Retry.Delay = TimeSpan.FromSeconds(2);
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    }
}

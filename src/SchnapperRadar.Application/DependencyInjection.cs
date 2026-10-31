using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchnapperRadar.Application.Ai;
using SchnapperRadar.Application.Costs;
using SchnapperRadar.Application.Options;
using SchnapperRadar.Application.Pipeline;
using SchnapperRadar.Application.Services;

namespace SchnapperRadar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<FeedRefreshLock>();
        services.AddSingleton<FeedRefreshTrigger>();
        services.AddScoped(sp => new CostCalculator(
            sp.GetRequiredService<IOptions<HomeOptions>>().Value,
            sp.GetRequiredService<IOptions<CostOptions>>().Value));
        services.AddScoped<AiDealAssessor>();
        services.AddScoped<DealPipeline>();
        services.AddScoped<SearchService>();
        services.AddScoped<FeedService>();
        services.AddScoped<InterestService>();
        services.AddScoped<SystemService>();
        return services;
    }
}

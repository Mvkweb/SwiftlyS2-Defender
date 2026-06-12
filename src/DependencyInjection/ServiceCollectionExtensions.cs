using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2_Defender.Interfaces;
using SwiftlyS2_Defender.Services;

namespace SwiftlyS2_Defender.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDefenderServices(this IServiceCollection services)
    {
        services.AddSingleton<Random>();

        // Core services
        services.AddSingleton<IDefenderConfigService, DefenderConfigService>();
        services.AddSingleton<IDefenderStateService, DefenderStateService>();
        services.AddSingleton<IRecordingService, RecordingService>();
        services.AddSingleton<IScenarioPlaybackService, ScenarioPlaybackService>();
        services.AddSingleton<IRoundManagerService, RoundManagerService>();
        services.AddSingleton<IScenarioVisualizationService, ScenarioVisualizationService>();

        return services;
    }
}

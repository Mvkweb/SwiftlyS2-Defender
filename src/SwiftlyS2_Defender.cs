using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2_Defender.DependencyInjection;
using SwiftlyS2_Defender.Handlers;
using SwiftlyS2_Defender.Interfaces;
using System;
using Microsoft.Extensions.Logging;

namespace SwiftlyS2_Defender;

[PluginMetadata(Id = "Defender", Version = "1.0.0", Name = "Defender", Author = "Mvk", Description = "Refrag-style Defender mode plugin")]
public class SwiftlyS2_Defender : BasePlugin
{
    private IServiceProvider? _serviceProvider;

    private GameplayEventHandlers? _gameplayEventHandlers;
    private CommandHandlers? _commandHandlers;

    public SwiftlyS2_Defender(ISwiftlyCore core) : base(core)
    {
    }

    public override void Load(bool hotReload)
    {
        _serviceProvider = ServiceProviderFactory.CreateServiceProvider(Core, Core.Logger);

        var roundManager = _serviceProvider.GetRequiredService<IRoundManagerService>();
        var recording = _serviceProvider.GetRequiredService<IRecordingService>();
        var state = _serviceProvider.GetRequiredService<IDefenderStateService>();
        _gameplayEventHandlers = new GameplayEventHandlers(roundManager, recording, state);
        _gameplayEventHandlers.Register(Core);


        var config = _serviceProvider.GetRequiredService<IDefenderConfigService>();
        var playback = _serviceProvider.GetRequiredService<IScenarioPlaybackService>();
        var vis = _serviceProvider.GetRequiredService<IScenarioVisualizationService>();

        _commandHandlers = new CommandHandlers(state, config, recording, playback, vis);
        _commandHandlers.Register(Core);

        config.LoadOrCreate();

        Core.Engine.ExecuteCommand("mp_warmuptime 999999");
        Core.Engine.ExecuteCommand("mp_warmup_pausetimer 1");
        Core.Engine.ExecuteCommand("mp_warmup_start");
        Core.Engine.ExecuteCommand("bot_join_team T");
        Core.Engine.ExecuteCommand("bot_kick");

        Core.Engine.ExecuteCommand("violence_hblood 0");
        Core.Engine.ExecuteCommand("violence_ablood 0");
        Core.Engine.ExecuteCommand("violence_hgibs 0");
        Core.Engine.ExecuteCommand("violence_agibs 0");

        Core.Logger.LogInformation("Defender: plugin loaded successfully via DI.");
    }

    public override void Unload()
    {
        _gameplayEventHandlers?.Unregister(Core);
        _commandHandlers?.Unregister(Core);

        ServiceProviderFactory.DisposeServiceProvider(_serviceProvider);
        _serviceProvider = null;
    }
}
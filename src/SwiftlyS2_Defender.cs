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
        var hud = _serviceProvider.GetRequiredService<IMenuHudService>();

        hud.Register(Core);

        _commandHandlers = new CommandHandlers(state, config, recording, playback, vis, hud, roundManager);
        _commandHandlers.Register(Core);

        config.LoadOrCreate();

        /* TEMPORARY: Revert back to real warmup if fake warmup is broken
        Core.Engine.ExecuteCommand("mp_warmuptime 999999");
        Core.Engine.ExecuteCommand("mp_warmup_pausetimer 1");
        Core.Engine.ExecuteCommand("mp_warmup_start");
        */

        // FAKE WARMUP
        Core.Engine.ExecuteCommand("mp_warmup_end");
        Core.Engine.ExecuteCommand("mp_ignore_round_win_conditions 1"); // Rounds never end
        Core.Engine.ExecuteCommand("mp_roundtime 60");
        Core.Engine.ExecuteCommand("mp_freezetime 0");
        Core.Engine.ExecuteCommand("mp_buytime 9999");
        Core.Engine.ExecuteCommand("mp_buy_anywhere 1");
        Core.Engine.ExecuteCommand("mp_maxmoney 65535");
        Core.Engine.ExecuteCommand("mp_startmoney 65535");
        Core.Engine.ExecuteCommand("sv_infinite_ammo 2"); // Infinite ammo without reloading bypassing
        Core.Engine.ExecuteCommand("mp_give_player_c4 0"); // Prevent T side from spawning with C4
        Core.Engine.ExecuteCommand("mp_buy_allow_grenades 0"); // Prevent players from buying extra grenades
        Core.Engine.ExecuteCommand("mp_playercashawards 0"); // Disables all player cash award chat messages
        Core.Engine.ExecuteCommand("mp_teamcashawards 0"); // Disables all team cash award chat messages
        Core.Engine.ExecuteCommand("cash_player_killed_enemy_default 0"); // Backup: No money for kills
        Core.Engine.ExecuteCommand("cash_player_killed_enemy_factor 0");
        Core.Engine.ExecuteCommand("cash_team_per_dead_enemy 0");
        Core.Engine.ExecuteCommand("mp_respawn_on_death_t 1"); // Enable standard respawns just in case
        Core.Engine.ExecuteCommand("mp_respawn_on_death_ct 1");
        Core.Engine.ExecuteCommand("mp_restartgame 1");
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

        var hud = _serviceProvider?.GetService<IMenuHudService>();
        hud?.Unregister(Core);

        ServiceProviderFactory.DisposeServiceProvider(_serviceProvider);
        _serviceProvider = null;
    }
}
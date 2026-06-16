using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Defender.Interfaces;
using SwiftlyS2_Defender.Utils;
using System.Linq;

namespace SwiftlyS2_Defender.Services;

public sealed class RoundManagerService : IRoundManagerService
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;
    private readonly IScenarioPlaybackService _playback;
    private readonly IDefenderStateService _state;
    private readonly IRecordingService _recording;
    private readonly IScenarioVisualizationService _vis;

    public RoundManagerService(ISwiftlyCore core, ILogger logger, IScenarioPlaybackService playback, IDefenderStateService state, IRecordingService recording, IScenarioVisualizationService vis)
    {
        _core = core;
        _logger = logger;
        _playback = playback;
        _state = state;
        _recording = recording;
        _vis = vis;
    }

    public void HandlePlayerDeath(int victimSlot, int attackerSlot)
    {
        if (!_state.IsPlaying) return;

        var players = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();
        var victim = players.FirstOrDefault(p => p.Slot == victimSlot);
        
        if (victim != null && !PlayerUtil.IsBot(victim))
        {
            _logger.LogInformation("[Defender-Debug] HUMAN player {name} died! Force failing.", victim.Name);
            // Human player died, force fail
            ForceFail();
        }
        else if (victim != null && PlayerUtil.IsBot(victim))
        {
            _logger.LogInformation("[Defender-Debug] BOT {name} died!", victim.Name);
            // Check if all bots are dead (excluding this victim since PawnIsAlive might still be true)
            var aliveBots = players.Count(p => PlayerUtil.IsBot(p) && p.Controller != null && p.Controller.PawnIsAlive && p.Slot != victimSlot);
            if (aliveBots == 0)
            {
                // All bots dead, player succeeded
                ForceSuccess();
            }
        }
    }

    public void HandlePlayerHurt(int victimSlot, int attackerSlot, int damage)
    {
        // Could be used for stats, ignored for now
    }

    public void ForceFail()
    {
        _logger.LogInformation("[Defender] Attempt Failed! Returning to edit mode.");
        _logger.LogInformation("[Defender-Debug] StackTrace for ForceFail: " + Environment.StackTrace);
        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !PlayerUtil.IsBot(p)))
        {
            player.SendMessage(SwiftlyS2.Shared.Players.MessageType.Chat, "[red][Defender][white] Attempt Failed! Returning to edit mode.");
        }
        StopAndClean();
    }

    public void ForceSuccess()
    {
        _logger.LogInformation("[Defender] Attempt Succeeded! Returning to edit mode.");
        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !PlayerUtil.IsBot(p)))
        {
            player.SendMessage(SwiftlyS2.Shared.Players.MessageType.Chat, "[green][Defender][white] Attempt Succeeded! Returning to edit mode.");
        }
        StopAndClean();
    }

    public void StopAndClean()
    {
        _playback.StopScenario();
        _core.Scheduler.DelayBySeconds(2.0f, () => 
        {
            _core.Engine.ExecuteCommand("bot_kick");
            _core.Engine.ExecuteCommand("bot_quota 0");
        });
        
        // Clean up entities (Weapons/Items are cleaned up automatically by the engine or playback service, avoid ent_fire since it triggers cheat protection)
        
        var humans = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !PlayerUtil.IsBot(p)).ToList();
        foreach (var human in humans)
        {
            if (human.PlayerPawn != null)
            {
                human.PlayerPawn.Health = 100;
                // We can also teleport them back to their anchor if desired, 
                // but StopScenario already sets playing to false.
            }
        }

        var scenario = _recording.GetWipScenario();
        if (scenario != null)
        {
            _vis.DrawScenario(scenario);
        }
    }
}

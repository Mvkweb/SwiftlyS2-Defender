using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Defender.Interfaces;
using SwiftlyS2_Defender.Models;
using SwiftlyS2_Defender.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using SwiftlyS2.Shared.Natives;

namespace SwiftlyS2_Defender.Services;

public sealed class ScenarioPlaybackService : IScenarioPlaybackService
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;
    private readonly IDefenderStateService _state;

    private bool _isPlaying;
    private long _playbackStartTimeMs;
    private Scenario? _playingScenario;
    private readonly Dictionary<int, ScenarioBot> _botAssignments = new();

    public ScenarioPlaybackService(ISwiftlyCore core, ILogger logger, IDefenderStateService state)
    {
        _core = core;
        _logger = logger;
        _state = state;

        _core.Event.OnTick += OnTick;
    }

    public void PlayScenario(Scenario scenario, bool teleportHumans = true)
    {
        _playingScenario = scenario;
        _botAssignments.Clear();
        _isPlaying = true;
        _playbackStartTimeMs = Environment.TickCount64;
        _state.SetPlayingState(true);

        var humans = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !PlayerUtil.IsBot(p)).ToList();
        var human = humans.FirstOrDefault();
        string botCmd = "bot_add_t"; // default
        if (human != null && human.Controller != null && human.Controller.TeamNum == 2) // 2 is Terrorist
        {
            botCmd = "bot_add_ct";
        }

        var currentBots = _core.PlayerManager.GetAllPlayers().Count(p => p.IsValid && PlayerUtil.IsBot(p));
        var neededBots = scenario.Bots.Count;

        _logger.LogInformation("PlayScenario: Need {Needed} bots, currently have {Current}. Spawning...", neededBots, currentBots);

        int botsToSpawn = neededBots - currentBots;
        for (int i = 0; i < botsToSpawn; i++)
        {
            float delay = i * 0.2f;
            _core.Scheduler.DelayBySeconds(delay, () => _core.Engine.ExecuteCommand(botCmd));
        }

        ResetToStart(teleportHumans);
    }

    public void StopScenario()
    {
        _isPlaying = false;
        _playingScenario = null;
        _botAssignments.Clear();
        _state.SetPlayingState(false);
    }

    public void ResetToStart(bool teleportHumans = true)
    {
        if (_playingScenario == null) return;
        _playbackStartTimeMs = Environment.TickCount64;

        if (teleportHumans)
        {
            // Teleport player
            var humans = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !PlayerUtil.IsBot(p)).ToList();
            foreach (var human in humans)
            {
                if (human.PlayerPawn != null)
                    human.PlayerPawn.Teleport(new Vector(_playingScenario.Anchor.X, _playingScenario.Anchor.Y, _playingScenario.Anchor.Z), new QAngle(_playingScenario.Anchor.Pitch, _playingScenario.Anchor.Yaw, 0), Vector.Zero);
            }
        }

        // Let OnTick handle bot assignments dynamically as they spawn!
        _botAssignments.Clear();
    }

    private void OnTick()
    {
        if (!_isPlaying || _playingScenario == null) return;

        var elapsedMs = Environment.TickCount64 - _playbackStartTimeMs;

        var players = _core.PlayerManager.GetAllPlayers();
        var bots = players.Where(p => p.IsValid && PlayerUtil.IsBot(p)).ToList();

        // Assign newly spawned bots dynamically
        foreach (var scenarioBot in _playingScenario.Bots)
        {
            if (!_botAssignments.ContainsValue(scenarioBot))
            {
                var availableBot = bots.FirstOrDefault(b => !_botAssignments.ContainsKey(b.Slot));
                if (availableBot != null && availableBot.Controller != null && availableBot.Controller.PawnIsAlive && availableBot.PlayerPawn != null)
                {
                    _botAssignments[availableBot.Slot] = scenarioBot;
                    
                    // Teleport immediately to first frame if they just spawned
                    if (scenarioBot.Frames.Count > 0)
                    {
                        var firstFrame = scenarioBot.Frames[0];
                        availableBot.PlayerPawn.Teleport(new Vector(firstFrame.X, firstFrame.Y, firstFrame.Z), new QAngle(firstFrame.Pitch, firstFrame.Yaw, 0), Vector.Zero);
                    }
                    
                    // Fix CS2 Async Model Loading Bug
                    string modelToSet = "characters/models/tm_phoenix/tm_phoenix.vmdl";
                    var human = players.FirstOrDefault(p => !PlayerUtil.IsBot(p) && p.IsValid);
                    if (human != null && human.Controller != null && human.Controller.TeamNum == 2)
                    {
                        modelToSet = "characters/models/ctm_sas/ctm_sas.vmdl"; // If human is T, bots are CT
                    }
                    
                    _core.Scheduler.DelayBySeconds(0.3f, () =>
                    {
                        if (availableBot.PlayerPawn != null)
                        {
                            availableBot.PlayerPawn.SetModel(modelToSet);
                        }
                    });
                }
            }
        }

        foreach (var assignment in _botAssignments)
        {
            var botSlot = assignment.Key;
            var scenarioBot = assignment.Value;
            var botPlayer = players.FirstOrDefault(p => p.Slot == botSlot);
            
            if (botPlayer == null || !botPlayer.IsValid || botPlayer.PlayerPawn == null || botPlayer.Controller == null || !botPlayer.Controller.PawnIsAlive) continue;

            var lastFrame = scenarioBot.Frames.LastOrDefault();
            if (lastFrame != null && elapsedMs > lastFrame.TimeOffsetMs)
            {
                // Sequence finished, freeze bot position but track player aim
                var human = players.FirstOrDefault(p => !PlayerUtil.IsBot(p) && p.IsValid && p.Controller != null && p.Controller.PawnIsAlive && p.PlayerPawn != null);
                if (human != null && human.PlayerPawn != null && botPlayer.PlayerPawn != null && human.PlayerPawn.AbsOrigin.HasValue && botPlayer.PlayerPawn.AbsOrigin.HasValue)
                {
                    var dx = human.PlayerPawn.AbsOrigin.Value.X - botPlayer.PlayerPawn.AbsOrigin.Value.X;
                    var dy = human.PlayerPawn.AbsOrigin.Value.Y - botPlayer.PlayerPawn.AbsOrigin.Value.Y;
                    var dz = (human.PlayerPawn.AbsOrigin.Value.Z + 64f) - (botPlayer.PlayerPawn.AbsOrigin.Value.Z + 64f);
                    
                    var yaw = System.MathF.Atan2(dy, dx) * 180f / System.MathF.PI;
                    var pitch = System.MathF.Atan2(-dz, System.MathF.Sqrt(dx * dx + dy * dy)) * 180f / System.MathF.PI;
                    
                    botPlayer.PlayerPawn.Teleport(botPlayer.PlayerPawn.AbsOrigin.Value, new QAngle(pitch, yaw, 0), Vector.Zero);
                }
            }
            else
            {
                var frameIndex = scenarioBot.Frames.FindLastIndex(f => f.TimeOffsetMs <= elapsedMs);
                if (frameIndex >= 0)
                {
                    var frame = scenarioBot.Frames[frameIndex];
                    var velocity = Vector.Zero;

                    if (frameIndex > 0)
                    {
                        var prevFrame = scenarioBot.Frames[frameIndex - 1];
                        float dt = (frame.TimeOffsetMs - prevFrame.TimeOffsetMs) / 1000.0f;
                        if (dt > 0.001f)
                        {
                            velocity = new Vector(
                                (frame.X - prevFrame.X) / dt,
                                (frame.Y - prevFrame.Y) / dt,
                                (frame.Z - prevFrame.Z) / dt
                            );
                        }
                    }

                    botPlayer.PlayerPawn.Teleport(new Vector(frame.X, frame.Y, frame.Z), new QAngle(frame.Pitch, frame.Yaw, 0), velocity);
                }
            }
        }
    }
}

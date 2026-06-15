using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
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

    private bool _isPlaying = false;
    private long _playbackStartTimeMs;
    private Scenario? _playingScenario;
    private readonly Dictionary<int, ScenarioBot> _botAssignments = new();
    private readonly HashSet<ScenarioGrenade> _thrownGrenades = new();

    public ScenarioPlaybackService(ISwiftlyCore core, ILogger logger, IDefenderStateService state)
    {
        _core = core;
        _logger = logger;
        _state = state;

        _core.Event.OnTick += OnTick;
    }

    public void PlayScenario(Scenario scenario, bool teleportHumans = true, ulong? testingPlayerId = null)
    {
        _playingScenario = scenario;
        _botAssignments.Clear();
        _thrownGrenades.Clear();

        // Clear molotov fire patches and flying molotovs
        _core.Engine.ExecuteCommand("ent_fire inferno kill");
        _core.Engine.ExecuteCommand("ent_fire molotov_projectile kill");
        _core.Engine.ExecuteCommand("ent_fire hegrenade_projectile kill");

        _isPlaying = true;
        _playbackStartTimeMs = Environment.TickCount64;
        _state.SetPlayingState(true);

        _logger.LogInformation("PlayScenario: Need {totalBots} bots, currently have {currentBots}. Spawning...", scenario.Bots.Count, _core.PlayerManager.GetAllPlayers().Count(p => PlayerUtil.IsBot(p)));

        int totalBotsNeeded = scenario.Bots.Count;
        if (scenario.Grenades.Count > 0 && totalBotsNeeded == 0) totalBotsNeeded = 1; // Need at least 1 bot to throw grenades

        var bots = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && PlayerUtil.IsBot(p)).ToList();
        int currentBots = bots.Count;

        // Spawn bots if we don't have enough
        string botCmd = "bot_add_t";
        if (testingPlayerId != null)
        {
            var testingPlayer = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.SteamID == testingPlayerId.Value);
            if (testingPlayer != null && testingPlayer.Controller != null && testingPlayer.Controller.TeamNum == 2)
            {
                botCmd = "bot_add_ct"; // If player is T, bots should be CT
            }
        }
        else if (scenario.MapName != null) { /* we could choose team based on map logic if no player */ }

        if (currentBots < totalBotsNeeded)
        {
            for (int i = 0; i < totalBotsNeeded - currentBots; i++)
            {
                _core.Engine.ExecuteCommand(botCmd);
            }
        }

        ResetToStart(teleportHumans, testingPlayerId);
    }

    public void StopScenario()
    {
        _isPlaying = false;
        _playingScenario = null;
        _botAssignments.Clear();
        _state.SetPlayingState(false);

        ClearActiveGrenades();
    }

    public void ResetToStart(bool teleportHumans = true, ulong? testingPlayerId = null)
    {
        if (_playingScenario == null) return;

        ClearActiveGrenades();

        _playbackStartTimeMs = Environment.TickCount64;

        if (teleportHumans)
        {
            // Teleport testing player(s)
            var humans = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !PlayerUtil.IsBot(p)).ToList();
            if (testingPlayerId != null)
            {
                humans = humans.Where(p => p.SteamID == testingPlayerId.Value).ToList();
            }
            foreach (var p in humans)
            {
                if (_playingScenario.PlayerAnchor.IsSet)
                {
                    var position = new Vector(_playingScenario.PlayerAnchor.X, _playingScenario.PlayerAnchor.Y, _playingScenario.PlayerAnchor.Z + 10.0f);
                    // Set pitch to 0 to prevent the entire player model from tilting forward (Teleport rotates the base entity, not just the eyes!)
                    var viewAngles = new QAngle(0, _playingScenario.PlayerAnchor.Yaw, 0);
                    p.PlayerPawn?.Teleport(position, viewAngles, new Vector(0, 0, -100));
                }

                // Strip existing weapons and equip saved loadout
                if (p.PlayerPawn?.WeaponServices != null && p.PlayerPawn?.ItemServices != null)
                {
                    var currentWeapons = p.PlayerPawn.WeaponServices.MyWeapons.Select(w => w.Value?.DesignerName).Where(name => name != null).ToList();
                    foreach (var wepName in currentWeapons)
                    {
                        if (!string.IsNullOrEmpty(wepName))
                        {
                            p.PlayerPawn.WeaponServices.RemoveWeaponByDesignerNameAsync(wepName);
                        }
                    }

                    if (_playingScenario.PlayerLoadout != null && _playingScenario.PlayerLoadout.Count > 0)
                    {
                        // Delay equipping the loadout to give RemoveWeaponByDesignerNameAsync time to finish
                        // completely clearing the weapon slots. Otherwise GiveItem will silently fail.
                        _core.Scheduler.DelayBySeconds(0.1f, () =>
                        {
                            if (!p.IsValid || p.PlayerPawn?.ItemServices == null) return;

                            foreach (var savedWep in _playingScenario.PlayerLoadout)
                            {
                                p.PlayerPawn.ItemServices.GiveItem<CBasePlayerWeapon>(savedWep);
                            }
                        });
                    }
                }
            }
        }

        // Let OnTick handle bot assignments dynamically as they spawn!
        _botAssignments.Clear();
        _thrownGrenades.Clear();
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

        // Process Grenade Projectiles
        foreach (var grenade in _playingScenario.Grenades)
        {
            if (!_thrownGrenades.Contains(grenade) && elapsedMs >= grenade.TimeOffsetMs)
            {
                _thrownGrenades.Add(grenade);

                var pos = new Vector(grenade.OriginX, grenade.OriginY, grenade.OriginZ);
                var vel = new Vector(grenade.VelocityX, grenade.VelocityY, grenade.VelocityZ);
                var ang = SwiftlyS2.Shared.Natives.QAngle.Zero;

                try
                {
                    var throwerBotPawn = _botAssignments.Count > 0
                        ? _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.Slot == _botAssignments.Keys.First())?.PlayerPawn
                        : _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && PlayerUtil.IsBot(p))?.PlayerPawn;

                    if (grenade.GrenadeType == "flashbang_projectile")
                    {
                        SwiftlyS2.Shared.SchemaDefinitions.CFlashbangProjectile.EmitGrenade(pos, ang, vel, throwerBotPawn);
                    }
                    else if (grenade.GrenadeType == "hegrenade_projectile")
                    {
                        SwiftlyS2.Shared.SchemaDefinitions.CHEGrenadeProjectile.EmitGrenade(pos, ang, vel, throwerBotPawn);
                    }
                    else if (grenade.GrenadeType == "smokegrenade_projectile")
                    {
                        SwiftlyS2.Shared.SchemaDefinitions.CSmokeGrenadeProjectile.EmitGrenade(pos, ang, vel, (SwiftlyS2.Shared.Players.Team)2, throwerBotPawn);
                    }
                    else if (grenade.GrenadeType == "molotov_projectile")
                    {
                        SwiftlyS2.Shared.SchemaDefinitions.CMolotovProjectile.EmitGrenade(pos, ang, vel, (SwiftlyS2.Shared.Players.Team)2, throwerBotPawn);
                    }
                    else if (grenade.GrenadeType == "decoy_projectile")
                    {
                        SwiftlyS2.Shared.SchemaDefinitions.CDecoyProjectile.EmitGrenade(pos, ang, vel, throwerBotPawn);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation("[Defender-Debug] EmitGrenade EXCEPTION: " + ex.Message + "\n" + ex.StackTrace);
                }
            }
        }
    }

    private void ClearActiveGrenades()
    {
        string[] grenadeClasses = { 
            "molotov_projectile", 
            "hegrenade_projectile", 
            "flashbang_projectile", 
            "smokegrenade_projectile", 
            "decoy_projectile" 
        };

        try
        {
            foreach (var cls in grenadeClasses)
            {
                var ents = _core.EntitySystem.GetAllEntitiesByDesignerName<SwiftlyS2.Shared.SchemaDefinitions.CBaseEntity>(cls)?.ToList();
                if (ents != null)
                {
                    int count = 0;
                    foreach (var e in ents)
                    {
                        if (e != null && e.IsValid) 
                        {
                            e.AcceptInput<string>("Kill", "", null, null, 0);
                            count++;
                        }
                    }
                    if (count > 0) _logger.LogInformation($"[Defender-Debug-Cleanup] Killed {count} {cls}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Defender-Debug-Cleanup] Grenade error: {ex.Message}");
        }

        // Delay fire cleanup by 0.1s to catch mid-air detonations
        _core.Scheduler.DelayBySeconds(0.1f, () => 
        {
            try
            {
                string[] fireClasses = { "inferno", "cs_inferno" };
                foreach (var cls in fireClasses)
                {
                    var ents = _core.EntitySystem.GetAllEntitiesByDesignerName<SwiftlyS2.Shared.SchemaDefinitions.CBaseEntity>(cls)?.ToList();
                    if (ents != null)
                    {
                        int count = 0;
                        foreach (var e in ents)
                        {
                            if (e != null && e.IsValid) 
                            {
                                e.AcceptInput<string>("Kill", "", null, null, 0);
                                count++;
                            }
                        }
                        if (count > 0) _logger.LogInformation($"[Defender-Debug-Cleanup] Killed {count} {cls}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Defender-Debug-Cleanup] Fire error: {ex.Message}");
            }
        });
    }
}

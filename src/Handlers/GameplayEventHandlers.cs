using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.Events;
using SwiftlyS2_Defender.Interfaces;
using Microsoft.Extensions.Logging;
using System;

namespace SwiftlyS2_Defender.Handlers;

public sealed class GameplayEventHandlers
{
    private readonly IRoundManagerService _roundManager;
    private readonly IRecordingService _recording;
    private ISwiftlyCore? _core;
    private Guid _playerDeathHook;
    private Guid _playerHurtHook;
    private Guid _roundStartHook;
    private Guid _playerSpawnHook;
    private Guid _decalHook;
    private readonly HashSet<ulong> _welcomedPlayers = new();

    public GameplayEventHandlers(IRoundManagerService roundManager, IRecordingService recording)
    {
        _roundManager = roundManager;
        _recording = recording;
    }

    public void Register(ISwiftlyCore core)
    {
        _core = core;
        _playerDeathHook = core.GameEvent.HookPost<EventPlayerDeath>(OnPlayerDeath);
        _playerHurtHook = core.GameEvent.HookPost<EventPlayerHurt>(OnPlayerHurt);
        _roundStartHook = core.GameEvent.HookPost<EventRoundStart>(OnRoundStart);
        _playerSpawnHook = core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawn);
        _decalHook = core.NetMessage.HookServerMessage<CSVCMsg_BSPDecal>((msg) => HookResult.Stop);
        core.Event.OnEntityCreated += OnEntityCreated;
    }

    public void Unregister(ISwiftlyCore core)
    {
        if (_playerDeathHook != Guid.Empty) core.GameEvent.Unhook(_playerDeathHook);
        if (_playerHurtHook != Guid.Empty) core.GameEvent.Unhook(_playerHurtHook);
        if (_roundStartHook != Guid.Empty) core.GameEvent.Unhook(_roundStartHook);
        if (_playerSpawnHook != Guid.Empty) core.GameEvent.Unhook(_playerSpawnHook);
        if (_decalHook != Guid.Empty) core.NetMessage.Unhook(_decalHook);
        core.Event.OnEntityCreated -= OnEntityCreated;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        var victim = @event.UserIdPlayer;
        var attacker = @event.AttackerPlayer;
        
        if (victim != null && attacker != null)
        {
            var deathPos = victim.PlayerPawn?.CBodyComponent?.SceneNode?.AbsOrigin;
            var deathAngles = victim.PlayerPawn?.EyeAngles;

            _roundManager.HandlePlayerDeath(victim.Slot, attacker.Slot);

            // Fast instant respawn exactly where they died
            if (deathPos != null && deathAngles != null && !victim.IsFakeClient && _core != null)
            {
                // Delay by 0.1s to ensure engine fully processes death and physics settle
                _core.Scheduler.DelayBySeconds(0.1f, () => 
                {
                    if (victim.IsValid)
                    {
                        victim.Respawn();
                        victim.PlayerPawn?.Teleport(deathPos.Value, deathAngles.Value, new Vector(0,0,0));
                    }
                });
            }
        }

        return HookResult.Continue;
    }

    private void OnEntityCreated(IOnEntityCreatedEvent @event)
    {
        if (!_recording.IsRecordingGrenade) return;

        var entity = @event.Entity;
        if (entity != null && _core != null)
        {
            string initName = entity.DesignerName ?? "null";
            _core.Logger.LogInformation("[Defender-Debug] OnEntityCreated fired for entity (InitName: {initName})", initName);

            // Delay by 1 tick so entity is fully initialized
            _core.Scheduler.DelayBySeconds(0.01f, () => 
            {
                if (!entity.IsValid) {
                    _core.Logger.LogInformation("[Defender-Debug] Entity became invalid after 1 tick.");
                    return;
                }
                
                string delayedName = entity.DesignerName ?? "null";
                _core.Logger.LogInformation("[Defender-Debug] Delayed tick. DesignerName: {name}", delayedName);

                if (delayedName.Contains("_projectile"))
                {
                    _core.Logger.LogInformation("[Defender-Debug] Entity is a projectile!");
                    
                    if (entity is SwiftlyS2.Shared.SchemaDefinitions.CBaseCSGrenadeProjectile proj)
                    {
                        _core.Logger.LogInformation("[Defender-Debug] Entity casted to CBaseCSGrenadeProjectile successfully.");
                        if (proj.AbsOrigin.HasValue)
                        {
                            _recording.LogProjectileSpawned(proj.DesignerName, proj.AbsOrigin.Value, proj.AbsVelocity);
                        }
                        else
                        {
                            _core.Logger.LogInformation("[Defender-Debug] AbsOrigin is null!");
                        }
                    }
                    else
                    {
                        _core.Logger.LogInformation("[Defender-Debug] Failed to cast to CBaseCSGrenadeProjectile. Type: {type}", entity.GetType().Name);
                    }
                }
            });
        }
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event)
    {
        var victim = @event.UserIdPlayer;
        var attacker = @event.AttackerPlayer;
        var damage = @event.ActualDmgHealth;

        if (victim != null && attacker != null)
        {
            _roundManager.HandlePlayerHurt(victim.Slot, attacker.Slot, damage);
        }

        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        if (_core == null) return HookResult.Continue;
        
        // Enforce Defender Gamemode (Infinite Warmup, no standard bots)
        _core.Engine.ExecuteCommand("mp_warmuptime 999999");
        _core.Engine.ExecuteCommand("mp_warmup_pausetimer 1");
        _core.Engine.ExecuteCommand("mp_autoteambalance 0");
        _core.Engine.ExecuteCommand("mp_limitteams 0");
        _core.Engine.ExecuteCommand("mp_respawn_on_death_t 1");
        _core.Engine.ExecuteCommand("mp_respawn_on_death_ct 1");
        _core.Engine.ExecuteCommand("mp_respawn_immunitytime -1"); // Fix invisible bots / ghost effect
        _core.Engine.ExecuteCommand("mp_respawn_immunitytime_warmup -1");
        _core.Engine.ExecuteCommand("mp_spawnprotectiontime 0");
        _core.Engine.ExecuteCommand("mp_death_drop_gun 0"); // Prevent weapons dropping on death
        _core.Engine.ExecuteCommand("mp_death_drop_defuser 0");
        _core.Engine.ExecuteCommand("mp_death_drop_grenade 0");
        _core.Engine.ExecuteCommand("mp_drop_knife_enable 0");
        _core.Engine.ExecuteCommand("bot_quota 0");

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        if (_core == null) return HookResult.Continue;

        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid) return HookResult.Continue;

        // Ensure we only welcome real players once per session
        if (player.SteamID != 0 && !_welcomedPlayers.Contains(player.SteamID))
        {
            _welcomedPlayers.Add(player.SteamID);
            
            _core.Scheduler.DelayBySeconds(2.0f, () => 
            {
                if (player.IsValid)
                {
                    player.SendMessage(SwiftlyS2.Shared.Players.MessageType.Chat, "[green][Defender][white] Welcome to the Defender plugin!");
                    player.SendMessage(SwiftlyS2.Shared.Players.MessageType.Chat, "[green][Defender][white] Create a scenario using: [lightred]!setup <name>[white]");
                    player.SendMessage(SwiftlyS2.Shared.Players.MessageType.Chat, "[green][Defender][white] Then use [lightred]!set_player[white] and [lightred]!bot[white]");
                }
            });
        }

        return HookResult.Continue;
    }
}

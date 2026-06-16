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
    private readonly IDefenderStateService _state;
    private ISwiftlyCore? _core;
    private Guid _playerDeathHook;
    private Guid _playerHurtHook;
    private Guid _roundStartHook;
    private Guid _playerSpawnHook;
    private Guid _decalHook;
    private readonly HashSet<ulong> _welcomedPlayers = new();

    public GameplayEventHandlers(IRoundManagerService roundManager, IRecordingService recording, IDefenderStateService state)
    {
        _roundManager = roundManager;
        _recording = recording;
        _state = state;
    }

    public void Register(ISwiftlyCore core)
    {
        _core = core;
        _playerDeathHook = core.GameEvent.HookPost<EventPlayerDeath>(OnPlayerDeath);
        _playerHurtHook = core.GameEvent.HookPost<EventPlayerHurt>(OnPlayerHurt);
        _roundStartHook = core.GameEvent.HookPost<EventRoundStart>(OnRoundStart);
        _playerSpawnHook = core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawn);
        _decalHook = core.NetMessage.HookServerMessage<SwiftlyS2.Shared.ProtobufDefinitions.CMsgPlaceDecalEvent>(OnPlaceDecal);
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

        if (victim == null) return HookResult.Continue;

        // Let RoundManager handle it (it checks IsPlaying and Bot vs Human internally)
        _roundManager.HandlePlayerDeath(victim.Slot, attacker?.Slot ?? -1);

        if (!_state.IsPlaying)
        {
            var deathPos = victim.PlayerPawn?.CBodyComponent?.SceneNode?.AbsOrigin;
            var deathAngles = victim.PlayerPawn?.EyeAngles;

            // Fast instant respawn exactly where they died
            if (deathPos != null && deathAngles != null && !victim.IsFakeClient && _core != null)
            {
                // Delay by 0.1s to ensure engine fully processes death and team changes
                _core.Scheduler.DelayBySeconds(0.1f, () => 
                {
                    // Check TeamNum HERE, after the delay, to ensure they didn't just join Spectator (Team 1)
                    if (victim.IsValid && victim.Controller != null && (victim.Controller.TeamNum == 2 || victim.Controller.TeamNum == 3))
                    {
                        victim.Respawn();
                        // Add +10 to Z to prevent spawning inside the floor or death ragdolls
                        var safePos = new Vector(deathPos.Value.X, deathPos.Value.Y, deathPos.Value.Z + 10.0f);
                        // Zero out pitch to prevent the player model from tilting forward into the floor
                        var safeAngles = new QAngle(0, deathAngles.Value.Y, 0);
                        victim.PlayerPawn?.Teleport(safePos, safeAngles, new Vector(0, 0, -100));
                    }
                });
            }
        }

        return HookResult.Continue;
    }

    private void OnEntityCreated(IOnEntityCreatedEvent @event)
    {
        var entity = @event.Entity;
        if (entity == null) return;

        string initName = entity.DesignerName ?? "";

        // Block blood decal entities from ever spawning (if any)
        if (initName.Contains("decal") || initName.Contains("blood"))
        {
            entity.AcceptInput<string>("Kill", "", null, null, 0);
            return;
        }

        // --- SWIFTLYCHAN DEBUG ---
        if (initName.Contains("sound") || initName.Contains("snd_event"))
        {
            if (_core != null)
            {
                _core.Logger.LogInformation("[Defender-Debug] Sound entity spawned: {name} | index: {h}", initName, entity.Index);
            }
        }
        // -------------------------

        if (!_recording.IsRecordingGrenade) return;

        if (_core != null)
        {
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
                            _recording.LogProjectileSpawned(proj);
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

    private HookResult OnPlaceDecal(SwiftlyS2.Shared.ProtobufDefinitions.CMsgPlaceDecalEvent msg)
    {
        // Stop all decal network messages entirely, which prevents client-side blood and bullet holes.
        return HookResult.Stop;
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
        
        // Enforce Defender Gamemode (Fake Warmup, no standard bots)
        _core.Engine.ExecuteCommand("mp_warmup_end");
        _core.Engine.ExecuteCommand("mp_ignore_round_win_conditions 1"); // Rounds never end
        _core.Engine.ExecuteCommand("mp_roundtime 60");
        _core.Engine.ExecuteCommand("mp_freezetime 0");
        _core.Engine.ExecuteCommand("mp_buytime 9999");
        _core.Engine.ExecuteCommand("mp_buy_anywhere 1");
        _core.Engine.ExecuteCommand("mp_maxmoney 65535");
        _core.Engine.ExecuteCommand("mp_startmoney 65535");
        _core.Engine.ExecuteCommand("sv_infinite_ammo 2");
        _core.Engine.ExecuteCommand("mp_give_player_c4 0"); // Prevent T side from spawning with C4
        _core.Engine.ExecuteCommand("mp_buy_allow_grenades 0"); // Prevent players from buying extra grenades
        _core.Engine.ExecuteCommand("mp_playercashawards 0"); // Disables all player cash award chat messages
        _core.Engine.ExecuteCommand("mp_teamcashawards 0"); // Disables all team cash award chat messages
        _core.Engine.ExecuteCommand("cash_player_killed_enemy_default 0"); // Backup: No money for kills
        _core.Engine.ExecuteCommand("cash_player_killed_enemy_factor 0");
        _core.Engine.ExecuteCommand("cash_team_per_dead_enemy 0");
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

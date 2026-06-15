using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2_Defender.Interfaces;
using SwiftlyS2_Defender.Models;
using System.Linq;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Natives;

namespace SwiftlyS2_Defender.Services;

public sealed class RecordingService : IRecordingService
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;
    private readonly IDefenderStateService _state;
    private readonly IScenarioPlaybackService _playback;
    private readonly IDefenderConfigService _config;
    private readonly IScenarioVisualizationService _vis;

    private Scenario? _wipScenario;
    private ScenarioBot? _activeBot;
    private bool _isRecordingGrenade = false;
    public bool IsRecordingGrenade => _isRecordingGrenade;
    private long _recordingStartTimeMs;
    private ulong _recordingPlayerId;
    
    private bool _isCountingDown;
    private long _lastFrameMs;
    private const int FrameIntervalMs = 0; // Record every tick for perfect smoothness

    private Vector? _lastPlayerPos;
    private long _lastMoveTimeMs;

    public RecordingService(ISwiftlyCore core, ILogger logger, IDefenderStateService state, IScenarioPlaybackService playback, IDefenderConfigService config, IScenarioVisualizationService vis)
    {
        _core = core;
        _logger = logger;
        _state = state;
        _playback = playback;
        _config = config;
        _vis = vis;

        _core.Event.OnTick += OnTick;
    }

    public void SetupScenario(string name)
    {
        _wipScenario = new Scenario { Name = name };
        _logger.LogInformation("Creating new scenario: {Name}", name);
    }

    public void SetPlayerDefendAnchor(ulong steamId)
    {
        if (_wipScenario == null) return;
        var player = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.SteamID == steamId);
        if (player == null || !player.IsValid || player.PlayerPawn == null) return;

        var origin = player.PlayerPawn.CBodyComponent?.SceneNode?.AbsOrigin;
        var viewAngles = player.PlayerPawn.EyeAngles;

        if (origin == null) return;

        _wipScenario.PlayerAnchor = new ScenarioAnchor
        {
            X = origin.Value.X,
            Y = origin.Value.Y,
            Z = origin.Value.Z,
            Pitch = viewAngles.X,
            Yaw = viewAngles.Y
        };

        _wipScenario.PlayerLoadout.Clear();
        if (player.PlayerPawn?.WeaponServices != null)
        {
            foreach (var handle in player.PlayerPawn.WeaponServices.MyWeapons)
            {
                var weapon = handle.Value;
                if (weapon != null && !string.IsNullOrEmpty(weapon.DesignerName))
                {
                    _wipScenario.PlayerLoadout.Add(weapon.DesignerName);
                }
            }
        }
        
        _logger.LogInformation("Anchor set.");
    }

    public void StartRecordingBot(ulong steamId)
    {
        if (_wipScenario == null) return;

        var player = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.SteamID == steamId);
        if (player == null || !player.IsValid || player.PlayerPawn == null) return;

        var origin = player.PlayerPawn.CBodyComponent?.SceneNode?.AbsOrigin;
        var viewAngles = player.PlayerPawn.EyeAngles;

        if (origin != null && !_wipScenario.Anchor.IsSet)
        {
            _wipScenario.Anchor = new ScenarioAnchor
            {
                X = origin.Value.X,
                Y = origin.Value.Y,
                Z = origin.Value.Z,
                Pitch = viewAngles.X,
                Yaw = viewAngles.Y
            };
        }

        _recordingPlayerId = steamId;
        _activeBot = new ScenarioBot();
        _isCountingDown = true;
        _state.SetRecordingState(true);

        if (player != null)
        {
            _core.Scheduler.DelayBySeconds(1.0f, () => player.SendMessage(MessageType.Chat, "3..."));
            _core.Scheduler.DelayBySeconds(2.0f, () => player.SendMessage(MessageType.Chat, "2..."));
            _core.Scheduler.DelayBySeconds(3.0f, () => player.SendMessage(MessageType.Chat, "1..."));
            _core.Scheduler.DelayBySeconds(4.0f, () => 
            {
                player.SendMessage(MessageType.Chat, "GO!");
                _isCountingDown = false;
                _recordingStartTimeMs = Environment.TickCount64;
                _lastMoveTimeMs = _recordingStartTimeMs;
                _lastPlayerPos = null;
            });
        }
    }

    public void StartRecordingGrenade(ulong steamId, string grenadeType)
    {
        if (_wipScenario == null) return;

        var player = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.SteamID == steamId);
        if (player != null && player.IsValid && player.PlayerPawn != null)
        {
            var origin = player.PlayerPawn.CBodyComponent?.SceneNode?.AbsOrigin;
            var viewAngles = player.PlayerPawn.EyeAngles;

            if (origin != null && !_wipScenario.Anchor.IsSet)
            {
                _wipScenario.Anchor = new ScenarioAnchor
                {
                    X = origin.Value.X,
                    Y = origin.Value.Y,
                    Z = origin.Value.Z,
                    Pitch = viewAngles.X,
                    Yaw = viewAngles.Y
                };
            }
        }
        _state.SetRecordingState(true);
        _isRecordingGrenade = true;
        _recordingStartTimeMs = Environment.TickCount64;
        // Start counting the ticks/frames offset for the grenade
        _activeBot = new ScenarioBot { Loadout = grenadeType };
        _activeBot.Frames.Clear(); 
        // We use activeBot temporarily just to track the time offset since recording started.
        // The actual throw will be logged by LogProjectileSpawned.
        _logger.LogInformation("Recording grenade throw...");
    }

    public void LogProjectileSpawned(string designerName, Vector origin, Vector velocity)
    {
        if (!_state.IsRecording || _activeBot == null || _wipScenario == null) return;
        
        long timeOffset;
        if (_isRecordingGrenade)
            timeOffset = 0; // Independent grenade throws always start at 0ms delay
        else
            timeOffset = _activeBot.Frames.Count > 0 ? _activeBot.Frames.Last().TimeOffsetMs : 0;
        
        _wipScenario.Grenades.Add(new ScenarioGrenade
        {
            GrenadeType = designerName,
            TimeOffsetMs = timeOffset,
            OriginX = origin.X,
            OriginY = origin.Y,
            OriginZ = origin.Z,
            VelocityX = velocity.X,
            VelocityY = velocity.Y,
            VelocityZ = velocity.Z
        });

        _logger.LogInformation("Logged projectile {Name} at {X}, {Y}, {Z}", designerName, origin.X, origin.Y, origin.Z);

        foreach (var p in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !p.IsFakeClient))
        {
            p.SendMessage(SwiftlyS2.Shared.Players.MessageType.Chat, "[green][Defender][default] Grenade recorded! Use [lightred]!setup[default] or edit mode to finish.");
        }
        
        // Auto-stop recording
        StopRecording(0); // Pass 0, StopRecording will handle it
    }

    public void StopRecording(ulong steamId)
    {
        if (!_state.IsRecording || _activeBot == null) return;

        if (!_isRecordingGrenade)
        {
            _wipScenario?.Bots.Add(_activeBot);
        }
        
        _logger.LogInformation("Recording stopped. Recorded {Count} frames.", _activeBot.Frames.Count);
        if (_wipScenario != null)
        {
            _vis.DrawScenario(_wipScenario);
        }
        _activeBot = null;
        _isRecordingGrenade = false;
        
        _state.SetRecordingState(false);
        _playback.StopScenario();
        _core.Engine.ExecuteCommand("bot_kick");
        _core.Engine.ExecuteCommand("bot_quota 0");
        
        var player = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.SteamID == steamId);
        if (player != null) player.SendMessage(MessageType.Chat, "Recording stopped and saved to scenario.");
    }

    public void ClearLastElement()
    {
        if (_wipScenario == null) return;
        if (_wipScenario.Bots.Count > 0)
        {
            _wipScenario.Bots.RemoveAt(_wipScenario.Bots.Count - 1);
        }
    }

    public void SaveScenario()
    {
        if (_wipScenario == null) return;
        _config.SaveScenario(_wipScenario);
        _wipScenario = null;
    }

    public Scenario? GetWipScenario() => _wipScenario;

    private void OnTick()
    {
        if (!_state.IsRecording || _isCountingDown || _activeBot == null) return;

        var nowMs = Environment.TickCount64;
        if (nowMs - _lastFrameMs >= FrameIntervalMs)
        {
            _lastFrameMs = nowMs;
            var player = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.SteamID == _recordingPlayerId);
            if (player == null || !player.IsValid || player.PlayerPawn == null || player.Controller == null || !player.Controller.PawnIsAlive)
            {
                StopRecording(_recordingPlayerId);
                return;
            }

            var origin = player.PlayerPawn.CBodyComponent?.SceneNode?.AbsOrigin;
            var viewAngle = player.PlayerPawn.EyeAngles;

            if (origin == null) return;

            if (_lastPlayerPos != null && _lastPlayerPos.HasValue)
            {
                var dx = origin.Value.X - _lastPlayerPos.Value.X;
                var dy = origin.Value.Y - _lastPlayerPos.Value.Y;
                var dz = origin.Value.Z - _lastPlayerPos.Value.Z;
                var distSq = dx * dx + dy * dy + dz * dz;

                if (distSq > 0.1f) // Player moved
                {
                    _lastMoveTimeMs = nowMs;
                    _lastPlayerPos = origin.Value;
                }
                else if (!_isRecordingGrenade && nowMs - _lastMoveTimeMs > 500) // 0.5 seconds of inactivity
                {
                    // Truncate frames that were recorded while stationary
                    long stopTimeMs = _lastMoveTimeMs - _recordingStartTimeMs;
                    _activeBot.Frames = _activeBot.Frames.Where(f => f.TimeOffsetMs <= stopTimeMs).ToList();

                    player.SendMessage(MessageType.Chat, "[green][Defender][white] Auto-stopped recording due to inactivity.");
                    StopRecording(_recordingPlayerId);
                    return;
                }
            }
            else
            {
                _lastPlayerPos = origin.Value;
                _lastMoveTimeMs = nowMs;
            }

            _activeBot.Frames.Add(new BotFrame
            {
                TimeOffsetMs = nowMs - _recordingStartTimeMs,
                X = origin.Value.X,
                Y = origin.Value.Y,
                Z = origin.Value.Z,
                Pitch = viewAngle.X,
                Yaw = viewAngle.Y
            });
        }
    }
}

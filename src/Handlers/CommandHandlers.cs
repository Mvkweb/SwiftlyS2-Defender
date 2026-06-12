using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2_Defender.Interfaces;
using System.Linq;

namespace SwiftlyS2_Defender.Handlers;

public sealed class CommandHandlers
{
    private readonly IDefenderStateService _state;
    private readonly IDefenderConfigService _config;
    private readonly IRecordingService _recording;
    private readonly IScenarioPlaybackService _playback;
    private readonly IScenarioVisualizationService _vis;
    private readonly List<Guid> _commandGuids = new();

    public CommandHandlers(
        IDefenderStateService state, 
        IDefenderConfigService config, 
        IRecordingService recording, 
        IScenarioPlaybackService playback,
        IScenarioVisualizationService vis)
    {
        _state = state;
        _config = config;
        _recording = recording;
        _playback = playback;
        _vis = vis;
    }

    public void Register(ISwiftlyCore core)
    {
        _commandGuids.Add(core.Command.RegisterCommand("setup", OnSetupScenario, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("set_player", OnSetPlayer, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("bot", OnRecordBot, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("record_bot", OnRecordBot, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("stop", OnStopRecording, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("clear_last", OnClearLast, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("remove_bot", OnRemoveBot, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("save_scenario", OnSaveScenario, registerRaw: true));
        
        _commandGuids.Add(core.Command.RegisterCommand("test", OnTestScenario, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("test_scenario", OnTestScenario, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("prac", OnPracScenario, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("edit_mode", OnEditMode, registerRaw: true));
    }

    public void Unregister(ISwiftlyCore core)
    {
        foreach (var id in _commandGuids)
        {
            core.Command.UnregisterCommand(id);
        }
        _commandGuids.Clear();
    }

    private void OnSetupScenario(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        var name = string.Join(" ", context.Args);
        if (string.IsNullOrWhiteSpace(name))
        {
            context.Reply("Usage: !setup <name>");
            return;
        }

        // Try to load existing scenario, otherwise create new
        var existing = _config.LoadScenario(name);
        if (existing != null)
        {
            _recording.SetupScenario(name); // Will recreate it
            var wip = _recording.GetWipScenario();
            if (wip != null)
            {
                wip.Anchor = existing.Anchor;
                wip.Bots.AddRange(existing.Bots);
                wip.Grenades.AddRange(existing.Grenades);
                _vis.DrawScenario(wip);
            }
            context.Reply($"[green][Defender][default] Scenario {name} loaded for editing.");
        }
        else
        {
            _recording.SetupScenario(name);
            context.Reply($"[green][Defender][default] Scenario {name} created. Use !set_player to set anchor.");
        }
    }

    private void OnSetPlayer(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        _recording.SetPlayerAnchor(player.SteamID);
        var wip = _recording.GetWipScenario();
        if (wip != null)
        {
            _vis.DrawScenario(wip);
        }
        context.Reply("[green][Defender][default] Anchor set for player.");
    }

    private void OnRecordBot(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        _recording.StartRecordingBot(player.SteamID);
        context.Reply("[green][Defender][default] Bot path recording starting...");
    }

    private void OnStopRecording(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        _recording.StopRecording(player.SteamID);
        context.Reply("[green][Defender][default] Recording stopped and saved.");
    }

    private void OnClearLast(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        _recording.ClearLastElement();
        context.Reply("Cleared last recorded element.");
    }

    private void OnSaveScenario(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        _recording.SaveScenario();
        context.Reply("Scenario saved!");
    }

    private void OnTestScenario(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        var scenario = _recording.GetWipScenario();
        if (scenario != null)
        {
            _vis.ClearVisualizations();
            _playback.PlayScenario(scenario);
            context.Reply("[green][Defender][default] Testing active scenario...");
        }
    }

    private void OnPracScenario(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        var name = string.Join(" ", context.Args);
        if (string.IsNullOrWhiteSpace(name)) return;

        var scenario = _config.LoadScenario(name);
        if (scenario != null)
        {
            _vis.ClearVisualizations();
            _playback.PlayScenario(scenario);
            context.Reply($"[green][Defender][default] Playing scenario {name}...");
        }
    }

    private void OnRemoveBot(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        var idStr = context.Args.FirstOrDefault();
        if (int.TryParse(idStr, out int id))
        {
            var wip = _recording.GetWipScenario();
            if (wip != null && id > 0 && id <= wip.Bots.Count)
            {
                wip.Bots.RemoveAt(id - 1);
                _vis.DrawScenario(wip);
                context.Reply($"[green][Defender][default] Removed bot #{id}.");
                return;
            }
        }
        context.Reply("Usage: !remove_bot <id>");
    }

    private void OnEditMode(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        
        _playback.StopScenario();
        var wip = _recording.GetWipScenario();
        if (wip != null)
        {
            _vis.DrawScenario(wip);
        }
        context.Reply("[green][Defender][default] Returned to Edit Mode.");
    }
}

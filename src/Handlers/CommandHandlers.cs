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

    private ISwiftlyCore? _core;

    public void Register(ISwiftlyCore core)
    {
        _core = core;
        _commandGuids.Add(core.Command.RegisterCommand("setup", OnSetupScenario, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("player", OnSetPlayer, registerRaw: true));
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

        _commandGuids.Add(core.Command.RegisterCommand("gflash", OnGiveGrenade, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("gsmoke", OnGiveGrenade, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("gmolotov", OnGiveGrenade, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("ggrenade", OnGiveGrenade, registerRaw: true));

        _commandGuids.Add(core.Command.RegisterCommand("nades", OnListNades, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("nade+", OnNadeAdd, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("nade-", OnNadeSub, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("nade", OnNadeSet, registerRaw: true));

        _commandGuids.Add(core.Command.RegisterCommand("ak", OnGiveAk, registerRaw: true));
        _commandGuids.Add(core.Command.RegisterCommand("ak47", OnGiveAk, registerRaw: true));
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
        _recording.SetPlayerDefendAnchor(player.SteamID);
        var wip = _recording.GetWipScenario();
        if (wip != null)
        {
            _vis.DrawScenario(wip);
        }
        context.Reply("[green][Defender][default] Player defend anchor set!");
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

        var available = _config.GetAvailableScenarios().ToList();

        if (context.Args.Length > 0)
        {
            if (int.TryParse(context.Args[0], out int id) && id >= 0 && id < available.Count)
            {
                var nameToLoad = available[id];
                var loaded = _config.LoadScenario(nameToLoad);
                if (loaded != null)
                {
                    if (!loaded.Anchor.IsSet)
                    {
                        context.Reply("[red][Defender][white] Scenario has no anchor set!");
                        return;
                    }
                    _vis.ClearVisualizations();
                    _playback.PlayScenario(loaded, true, player.SteamID);
                    context.Reply($"[green][Defender][default] Testing scenario: {nameToLoad}...");
                    return;
                }
            }
            context.Reply($"[red][Defender][white] Invalid scenario ID. Type !test to see available scenarios.");
            return;
        }

        var scenario = _recording.GetWipScenario();
        if (scenario != null)
        {
            if (!scenario.Anchor.IsSet)
            {
                context.Reply("[red][Defender][white] You must set a player anchor with [lightred]!player[white] before testing!");
                return;
            }

            // Update loadout to current weapons
            scenario.PlayerLoadout.Clear();
            if (player.PlayerPawn?.WeaponServices != null)
            {
                foreach (var handle in player.PlayerPawn.WeaponServices.MyWeapons)
                {
                    var weapon = handle.Value;
                    if (weapon != null && !string.IsNullOrEmpty(weapon.DesignerName))
                    {
                        scenario.PlayerLoadout.Add(weapon.DesignerName);
                    }
                }
            }

            _vis.ClearVisualizations();
            _playback.PlayScenario(scenario, true, player.SteamID);
            context.Reply("[green][Defender][default] Testing active scenario...");
        }
        else
        {
            if (available.Count == 0)
            {
                context.Reply("[red][Defender][white] No scenarios saved! Use !setup to create one.");
            }
            else
            {
                context.Reply("[green][Defender][white] No active scenario loaded. Available scenarios:");
                for (int i = 0; i < available.Count; i++)
                {
                    context.Reply($"[green]{i}[white] - {available[i]}");
                }
                context.Reply("Type [lightred]!test <id>[white] to test one.");
            }
        }
    }

    private void OnPracScenario(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        
        var available = _config.GetAvailableScenarios().ToList();

        if (context.Args.Length > 0)
        {
            if (int.TryParse(context.Args[0], out int id) && id >= 0 && id < available.Count)
            {
                var nameToLoad = available[id];
                var loaded = _config.LoadScenario(nameToLoad);
                if (loaded != null)
                {
                    _vis.ClearVisualizations();
                    _playback.PlayScenario(loaded, true, player.SteamID);
                    context.Reply($"[green][Defender][default] Playing scenario: {nameToLoad}...");
                    return;
                }
            }
            else
            {
                var name = string.Join(" ", context.Args);
                var scenario = _config.LoadScenario(name);
                if (scenario != null)
                {
                    _vis.ClearVisualizations();
                    _playback.PlayScenario(scenario, true, player.SteamID);
                    context.Reply($"[green][Defender][default] Playing scenario: {name}...");
                    return;
                }
            }
        }

        if (available.Count == 0)
        {
            context.Reply("[red][Defender][white] No scenarios saved!");
        }
        else
        {
            context.Reply("[green][Defender][white] Available scenarios:");
            for (int i = 0; i < available.Count; i++)
            {
                context.Reply($"[green]{i}[white] - {available[i]}");
            }
            context.Reply("Type [lightred]!prac <id>[white] to play one.");
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
        context.Reply("[green][Defender][default] Returned to edit mode.");
    }

    private void OnGiveGrenade(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        var wip = _recording.GetWipScenario();
        if (wip == null)
        {
            context.Reply("[red][Defender][white] Use !setup before recording grenades!");
            return;
        }

        string cmd = context.CommandName;
        string wepName = "weapon_flashbang";
        string typeName = "Flashbang";

        if (cmd == "gsmoke") { wepName = "weapon_smokegrenade"; typeName = "Smoke"; }
        else if (cmd == "gmolotov") { wepName = "weapon_molotov"; typeName = "Molotov"; } // Also give incendiary if CT? Molotov is standard.
        else if (cmd == "ggrenade") { wepName = "weapon_hegrenade"; typeName = "HE Grenade"; }

        // Give the player the grenade
        if (player.PlayerPawn?.ItemServices != null)
        {
            player.PlayerPawn.ItemServices.GiveItem<SwiftlyS2.Shared.SchemaDefinitions.CBasePlayerWeapon>(wepName);
        }

        // Enable grenade previews
        if (_core != null)
        {
            _core.Engine.ExecuteCommand("sv_cheats 1");
            _core.Engine.ExecuteCommand("sv_grenade_trajectory_prac_pipreview 1");
            _core.Engine.ExecuteCommand("ammo_grenade_limit_total 5");
        }

        // Inform RecordingService to expect a throw from this player
        _recording.StartRecordingGrenade(player.SteamID, wepName);

        context.Reply($"[green][Defender][default] {typeName} equipped! Throw it to record its trajectory.");
    }

    private void OnListNades(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        var wip = _recording.GetWipScenario();
        if (wip == null || wip.Grenades.Count == 0)
        {
            context.Reply("[red][Defender][white] No grenades in the current scenario.");
            return;
        }

        context.Reply("[green][Defender][white] Recorded Grenades:");
        for (int i = 0; i < wip.Grenades.Count; i++)
        {
            context.Reply($"  [yellow]ID {i}[white]: {wip.Grenades[i].GrenadeType} @ {wip.Grenades[i].TimeOffsetMs}ms");
        }
        context.Reply("Use [yellow]!nade <id> <ms>[white] to set, or [yellow]!nade+/- <id> <ms>[white] to adjust.");
    }

    private void OnNadeAdd(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        var wip = _recording.GetWipScenario();
        if (wip == null || wip.Grenades.Count == 0)
        {
            context.Reply("[red][Defender][white] No grenades in the current scenario.");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply("Usage: !nade+ <id> <ms>. Example: !nade+ 0 200");
            return;
        }

        if (int.TryParse(context.Args[0], out int id) && id >= 0 && id < wip.Grenades.Count)
        {
            if (int.TryParse(context.Args[1], out int ms))
            {
                wip.Grenades[id].TimeOffsetMs += ms;
                context.Reply($"[green][Defender][white] Grenade #{id} +{ms}ms → now at {wip.Grenades[id].TimeOffsetMs}ms");
            }
        }
        else
        {
            context.Reply("[red][Defender][white] Invalid grenade ID. Use !nades to list.");
        }
    }

    private void OnNadeSub(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        var wip = _recording.GetWipScenario();
        if (wip == null || wip.Grenades.Count == 0)
        {
            context.Reply("[red][Defender][white] No grenades in the current scenario.");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply("Usage: !nade- <id> <ms>. Example: !nade- 0 200");
            return;
        }

        if (int.TryParse(context.Args[0], out int id) && id >= 0 && id < wip.Grenades.Count)
        {
            if (int.TryParse(context.Args[1], out int ms))
            {
                wip.Grenades[id].TimeOffsetMs -= ms;
                if (wip.Grenades[id].TimeOffsetMs < 0) wip.Grenades[id].TimeOffsetMs = 0;
                context.Reply($"[green][Defender][white] Grenade #{id} -{ms}ms → now at {wip.Grenades[id].TimeOffsetMs}ms");
            }
        }
        else
        {
            context.Reply("[red][Defender][white] Invalid grenade ID. Use !nades to list.");
        }
    }

    private void OnNadeSet(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        var wip = _recording.GetWipScenario();
        if (wip == null || wip.Grenades.Count == 0)
        {
            context.Reply("[red][Defender][white] No grenades in the current scenario.");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply("Usage: !nade <id> <ms>. Example: !nade 0 1500");
            return;
        }

        if (int.TryParse(context.Args[0], out int id) && id >= 0 && id < wip.Grenades.Count)
        {
            if (int.TryParse(context.Args[1], out int ms))
            {
                if (ms < 0) ms = 0;
                wip.Grenades[id].TimeOffsetMs = ms;
                context.Reply($"[green][Defender][white] Grenade #{id} delay set to {wip.Grenades[id].TimeOffsetMs}ms");
            }
        }
        else
        {
            context.Reply("[red][Defender][white] Invalid grenade ID. Use !nades to list.");
        }
    }

    private void OnGiveAk(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;
        if (player.PlayerPawn?.ItemServices != null)
        {
            player.PlayerPawn.ItemServices.GiveItem<SwiftlyS2.Shared.SchemaDefinitions.CBasePlayerWeapon>("weapon_ak47");
            context.Reply("[green][Defender][white] Granted AK-47.");
        }
    }
}

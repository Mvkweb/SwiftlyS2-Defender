using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2_Defender.Interfaces;
using SwiftlyS2_Defender.Models;
using SwiftlyS2_Defender.Utils;
using System;
using System.Linq;

namespace SwiftlyS2_Defender.Services;

public sealed class MenuHudService : IMenuHudService
{
    private ISwiftlyCore? _core;
    private readonly IDefenderStateService _state;

    public MenuHudService(IDefenderStateService state)
    {
        _state = state;
    }

    public void Register(ISwiftlyCore core)
    {
        _core = core;
    }

    public void Unregister(ISwiftlyCore core)
    {
        _core = null;
    }

    public void StartCountdownAndPlay(IPlayer player, Scenario scenario, ulong? testingPlayerId, Action startCallback)
    {
        if (_core == null) return;

        _core.MenusAPI.CloseActiveMenu(player);

        ShowCountdownStep(player, 2, startCallback);
    }

    private void ShowCountdownStep(IPlayer player, int timeLeft, Action startCallback)
    {
        if (_core == null) return;

        // Freeze player using FL_FROZEN flag (1 << 6)
        if (player.PlayerPawn != null)
            player.PlayerPawn.Flags |= (uint)(1 << 6);

        if (timeLeft > 0)
        {
            // Set duration to 1000 milliseconds (1 second) so it properly stays on screen for the countdown
            player.SendCenterHTML($"<font class='fontSize-l'>{timeLeft}</font>", 1000);
            _core.Scheduler.DelayBySeconds(1f, () => ShowCountdownStep(player, timeLeft - 1, startCallback));
        }
        else
        {
            // Unfreeze
            if (player.PlayerPawn != null)
                player.PlayerPawn.Flags &= ~(uint)(1 << 6);

            player.SendCenterHTML(string.Empty, 1); // Clear it
            _core.Scheduler.DelayBySeconds(0.5f, () => 
            {
                startCallback();
            });
        }
    }

    public void ShowActiveScenarioHud(IPlayer player, Scenario scenario)
    {
        if (_core == null) return;

        _core.MenusAPI.CloseActiveMenu(player);

        UpdateHudLoop(player, scenario, -1);
    }

    private void UpdateHudLoop(IPlayer player, Scenario scenario, int lastAliveBots)
    {
        if (_core == null || !_state.IsPlaying) return;

        var players = _core.PlayerManager.GetAllPlayers();
        var aliveBots = players.Count(p => PlayerUtil.IsBot(p) && p.Controller != null && p.Controller.PawnIsAlive);

        if (aliveBots != lastAliveBots)
        {
            // Omit the duration argument to let it persist as long as the CS2 engine naturally allows for center HTML,
            // or pass a very large number in seconds/milliseconds. Let's try 999999 in case it's milliseconds.
            player.SendCenterHTML($"Arena: {scenario.Name} ({aliveBots}/{scenario.Bots.Count})", 999999);
        }

        _core.Scheduler.DelayBySeconds(0.1f, () => UpdateHudLoop(player, scenario, aliveBots));
    }

    public void CloseHud(IPlayer player)
    {
        if (_core == null) return;
        player.SendCenterHTML(string.Empty, 1);
    }
}

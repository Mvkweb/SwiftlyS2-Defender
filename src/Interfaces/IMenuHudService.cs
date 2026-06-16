using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Defender.Models;
using System;

namespace SwiftlyS2_Defender.Interfaces;

public interface IMenuHudService
{
    void Register(ISwiftlyCore core);
    void Unregister(ISwiftlyCore core);
    void StartCountdownAndPlay(IPlayer player, Scenario scenario, ulong? testingPlayerId, Action startCallback);
    void ShowActiveScenarioHud(IPlayer player, Scenario scenario);
    void CloseHud(IPlayer player);
}

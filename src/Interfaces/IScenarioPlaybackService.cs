using SwiftlyS2_Defender.Models;

namespace SwiftlyS2_Defender.Interfaces;

public interface IScenarioPlaybackService
{
    void PlayScenario(Scenario scenario, bool teleportHumans = true);
    void StopScenario();
    void ResetToStart(bool teleportHumans = true);
}

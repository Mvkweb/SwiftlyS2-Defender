using SwiftlyS2_Defender.Models;

namespace SwiftlyS2_Defender.Interfaces;

public interface IRecordingService
{
    void SetupScenario(string name);
    void SetPlayerAnchor(ulong steamId);
    void StartRecordingBot(ulong steamId);
    void StartRecordingGrenade(ulong steamId, string grenadeType);
    void StopRecording(ulong steamId);
    void ClearLastElement();
    void SaveScenario();
    Scenario? GetWipScenario();
}

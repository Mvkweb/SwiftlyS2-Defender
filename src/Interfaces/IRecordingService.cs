using SwiftlyS2_Defender.Models;

namespace SwiftlyS2_Defender.Interfaces;

public interface IRecordingService
{
    bool IsRecordingGrenade { get; }
    void SetupScenario(string name);
    void SetPlayerAnchor(ulong steamId);
    void StartRecordingBot(ulong steamId);
    void StartRecordingGrenade(ulong steamId, string grenadeType);
    void LogProjectileSpawned(string designerName, SwiftlyS2.Shared.Natives.Vector origin, SwiftlyS2.Shared.Natives.Vector velocity);
    void StopRecording(ulong steamId);
    void ClearLastElement();
    void SaveScenario();
    Scenario? GetWipScenario();
}

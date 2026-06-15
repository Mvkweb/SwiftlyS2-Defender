using SwiftlyS2_Defender.Models;

namespace SwiftlyS2_Defender.Interfaces;

public interface IRecordingService
{
    bool IsRecordingGrenade { get; }
    void SetupScenario(string name);
    void SetPlayerDefendAnchor(ulong steamId);
    void StartRecordingBot(ulong steamId);
    void StartRecordingGrenade(ulong steamId, string grenadeType);
    void LogProjectileSpawned(SwiftlyS2.Shared.SchemaDefinitions.CBaseCSGrenadeProjectile proj);
    void StopRecording(ulong steamId);
    void ClearLastElement();
    void SaveScenario();
    Scenario? GetWipScenario();
}

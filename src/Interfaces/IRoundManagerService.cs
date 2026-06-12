namespace SwiftlyS2_Defender.Interfaces;

public interface IRoundManagerService
{
    void HandlePlayerDeath(int victimSlot, int attackerSlot);
    void HandlePlayerHurt(int victimSlot, int attackerSlot, int damage);
    void ForceFail();
    void ForceSuccess();
}

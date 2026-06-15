using SwiftlyS2_Defender.Models;

namespace SwiftlyS2_Defender.Interfaces;

public interface IScenarioVisualizationService
{
    void DrawScenario(Scenario scenario);
    void ClearVisualizations();
    void DrawLiveTrajectorySegment(SwiftlyS2.Shared.Natives.Vector start, SwiftlyS2.Shared.Natives.Vector end, string grenadeType);
}

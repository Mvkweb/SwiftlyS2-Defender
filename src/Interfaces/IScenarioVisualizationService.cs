using SwiftlyS2_Defender.Models;

namespace SwiftlyS2_Defender.Interfaces;

public interface IScenarioVisualizationService
{
    void DrawScenario(Scenario scenario);
    void ClearVisualizations();
}

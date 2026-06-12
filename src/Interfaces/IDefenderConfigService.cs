using SwiftlyS2_Defender.Configuration;
using SwiftlyS2_Defender.Models;
using System.Collections.Generic;

namespace SwiftlyS2_Defender.Interfaces;

public interface IDefenderConfigService
{
    DefenderConfig Config { get; }
    void LoadOrCreate();
    Scenario? LoadScenario(string name);
    void SaveScenario(Scenario scenario);
    IEnumerable<string> GetAvailableScenarios();
}

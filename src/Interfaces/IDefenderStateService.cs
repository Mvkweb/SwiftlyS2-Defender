using SwiftlyS2_Defender.Models;

namespace SwiftlyS2_Defender.Interfaces;

public enum DefenderMode
{
    Smart,
    Practice,
    Competitive
}

public enum DefenderDifficulty
{
    Easy,
    Normal,
    Hard
}

public interface IDefenderStateService
{
    DefenderMode CurrentMode { get; set; }
    DefenderDifficulty CurrentDifficulty { get; set; }
    Scenario? ActiveScenario { get; set; }
    bool IsRecording { get; }
    bool IsPlaying { get; }
    
    void SetRecordingState(bool isRecording);
    void SetPlayingState(bool isPlaying);
}

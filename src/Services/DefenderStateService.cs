using SwiftlyS2_Defender.Interfaces;
using SwiftlyS2_Defender.Models;

namespace SwiftlyS2_Defender.Services;

public sealed class DefenderStateService : IDefenderStateService
{
    public DefenderMode CurrentMode { get; set; } = DefenderMode.Smart;
    public DefenderDifficulty CurrentDifficulty { get; set; } = DefenderDifficulty.Normal;
    public Scenario? ActiveScenario { get; set; }
    
    public bool IsRecording { get; private set; }
    public bool IsPlaying { get; private set; }

    public void SetRecordingState(bool isRecording)
    {
        IsRecording = isRecording;
    }

    public void SetPlayingState(bool isPlaying)
    {
        IsPlaying = isPlaying;
    }
}

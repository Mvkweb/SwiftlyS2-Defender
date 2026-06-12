using System.Collections.Generic;

namespace SwiftlyS2_Defender.Models;

public sealed class Scenario
{
    public string Name { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public ScenarioAnchor Anchor { get; set; } = new();
    public List<string> PlayerLoadout { get; set; } = new();
    public List<ScenarioBot> Bots { get; set; } = new();
    public List<ScenarioGrenade> Grenades { get; set; } = new();
}

public sealed class ScenarioAnchor
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
    public bool IsSet => X != 0 || Y != 0 || Z != 0;
}

public sealed class ScenarioBot
{
    public string Loadout { get; set; } = "ak47";
    public List<BotFrame> Frames { get; set; } = new();
}

public sealed class BotFrame
{
    public long TimeOffsetMs { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
}

public sealed class ScenarioGrenade
{
    public string GrenadeType { get; set; } = string.Empty;
    public long TimeOffsetMs { get; set; }
    public float OriginX { get; set; }
    public float OriginY { get; set; }
    public float OriginZ { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float VelocityZ { get; set; }
}

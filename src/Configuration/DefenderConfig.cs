namespace SwiftlyS2_Defender.Configuration;

public sealed class DefenderConfig
{
    public bool Enabled { get; set; } = true;
    public int CountdownTimer { get; set; } = 3;
    public string Prefix { get; set; } = "{Green}[Defender]{Default}";
    public string DataDirectory { get; set; } = "addons/swiftly/configs/plugins/Defender/Scenarios";
}

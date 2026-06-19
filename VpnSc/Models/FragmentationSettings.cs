namespace VpnSc.Models;

public sealed class FragmentationSettings
{
    public bool Enabled { get; set; }
    public string Packets { get; set; } = "tlshello";
    public string Length { get; set; } = "50-100";
    public string Interval { get; set; } = "10-20";
    public string MaxSplit { get; set; } = "3-6";

    public static FragmentationSettings Defaults => new()
    {
        Enabled = true,
        Packets = "tlshello",
        Length = "50-100",
        Interval = "10-20",
        MaxSplit = "3-6"
    };

    public FragmentationSettings CopyWith(bool? enabled = null) => new()
    {
        Enabled = enabled ?? Enabled,
        Packets = Packets,
        Length = Length,
        Interval = Interval,
        MaxSplit = MaxSplit
    };
}

public sealed class FragmentationPreset
{
    public string Name { get; }
    public FragmentationSettings Settings { get; }

    public FragmentationPreset(string name, FragmentationSettings settings)
    {
        Name = name;
        Settings = settings;
    }
}

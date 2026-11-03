using RfLab.Core.Presets;

namespace RfLab.Core.Tests;

public sealed class RfLabPresetTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "RfLabPresetTests-" + Guid.NewGuid().ToString("N"));

    private static readonly string[] OriginalFile =
    [
        "Name,AM_1",
        "Description,Ffix 10MHz -10dB",
        "Mode,1",
        "Frequency,147500",
        "Amplitude,40",
        "FMod,1000",
        "MGENMode,0",
        "Pattern,Message",
        "SI5351Fclk0,1000000",
    ];

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Read_KnownKeysAsNumbersAndAbsentKeysAsNull()
    {
        RfLabPreset preset = RfLabPreset.FromLines("AM_1", OriginalFile);

        Assert.Equal(1, preset.Mode);
        Assert.Equal(147_500, preset.Frequency);
        Assert.Equal(40, preset.Amplitude);
        Assert.Equal(1_000_000, preset.ClockFrequency0);
        Assert.Null(preset.ClockFrequency1);
        Assert.Null(preset.SweepMode);
        Assert.Equal("Ffix 10MHz -10dB", preset.Description);
    }

    [Fact]
    public void Write_KeepsUnknownKeysInTheirPlace()
    {
        RfLabPreset preset = RfLabPreset.FromLines("AM_1", OriginalFile);
        preset.Set(RfLabPreset.FrequencyKey, 10_000_000);
        preset.Set(RfLabPreset.SweepModeKey, "Ramp");

        string[] lines = [.. preset.ToLines()];

        Assert.Equal(
            [
                "Name,AM_1", "Description,Ffix 10MHz -10dB", "Mode,1", "Frequency,10000000",
                "Amplitude,40", "FMod,1000", "MGENMode,0", "Pattern,Message", "SI5351Fclk0,1000000",
                "SweepMode,Ramp",
            ],
            lines);
    }

    [Fact]
    public void Read_TheFileNameWinsOverAStaleNameLine()
    {
        RfLabPreset preset = RfLabPreset.FromLines("Copy of AM_1", OriginalFile);

        Assert.Equal("Name,Copy of AM_1", preset.ToLines().First());
    }

    [Fact]
    public void Description_CannotBreakTheLine()
    {
        var preset = new RfLabPreset("x") { Description = "two\r\nlines, one comma" };

        Assert.Equal("Description,two  lines, one comma", preset.ToLines().ElementAt(1));
        Assert.Equal("two  lines, one comma", RfLabPreset.FromLines("x", preset.ToLines()).Description);
    }

    [Fact]
    public void Store_SavesListsReadsRenamesAndDeletes()
    {
        var store = new RfLabPresetStore(directory) { UsesRecycleBin = false };
        RfLabPreset preset = RfLabPreset.FromLines("AM_1", OriginalFile);

        store.Save(preset);
        store.Save(new RfLabPreset("b-second"));

        Assert.Equal(["AM_1", "b-second"], store.ListNames());
        Assert.Equal(147_500, store.Read("AM_1")!.Frequency);

        store.Rename("AM_1", "AM one");
        Assert.Equal(["AM one", "b-second"], store.ListNames());
        Assert.Equal("Name,AM one", File.ReadLines(Path.Combine(directory, "AM one.txt")).First());
        Assert.Equal("Message", store.Read("AM one")!["Pattern"]);

        Assert.Throws<IOException>(() => store.Rename("AM one", "b-second"));

        store.Delete("b-second");
        Assert.Equal(["AM one"], store.ListNames());
    }

    [Fact]
    public void Store_RenameThatOnlyChangesCase()
    {
        var store = new RfLabPresetStore(directory) { UsesRecycleBin = false };
        store.Save(new RfLabPreset("sweep"));

        store.Rename("sweep", "Sweep");

        Assert.Equal(["Sweep"], store.ListNames());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" lead")]
    [InlineData("trail.")]
    [InlineData("a/b")]
    [InlineData("..\\escape")]
    [InlineData("comma,name")]
    public void Store_RefusesNamesThatAreNotPlainFileNames(string name)
    {
        var store = new RfLabPresetStore(directory) { UsesRecycleBin = false };

        Assert.NotNull(RfLabPresetStore.ValidateName(name));
        Assert.Null(store.Read(name));
        if (!string.IsNullOrWhiteSpace(name))
        {
            Assert.Throws<ArgumentException>(() => store.Save(new RfLabPreset("ok").WithName(name)));
        }
    }

    [Fact]
    public void Store_SeedsTheExamplesOnlyIntoAMissingFolder()
    {
        string examples = Path.Combine(directory, "examples");
        Directory.CreateDirectory(examples);
        File.WriteAllLines(Path.Combine(examples, "10MHz -20dB.txt"), ["Name,10MHz -20dB"]);
        var store = new RfLabPresetStore(Path.Combine(directory, "presets")) { UsesRecycleBin = false };

        Assert.True(store.SeedIfMissing(examples));
        store.Delete("10MHz -20dB");
        Assert.False(store.SeedIfMissing(examples));

        Assert.Empty(store.ListNames());
    }

    [Fact]
    public void Store_ListsNothingForAMissingFolder()
    {
        Assert.Empty(new RfLabPresetStore(Path.Combine(directory, "none")).ListNames());
    }
}

internal static class RfLabPresetTestExtensions
{
    /// <summary>A preset carrying a name the constructor would refuse, to reach the store's own check.</summary>
    public static RfLabPreset WithName(this RfLabPreset preset, string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            preset.Rename(name);
        }

        return preset;
    }
}

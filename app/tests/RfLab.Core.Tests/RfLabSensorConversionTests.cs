namespace RfLab.Core.Tests;

public sealed class RfLabSensorConversionTests
{
    [Fact]
    public void MillivoltsFromAdcValue_UsesTwoPointFiveMillivoltsPerCount()
    {
        Assert.Equal(0.0, RfLabSensorConversion.MillivoltsFromAdcValue(0));
        Assert.Equal(2.5, RfLabSensorConversion.MillivoltsFromAdcValue(1));
        Assert.Equal(2500.0, RfLabSensorConversion.MillivoltsFromAdcValue(1000));
        Assert.Equal(2557.5, RfLabSensorConversion.MillivoltsFromAdcValue(1023));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1024)]
    public void MillivoltsFromAdcValue_RejectsValuesBeyondTenBits(int adcValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RfLabSensorConversion.MillivoltsFromAdcValue(adcValue));
    }

    [Fact]
    public void LevelFromMillivolts_ReportsTheFloorBelowTwoHundredMillivolts()
    {
        Assert.Equal(-70.0, RfLabSensorConversion.LevelFromMillivolts(0.0));
        Assert.Equal(-70.0, RfLabSensorConversion.LevelFromMillivolts(199.9));
    }

    [Fact]
    public void LevelFromMillivolts_MatchesTheCharacterizedDetectorTransfer()
    {
        // -(2235 - 2500) / 25.917 - 0.5
        Assert.Equal(
            9.725,
            RfLabSensorConversion.LevelFromMillivolts(2500.0),
            precision: 3);

        // -(2235 - 500) / 25.917 - 0.5
        Assert.Equal(
            -67.444,
            RfLabSensorConversion.LevelFromMillivolts(500.0),
            precision: 3);
    }
}

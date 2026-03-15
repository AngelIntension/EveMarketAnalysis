using EveMarketAnalysisClient.Models;
using FluentAssertions;

namespace EveMarketAnalysisClient.Tests.Unit;

public class TradeHubRegionTests
{
    [Fact]
    public void All_ReturnsExactlyFiveRegions()
    {
        TradeHubRegion.All.Should().HaveCount(5);
    }

    [Fact]
    public void Default_ReturnsTheForge()
    {
        TradeHubRegion.Default.RegionId.Should().Be(10000002);
        TradeHubRegion.Default.RegionName.Should().Be("The Forge");
        TradeHubRegion.Default.HubName.Should().Be("Jita");
    }

    [Theory]
    [InlineData(10000002, "The Forge", "Jita")]
    [InlineData(10000043, "Domain", "Amarr")]
    [InlineData(10000032, "Sinq Laison", "Dodixie")]
    [InlineData(10000042, "Metropolis", "Hek")]
    [InlineData(10000030, "Heimatar", "Rens")]
    public void All_ContainsExpectedRegions(int expectedId, string expectedName, string expectedHub)
    {
        TradeHubRegion.All.Should().Contain(r =>
            r.RegionId == expectedId &&
            r.RegionName == expectedName &&
            r.HubName == expectedHub);
    }

    [Fact]
    public void IsDefault_TrueOnlyForTheForge()
    {
        TradeHubRegion.All.Should().ContainSingle(r => r.IsDefault);
        TradeHubRegion.All.Single(r => r.IsDefault).RegionId.Should().Be(10000002);
    }
}

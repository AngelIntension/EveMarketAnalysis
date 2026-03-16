using EveMarketAnalysisClient.Models;
using FluentAssertions;

namespace EveMarketAnalysisClient.Tests.Unit;

public class PortfolioConfigurationTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new PortfolioConfiguration();

        config.ProcurementStationId.Should().Be(60003760);
        config.ProcurementRegionId.Should().Be(10000002);
        config.SellingHubStationId.Should().Be(60003760);
        config.SellingHubRegionId.Should().Be(10000002);
        config.ManufacturingSystemId.Should().Be(30000142);
        config.BuyingBrokerFeePercent.Should().Be(3.0m);
        config.SellingBrokerFeePercent.Should().Be(3.0m);
        config.SalesTaxPercent.Should().Be(3.6m);
        config.MinIskPerHour.Should().Be(25_000_000m);
        config.DailyIncomeGoal.Should().Be(750_000_000m);
        config.ManufacturingSlots.Should().Be(11);
        config.WhatIfME.Should().BeNull();
        config.WhatIfTE.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(50)]
    public void ManufacturingSlots_ValidRange(int slots)
    {
        var config = new PortfolioConfiguration(ManufacturingSlots: slots);
        config.ManufacturingSlots.Should().Be(slots);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void BrokerFee_ValidRange(decimal fee)
    {
        var config = new PortfolioConfiguration(BuyingBrokerFeePercent: fee, SellingBrokerFeePercent: fee);
        config.BuyingBrokerFeePercent.Should().Be(fee);
        config.SellingBrokerFeePercent.Should().Be(fee);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void WhatIfME_ValidRange(int me)
    {
        var config = new PortfolioConfiguration(WhatIfME: me);
        config.WhatIfME.Should().Be(me);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(20)]
    public void WhatIfTE_ValidRange(int te)
    {
        var config = new PortfolioConfiguration(WhatIfTE: te);
        config.WhatIfTE.Should().Be(te);
    }
}

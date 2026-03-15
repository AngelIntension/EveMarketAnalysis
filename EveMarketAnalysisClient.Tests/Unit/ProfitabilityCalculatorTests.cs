using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace EveMarketAnalysisClient.Tests.Unit;

public class ProfitabilityCalculatorTests
{
    private static ProfitabilityCalculator CreateCalculator(
        Mock<IBlueprintDataService>? blueprintData = null,
        Mock<IEsiMarketClient>? marketClient = null)
    {
        blueprintData ??= new Mock<IBlueprintDataService>();
        marketClient ??= new Mock<IEsiMarketClient>();
        var apiClient = new Mock<ApiClient>(
            new Mock<Microsoft.Kiota.Abstractions.IRequestAdapter>().Object);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<ProfitabilityCalculator>>();

        return new ProfitabilityCalculator(
            blueprintData.Object,
            marketClient.Object,
            apiClient.Object,
            cache,
            logger.Object);
    }

    private static CharacterBlueprint CreateBlueprint(
        int typeId = 587, int me = 0, int te = 0, bool isCopy = false)
    {
        return new CharacterBlueprint(
            ItemId: 1,
            TypeId: typeId,
            TypeName: "Test Blueprint",
            MaterialEfficiency: me,
            TimeEfficiency: te,
            Runs: isCopy ? 10 : -1,
            IsCopy: isCopy);
    }

    private static BlueprintActivity CreateActivity(
        int blueprintTypeId = 587,
        int producedTypeId = 587,
        int baseTime = 3600,
        params (int typeId, int quantity)[] materials)
    {
        var mats = materials.Length > 0
            ? materials.Select(m => new MaterialRequirement(m.typeId, "", m.quantity, 0)).ToImmutableArray()
            : ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0));

        return new BlueprintActivity(
            BlueprintTypeId: blueprintTypeId,
            ProducedTypeId: producedTypeId,
            ProducedQuantity: 1,
            BaseTime: baseTime,
            Materials: mats);
    }

    private static void SetupMarketSnapshot(
        Mock<IEsiMarketClient> marketClient,
        int typeId,
        decimal? sellPrice,
        decimal? buyPrice,
        double volume = 100.0)
    {
        marketClient.Setup(m => m.GetMarketSnapshotAsync(
                It.IsAny<int>(), typeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketSnapshot(
                typeId, 10000002, sellPrice, buyPrice, volume, DateTimeOffset.UtcNow));
    }

    // === T023: ME Formula Unit Tests ===

    [Theory]
    [InlineData(0, 100, 100)]   // ME=0 returns base quantity
    [InlineData(10, 100, 90)]   // ME=10: max(1, ceil(100*0.90)) = 90
    [InlineData(5, 100, 95)]    // ME=5: max(1, ceil(100*0.95)) = 95
    [InlineData(5, 2, 2)]       // ME=5: max(1, ceil(2*0.95)) = max(1, ceil(1.9)) = 2
    [InlineData(10, 1, 1)]      // ME=10: max(1, ceil(1*0.90)) = max(1, 1) = 1 (max guarantee)
    [InlineData(10, 10, 9)]     // ME=10: max(1, ceil(10*0.90)) = 9
    [InlineData(1, 100, 99)]    // ME=1: max(1, ceil(100*0.99)) = 99
    public async Task ME_Formula_CalculatesCorrectAdjustedQuantity(
        int me, int baseQuantity, int expectedAdjusted)
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint(me: me);
        var activity = CreateActivity(materials: (34, baseQuantity));

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, 5.0m, null);
        SetupMarketSnapshot(marketClient, 587, 1000.0m, 900.0m);

        var calculator = CreateCalculator(blueprintData, marketClient);

        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp),
            new ProfitabilitySettings());

        results.Should().NotBeEmpty();
        results[0].Materials[0].AdjustedQuantity.Should().Be(expectedAdjusted);
    }

    // === T024: TE Formula Unit Tests ===

    [Theory]
    [InlineData(0, 3600, 3600)]     // TE=0 returns base time
    [InlineData(20, 3600, 2880)]    // TE=20: 3600 * 0.80 = 2880
    [InlineData(10, 3600, 3240)]    // TE=10: 3600 * 0.90 = 3240
    [InlineData(20, 10800, 8640)]   // TE=20: 10800 * 0.80 = 8640
    public async Task TE_Formula_CalculatesCorrectProductionTime(
        int te, int baseTime, int expectedTime)
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint(te: te);
        var activity = CreateActivity(baseTime: baseTime, materials: (34, 100));

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, 5.0m, null);
        SetupMarketSnapshot(marketClient, 587, 1000.0m, 900.0m);

        var calculator = CreateCalculator(blueprintData, marketClient);

        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp),
            new ProfitabilitySettings());

        results.Should().NotBeEmpty();
        results[0].ProductionTimeSeconds.Should().Be(expectedTime);
    }

    // === T025: Profit Computation Unit Tests ===

    [Fact]
    public async Task Profit_GrossProfit_IsSellMinusCostMinusTaxMinusFee()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint(me: 0, te: 0);
        var activity = CreateActivity(materials: (34, 100));

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, 10.0m, null);    // material cost: 100 * 10 = 1000
        SetupMarketSnapshot(marketClient, 587, 5000.0m, 4000.0m); // sell value: 4000 (highest buy)

        var calculator = CreateCalculator(blueprintData, marketClient);

        var settings = new ProfitabilitySettings(TaxRate: 0.08m, InstallationFeeRate: 0.01m);
        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp), settings);

        var r = results[0];
        r.TotalMaterialCost.Should().Be(1000m);
        r.ProductSellValue.Should().Be(4000m);
        r.TaxAmount.Should().Be(320m);      // 4000 * 0.08
        r.InstallationFee.Should().Be(40m); // 4000 * 0.01
        // gross = 4000 - 1000 - 320 - 40 = 2640
        r.GrossProfit.Should().Be(2640m);
    }

    [Fact]
    public async Task Profit_Margin_IsGrossDividedByMaterialCostTimesHundred()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint(me: 0, te: 0);
        var activity = CreateActivity(materials: (34, 100));

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, 10.0m, null);
        SetupMarketSnapshot(marketClient, 587, 5000.0m, 4000.0m);

        var calculator = CreateCalculator(blueprintData, marketClient);
        var settings = new ProfitabilitySettings(TaxRate: 0.08m, InstallationFeeRate: 0.01m);

        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp), settings);

        // margin = (2640 / 1000) * 100 = 264%
        results[0].ProfitMarginPercent.Should().BeApproximately(264.0, 0.1);
    }

    [Fact]
    public async Task Profit_IskPerHour_IsGrossDividedByHours()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint(me: 0, te: 0);
        var activity = CreateActivity(baseTime: 3600, materials: (34, 100));

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, 10.0m, null);
        SetupMarketSnapshot(marketClient, 587, 5000.0m, 4000.0m);

        var calculator = CreateCalculator(blueprintData, marketClient);
        var settings = new ProfitabilitySettings(TaxRate: 0.08m, InstallationFeeRate: 0.01m);

        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp), settings);

        // ISK/hr = 2640 / (3600/3600) = 2640
        results[0].IskPerHour.Should().BeApproximately(2640.0, 0.1);
    }

    [Fact]
    public async Task Profit_WithZeroMaterialCost_ReturnsZeroMargin()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint(me: 0, te: 0);
        var activity = CreateActivity(materials: (34, 100));

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, null, null);        // no material price
        SetupMarketSnapshot(marketClient, 587, 1000.0m, 900.0m); // has product price

        var calculator = CreateCalculator(blueprintData, marketClient);

        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp),
            new ProfitabilitySettings());

        results[0].TotalMaterialCost.Should().Be(0);
        results[0].ProfitMarginPercent.Should().Be(0);
    }

    [Fact]
    public async Task Profit_NegativeProfit_IsCalculatedCorrectly()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint(me: 0, te: 0);
        var activity = CreateActivity(materials: (34, 1000));

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, 100.0m, null);      // material cost: 100000
        SetupMarketSnapshot(marketClient, 587, 1000.0m, 900.0m);  // sell value: 900

        var calculator = CreateCalculator(blueprintData, marketClient);

        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp),
            new ProfitabilitySettings());

        results[0].GrossProfit.Should().BeNegative();
    }

    // === T026: Sell Value Determination Tests ===

    [Fact]
    public async Task SellValue_UsesHighestBuyPrice_WhenAvailable()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint();
        var activity = CreateActivity(materials: (34, 10));

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, 5.0m, null);
        SetupMarketSnapshot(marketClient, 587, 1000.0m, 800.0m); // both sell and buy

        var calculator = CreateCalculator(blueprintData, marketClient);

        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp),
            new ProfitabilitySettings());

        results[0].ProductSellValue.Should().Be(800.0m); // uses highest buy
    }

    [Fact]
    public async Task SellValue_FallsBackToLowestSell_WhenNoBuyOrders()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint();
        var activity = CreateActivity(materials: (34, 10));

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, 5.0m, null);
        SetupMarketSnapshot(marketClient, 587, 1000.0m, null); // sell only, no buy

        var calculator = CreateCalculator(blueprintData, marketClient);

        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp),
            new ProfitabilitySettings());

        results[0].ProductSellValue.Should().Be(1000.0m); // uses lowest sell
    }

    [Fact]
    public async Task SellValue_ReturnsZero_WhenNoMarketData()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint();
        var activity = CreateActivity(materials: (34, 10));

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, 5.0m, null);
        SetupMarketSnapshot(marketClient, 587, null, null); // no market data

        var calculator = CreateCalculator(blueprintData, marketClient);

        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp),
            new ProfitabilitySettings());

        results[0].ProductSellValue.Should().Be(0);
        results[0].HasMarketData.Should().BeFalse();
    }

    // === T041: Calculator Error Handling Tests ===

    [Fact]
    public async Task Calculator_NoSdeMatch_SetsErrorMessage()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        blueprintData.Setup(b => b.GetBlueprintActivity(It.IsAny<int>()))
            .Returns((BlueprintActivity?)null);

        var calculator = CreateCalculator(blueprintData);

        var bp = CreateBlueprint(typeId: 99999);
        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp),
            new ProfitabilitySettings());

        results.Should().HaveCount(1);
        results[0].HasMarketData.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("No manufacturing data");
    }

    [Fact]
    public async Task Calculator_NoMarketOrders_SetsHasMarketDataFalse()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint();
        var activity = CreateActivity(materials: (34, 100));

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, 5.0m, null);
        SetupMarketSnapshot(marketClient, 587, null, null); // no product market data

        var calculator = CreateCalculator(blueprintData, marketClient);

        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp),
            new ProfitabilitySettings());

        results.Should().ContainSingle(r => !r.HasMarketData);
    }

    [Fact]
    public async Task Calculator_PartialMaterialFailure_SetsErrorMessage()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();

        var bp = CreateBlueprint();
        var activity = CreateActivity(materials: new[] { (34, 100), (35, 50) });

        blueprintData.Setup(b => b.GetBlueprintActivity(587)).Returns(activity);
        SetupMarketSnapshot(marketClient, 34, 5.0m, null);     // has price
        SetupMarketSnapshot(marketClient, 35, null, null);       // no price
        SetupMarketSnapshot(marketClient, 587, 1000.0m, 900.0m);

        var calculator = CreateCalculator(blueprintData, marketClient);

        var results = await calculator.CalculateAsync(
            ImmutableArray.Create(bp),
            new ProfitabilitySettings());

        results[0].ErrorMessage.Should().Contain("material prices unavailable");
    }
}

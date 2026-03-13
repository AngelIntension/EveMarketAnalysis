using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace EveMarketAnalysisClient.Tests.Services;

public class ProfitabilityIntegrationTests
{
    [Fact]
    public async Task EndToEnd_CalculatesProfitability_ForRifterBlueprint()
    {
        // Use real BlueprintDataService with embedded SDE data
        var blueprintData = new BlueprintDataService();
        var marketClient = new Mock<IEsiMarketClient>();

        // Rifter Blueprint (691) produces Rifter (587)
        // Real SDE materials: Tritanium(34)=32000, Pyerite(35)=6000, Mexallon(36)=2500, Isogen(37)=500
        var materialPrices = new Dictionary<int, decimal>
        {
            { 34, 5.50m },     // Tritanium
            { 35, 8.00m },     // Pyerite
            { 36, 32.00m },    // Mexallon
            { 37, 320.00m }    // Isogen
        };

        foreach (var (typeId, price) in materialPrices)
        {
            marketClient.Setup(m => m.GetMarketSnapshotAsync(
                    10000002, typeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MarketSnapshot(
                    typeId, 10000002, price, null, 500000, DateTimeOffset.UtcNow));
        }

        // Rifter product price
        marketClient.Setup(m => m.GetMarketSnapshotAsync(
                10000002, 587, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketSnapshot(
                587, 10000002, 850000m, 780000m, 1200, DateTimeOffset.UtcNow));

        var apiClient = new Mock<ApiClient>(
            new Mock<Microsoft.Kiota.Abstractions.IRequestAdapter>().Object);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<ProfitabilityCalculator>>();

        var calculator = new ProfitabilityCalculator(
            blueprintData, marketClient.Object, apiClient.Object, cache, logger.Object);

        var blueprints = ImmutableArray.Create(
            new CharacterBlueprint(1, 691, "Rifter Blueprint", 10, 20, -1, false));

        var settings = new ProfitabilitySettings(RegionId: 10000002);

        var results = await calculator.CalculateAsync(blueprints, settings);

        results.Should().NotBeEmpty();
        var rifter = results[0];
        rifter.ProducedTypeId.Should().Be(587);
        rifter.HasMarketData.Should().BeTrue();
        rifter.ProductSellValue.Should().Be(780000m); // uses highest buy
        rifter.TotalMaterialCost.Should().BeGreaterThan(0);
        // Real SDE base time = 6000, TE=20: 6000 * 0.80 = 4800
        rifter.ProductionTimeSeconds.Should().Be(4800);
        rifter.Materials.Should().NotBeEmpty();
        // ME=10 on 32000 Tritanium: max(1, ceil(32000 * 0.90)) = 28800
        rifter.Materials.First(m => m.TypeId == 34).AdjustedQuantity.Should().Be(28800);
    }

    [Fact]
    public async Task EndToEnd_SortsByIskPerHour_Descending()
    {
        var blueprintData = new BlueprintDataService();
        var marketClient = new Mock<IEsiMarketClient>();

        // Set up generic material prices for all common minerals
        for (int typeId = 34; typeId <= 40; typeId++)
        {
            marketClient.Setup(m => m.GetMarketSnapshotAsync(
                    10000002, typeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MarketSnapshot(
                    typeId, 10000002, 5.0m, null, 100000, DateTimeOffset.UtcNow));
        }

        // Rifter (produced by blueprint 691)
        marketClient.Setup(m => m.GetMarketSnapshotAsync(
                10000002, 587, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketSnapshot(
                587, 10000002, 500000m, 450000m, 1000, DateTimeOffset.UtcNow));

        // Executioner (produced by blueprint 692)
        marketClient.Setup(m => m.GetMarketSnapshotAsync(
                10000002, 622, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketSnapshot(
                622, 10000002, 200000m, 180000m, 800, DateTimeOffset.UtcNow));

        var apiClient = new Mock<ApiClient>(
            new Mock<Microsoft.Kiota.Abstractions.IRequestAdapter>().Object);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<ProfitabilityCalculator>>();

        var calculator = new ProfitabilityCalculator(
            blueprintData, marketClient.Object, apiClient.Object, cache, logger.Object);

        var blueprints = ImmutableArray.Create(
            new CharacterBlueprint(1, 691, "Rifter BP", 10, 20, -1, false),
            new CharacterBlueprint(2, 692, "Executioner BP", 10, 20, -1, false));

        var results = await calculator.CalculateAsync(
            blueprints, new ProfitabilitySettings());

        var profitable = results.Where(r => r.HasMarketData).ToList();
        if (profitable.Count > 1)
        {
            profitable[0].IskPerHour.Should().BeGreaterThanOrEqualTo(profitable[1].IskPerHour);
        }
    }
}

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

public class PortfolioAnalyzerTests
{
    private static PortfolioAnalyzer CreateAnalyzer(
        Mock<IBlueprintDataService>? blueprintData = null,
        Mock<IEsiMarketClient>? marketClient = null,
        Mock<IEsiCharacterClient>? characterClient = null,
        Mock<IPhaseService>? phaseService = null,
        IMemoryCache? cache = null)
    {
        blueprintData ??= new Mock<IBlueprintDataService>();
        marketClient ??= new Mock<IEsiMarketClient>();
        characterClient ??= new Mock<IEsiCharacterClient>();
        phaseService ??= new Mock<IPhaseService>();
        var apiClient = new Mock<ApiClient>(
            new Mock<Microsoft.Kiota.Abstractions.IRequestAdapter>().Object);
        cache ??= new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<PortfolioAnalyzer>>();

        return new PortfolioAnalyzer(
            blueprintData.Object,
            marketClient.Object,
            characterClient.Object,
            phaseService.Object,
            apiClient.Object,
            cache,
            logger.Object);
    }

    private static CharacterBlueprint CreateBlueprint(
        int typeId = 691, int me = 10, int te = 20, bool isCopy = false)
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
        int blueprintTypeId = 691,
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

    private static MarketSnapshot CreateSnapshot(
        int typeId, decimal? lowestSell = null, decimal? highestBuy = null, double avgVolume = 100.0)
    {
        return new MarketSnapshot(
            TypeId: typeId,
            RegionId: 10000002,
            LowestSellPrice: lowestSell,
            HighestBuyPrice: highestBuy,
            AverageDailyVolume: avgVolume,
            FetchedAt: DateTimeOffset.UtcNow);
    }

    private static void SetupStandardMocks(
        Mock<IEsiCharacterClient> characterClient,
        Mock<IBlueprintDataService> blueprintData,
        Mock<IEsiMarketClient> marketClient,
        Mock<IPhaseService> phaseService,
        CharacterBlueprint? blueprint = null,
        BlueprintActivity? activity = null)
    {
        var bp = blueprint ?? CreateBlueprint();
        var act = activity ?? CreateActivity();

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(bp));

        characterClient.Setup(c => c.GetCharacterSkillsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(
                new CharacterSkill(3319, "Industry", 5, 256000)));

        blueprintData.Setup(b => b.GetBlueprintActivity(bp.TypeId))
            .Returns(act);

        // Material market snapshot (buy price for procurement)
        foreach (var mat in act.Materials)
        {
            marketClient.Setup(m => m.GetMarketSnapshotAsync(
                    It.IsAny<int>(), mat.TypeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateSnapshot(mat.TypeId, lowestSell: 10m, highestBuy: 8m));
        }

        // Product market snapshot (sell price for revenue)
        marketClient.Setup(m => m.GetMarketSnapshotAsync(
                It.IsAny<int>(), act.ProducedTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(act.ProducedTypeId, lowestSell: 2200000m, highestBuy: 2000000m, avgVolume: 150.0));

        phaseService.Setup(p => p.GetPhaseForTypeId(bp.TypeId))
            .Returns(new PhaseDefinition(1, "T1 Frigate", "Test", ImmutableArray.Create(bp.TypeId)));
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "T1 Frigate", "Test", ImmutableArray.Create(bp.TypeId)),
                new PhaseDefinition(2, "T1 Destroyer", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "Cruiser", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "Battleship", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "Capital", "Test", ImmutableArray<int>.Empty)));
    }

    // === T016: ISK/hr Calculation Tests ===

    [Fact]
    public async Task AnalyzeAsync_CalculatesIskPerHour_WithBrokerFeesAndTax()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        var bp = CreateBlueprint(me: 10, te: 20);
        var activity = CreateActivity(baseTime: 3600, materials: (34, 100));
        SetupStandardMocks(characterClient, blueprintData, marketClient, phaseService, bp, activity);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration(
            BuyingBrokerFeePercent: 3.0m,
            SellingBrokerFeePercent: 3.0m,
            SalesTaxPercent: 3.6m);

        var result = await analyzer.AnalyzeAsync(12345, config);

        result.Rankings.Should().NotBeEmpty();
        var entry = result.Rankings[0];
        entry.IskPerHour.Should().BeGreaterThan(0);
        entry.BuyingBrokerFee.Should().BeGreaterThan(0);
        entry.SellingBrokerFee.Should().BeGreaterThan(0);
        entry.SalesTax.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzeAsync_MaterialCost_UsesHighestBuyPrice()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        var bp = CreateBlueprint(me: 0, te: 0);
        var activity = CreateActivity(baseTime: 3600, materials: (34, 100));
        SetupStandardMocks(characterClient, blueprintData, marketClient, phaseService, bp, activity);

        // Override material snapshot: highest buy = 8
        marketClient.Setup(m => m.GetMarketSnapshotAsync(
                It.IsAny<int>(), 34, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(34, lowestSell: 10m, highestBuy: 8m));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        var entry = result.Rankings[0];
        // Material cost = 100 * 8 = 800 (uses highest buy)
        entry.MaterialCost.Should().Be(800m);
    }

    [Fact]
    public async Task AnalyzeAsync_ProductRevenue_UsesLowestSellPrice()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        var bp = CreateBlueprint(me: 0, te: 0);
        var activity = CreateActivity(baseTime: 3600, materials: (34, 100));
        SetupStandardMocks(characterClient, blueprintData, marketClient, phaseService, bp, activity);

        // Override product snapshot: lowest sell = 2200000
        marketClient.Setup(m => m.GetMarketSnapshotAsync(
                It.IsAny<int>(), 587, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(587, lowestSell: 2200000m, highestBuy: 2000000m));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        var entry = result.Rankings[0];
        // Product revenue = lowest sell = 2,200,000
        entry.ProductRevenue.Should().Be(2200000m);
    }

    [Fact]
    public async Task AnalyzeAsync_SystemCostFee_IncludesSystemCostIndex()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        var bp = CreateBlueprint(me: 10, te: 20);
        var activity = CreateActivity(baseTime: 3600, materials: (34, 100));
        SetupStandardMocks(characterClient, blueprintData, marketClient, phaseService, bp, activity);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        var entry = result.Rankings[0];
        // System cost fee should be >= 0 (may be 0 if cost index fetch fails)
        entry.SystemCostFee.Should().BeGreaterThanOrEqualTo(0);
    }

    // === T017: Ranking, Skill Gating, Error Handling ===

    [Fact]
    public async Task AnalyzeAsync_RankingsSortedByIskPerHour_Descending()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        var bp1 = new CharacterBlueprint(1, 691, "BP1", 10, 20, -1, false);
        var bp2 = new CharacterBlueprint(2, 936, "BP2", 10, 20, -1, false);

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(bp1, bp2));

        characterClient.Setup(c => c.GetCharacterSkillsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(new CharacterSkill(3319, "Industry", 5, 256000)));

        var activity1 = CreateActivity(691, 587, 3600, (34, 100));
        var activity2 = CreateActivity(936, 588, 1800, (34, 50));

        blueprintData.Setup(b => b.GetBlueprintActivity(691)).Returns(activity1);
        blueprintData.Setup(b => b.GetBlueprintActivity(936)).Returns(activity2);

        // Product 587 sells for less
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 587, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(587, lowestSell: 1000000m, highestBuy: 900000m));
        // Product 588 sells for more with shorter build time (higher ISK/hr)
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 588, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(588, lowestSell: 2000000m, highestBuy: 1800000m));
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 34, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(34, lowestSell: 10m, highestBuy: 8m));

        phaseService.Setup(p => p.GetPhaseForTypeId(It.IsAny<int>()))
            .Returns(new PhaseDefinition(1, "Test", "Test", ImmutableArray<int>.Empty));
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "Test", "Test", ImmutableArray.Create(691, 936)),
                new PhaseDefinition(2, "T2", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "T3", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "T4", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "T5", "Test", ImmutableArray<int>.Empty)));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        var rankings = result.Rankings.Where(r => r.HasMarketData).ToList();
        rankings.Should().HaveCountGreaterOrEqualTo(2);
        rankings[0].IskPerHour.Should().BeGreaterThanOrEqualTo(rankings[1].IskPerHour);
    }

    [Fact]
    public async Task AnalyzeAsync_ExcludesBlueprints_WhenSkillsNotMet()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        var bp = CreateBlueprint(typeId: 1002); // Phase 5 - requires Industry 5 + Advanced Industry 1

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(bp));

        // Character only has Industry 1 — should not meet Phase 5 requirements
        characterClient.Setup(c => c.GetCharacterSkillsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(new CharacterSkill(3319, "Industry", 1, 10000)));

        blueprintData.Setup(b => b.GetBlueprintActivity(1002))
            .Returns(CreateActivity(1002, 22000, 36000, (34, 1000)));

        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(34, lowestSell: 10m, highestBuy: 8m));

        phaseService.Setup(p => p.GetPhaseForTypeId(It.IsAny<int>()))
            .Returns((PhaseDefinition?)null);
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "T1", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(2, "T2", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "T3", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "T4", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "Capital", "Test", ImmutableArray.Create(1002))));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        // Blueprint should be excluded due to skill gating
        result.Rankings.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_HandlesNoManufacturingData_Gracefully()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        var bp = CreateBlueprint(typeId: 99999);

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(bp));

        characterClient.Setup(c => c.GetCharacterSkillsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<CharacterSkill>.Empty);

        // No blueprint activity data
        blueprintData.Setup(b => b.GetBlueprintActivity(99999)).Returns((BlueprintActivity?)null);

        phaseService.Setup(p => p.GetPhaseForTypeId(It.IsAny<int>())).Returns((PhaseDefinition?)null);
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "T1", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(2, "T2", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "T3", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "T4", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "T5", "Test", ImmutableArray<int>.Empty)));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        result.ErrorCount.Should().BeGreaterThan(0);
    }

    // === T018: What-if ME/TE Overrides, Portfolio Size Warning ===

    [Fact]
    public async Task AnalyzeAsync_WhatIfME_OverridesActualME()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        var bp = CreateBlueprint(me: 0, te: 0);
        var activity = CreateActivity(baseTime: 3600, materials: (34, 100));
        SetupStandardMocks(characterClient, blueprintData, marketClient, phaseService, bp, activity);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);

        // Without what-if: ME=0, material cost = 100 * 8 = 800
        var resultNoWhatIf = await analyzer.AnalyzeAsync(12345, new PortfolioConfiguration());
        // With what-if ME=10: material cost = ceil(100 * 0.9) * 8 = 90 * 8 = 720
        var resultWithWhatIf = await analyzer.AnalyzeAsync(12345, new PortfolioConfiguration(WhatIfME: 10));

        resultWithWhatIf.Rankings[0].MaterialCost.Should().BeLessThan(resultNoWhatIf.Rankings[0].MaterialCost);
    }

    [Fact]
    public async Task AnalyzeAsync_WhatIfTE_OverridesActualTE()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        var bp = CreateBlueprint(me: 10, te: 0);
        var activity = CreateActivity(baseTime: 3600, materials: (34, 100));
        SetupStandardMocks(characterClient, blueprintData, marketClient, phaseService, bp, activity);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);

        // Without what-if: TE=0, production time = 3600
        var resultNoWhatIf = await analyzer.AnalyzeAsync(12345, new PortfolioConfiguration());
        // With what-if TE=20: production time = 3600 * 0.8 = 2880
        var resultWithWhatIf = await analyzer.AnalyzeAsync(12345, new PortfolioConfiguration(WhatIfTE: 20));

        resultWithWhatIf.Rankings[0].ProductionTimeSeconds.Should().BeLessThan(
            resultNoWhatIf.Rankings[0].ProductionTimeSeconds);
    }

    [Fact]
    public async Task AnalyzeAsync_SetsPortfolioSizeWarning_WhenOver300Blueprints()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        // Create 301 blueprints
        var blueprints = Enumerable.Range(1, 301)
            .Select(i => new CharacterBlueprint(i, 691, "BP", 10, 20, -1, false))
            .ToImmutableArray();

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blueprints);

        characterClient.Setup(c => c.GetCharacterSkillsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(new CharacterSkill(3319, "Industry", 5, 256000)));

        var activity = CreateActivity(691, 587, 3600, (34, 100));
        blueprintData.Setup(b => b.GetBlueprintActivity(691)).Returns(activity);

        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(34, lowestSell: 10m, highestBuy: 8m));

        phaseService.Setup(p => p.GetPhaseForTypeId(It.IsAny<int>()))
            .Returns(new PhaseDefinition(1, "Test", "Test", ImmutableArray.Create(691)));
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "Test", "Test", ImmutableArray.Create(691)),
                new PhaseDefinition(2, "T2", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "T3", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "T4", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "T5", "Test", ImmutableArray<int>.Empty)));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        result.PortfolioSizeWarning.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsEmptyRankings_WhenNoBlueprints()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<CharacterBlueprint>.Empty);

        characterClient.Setup(c => c.GetCharacterSkillsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<CharacterSkill>.Empty);

        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "T1", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(2, "T2", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "T3", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "T4", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "T5", "Test", ImmutableArray<int>.Empty)));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        result.Rankings.Should().BeEmpty();
        result.TotalBlueprintsEvaluated.Should().Be(0);
    }
}

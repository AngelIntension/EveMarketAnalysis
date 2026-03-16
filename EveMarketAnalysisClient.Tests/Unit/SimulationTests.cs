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

public class SimulationTests
{
    private static PortfolioAnalyzer CreateAnalyzer(
        Mock<IBlueprintDataService>? blueprintData = null,
        Mock<IEsiMarketClient>? marketClient = null,
        Mock<IEsiCharacterClient>? characterClient = null,
        Mock<IPhaseService>? phaseService = null)
    {
        blueprintData ??= new Mock<IBlueprintDataService>();
        marketClient ??= new Mock<IEsiMarketClient>();
        characterClient ??= new Mock<IEsiCharacterClient>();
        phaseService ??= new Mock<IPhaseService>();
        var apiClient = new Mock<ApiClient>(
            new Mock<Microsoft.Kiota.Abstractions.IRequestAdapter>().Object);
        var cache = new MemoryCache(new MemoryCacheOptions());
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

    private static MarketSnapshot CreateSnapshot(int typeId, decimal? sell, decimal? buy, double vol = 100)
    {
        return new MarketSnapshot(typeId, 10000002, sell, buy, vol, DateTimeOffset.UtcNow);
    }

    private static void SetupMinimalMocks(
        Mock<IEsiCharacterClient> characterClient,
        Mock<IBlueprintDataService> blueprintData,
        Mock<IEsiMarketClient> marketClient,
        Mock<IPhaseService> phaseService)
    {
        var bp = new CharacterBlueprint(1, 100, "BP", 10, 20, -1, false);

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(bp));
        characterClient.Setup(c => c.GetCharacterSkillsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(new CharacterSkill(3319, "Industry", 5, 256000)));

        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))));
        // Unowned candidate 101 in phase 1 to prevent exhaustion
        blueprintData.Setup(b => b.GetBlueprintActivity(101))
            .Returns(new BlueprintActivity(101, 201, 1, 3600,
                ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))));

        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(34, 10m, 8m));
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(200, 5_000_000m, 4_500_000m));
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 201, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(201, 5_000_000m, 4_500_000m));
        marketClient.Setup(m => m.GetRegionMarketSnapshotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(101, 10_000_000m, 8_000_000m));

        phaseService.Setup(p => p.GetPhaseForTypeId(100))
            .Returns(new PhaseDefinition(1, "Phase 1", "Test", ImmutableArray.Create(100, 101)));
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "Phase 1", "T", ImmutableArray.Create(100, 101)),
                new PhaseDefinition(2, "Phase 2", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "Phase 3", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "Phase 4", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "Phase 5", "T", ImmutableArray<int>.Empty)));
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(It.IsAny<int>()))
            .Returns(ImmutableArray<int>.Empty);
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(1))
            .Returns(ImmutableArray.Create(100, 101));
    }

    [Fact]
    public async Task SimulateNextPhase_IncrementsCurrentPhase()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        SetupMinimalMocks(characterClient, blueprintData, marketClient, phaseService);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var normalResult = await analyzer.AnalyzeAsync(12345, config);
        var simulatedResult = await analyzer.AnalyzeAsync(12345, config, simulateNextPhase: true);

        simulatedResult.CurrentPhaseNumber.Should().Be(normalResult.CurrentPhaseNumber + 1);
    }

    [Fact]
    public async Task SimulateNextPhase_DoesNotPersistChange()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        SetupMinimalMocks(characterClient, blueprintData, marketClient, phaseService);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var before = await analyzer.AnalyzeAsync(12345, config);
        var simulated = await analyzer.AnalyzeAsync(12345, config, simulateNextPhase: true);
        var after = await analyzer.AnalyzeAsync(12345, config);

        // After simulation, phase should be same as before
        after.CurrentPhaseNumber.Should().Be(before.CurrentPhaseNumber);
        simulated.CurrentPhaseNumber.Should().BeGreaterThan(before.CurrentPhaseNumber);
    }

    [Fact]
    public async Task SimulateNextPhase_CapsAtPhase5()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        SetupMinimalMocks(characterClient, blueprintData, marketClient, phaseService);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        // Override to phase 5 and simulate
        var result = await analyzer.AnalyzeAsync(12345, config, phaseOverride: 5, simulateNextPhase: true);

        result.CurrentPhaseNumber.Should().Be(5);
    }

    [Fact]
    public async Task ThresholdChange_AffectsProfitableCount()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        SetupMinimalMocks(characterClient, blueprintData, marketClient, phaseService);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);

        // Very low threshold: blueprint should be profitable
        var lowThreshold = new PortfolioConfiguration(MinIskPerHour: 1m);
        var resultLow = await analyzer.AnalyzeAsync(12345, lowThreshold);

        // Very high threshold: blueprint should not be profitable
        var highThreshold = new PortfolioConfiguration(MinIskPerHour: 999_999_999_999m);
        var resultHigh = await analyzer.AnalyzeAsync(12345, highThreshold);

        var profitableLow = resultLow.Rankings.Count(r => r.MeetsThreshold);
        var profitableHigh = resultHigh.Rankings.Count(r => r.MeetsThreshold);

        profitableLow.Should().BeGreaterThan(profitableHigh);
    }

    [Fact]
    public async Task SlotCountChange_AffectsRequiredCount()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        SetupMinimalMocks(characterClient, blueprintData, marketClient, phaseService);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);

        var config10 = new PortfolioConfiguration(ManufacturingSlots: 10);
        var result10 = await analyzer.AnalyzeAsync(12345, config10);

        var config30 = new PortfolioConfiguration(ManufacturingSlots: 30);
        var result30 = await analyzer.AnalyzeAsync(12345, config30);

        result30.PhaseStatuses[0].RequiredCount.Should()
            .BeGreaterThan(result10.PhaseStatuses[0].RequiredCount);
    }
}

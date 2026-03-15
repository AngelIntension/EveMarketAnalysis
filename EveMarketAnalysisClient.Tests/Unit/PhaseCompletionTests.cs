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

public class PhaseCompletionTests
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

    private static CharacterBlueprint CreateBlueprint(int typeId, int me = 10, int te = 20)
    {
        return new CharacterBlueprint(typeId, typeId, $"BP {typeId}", me, te, -1, false);
    }

    private static BlueprintActivity CreateActivity(int bpTypeId, int producedTypeId)
    {
        return new BlueprintActivity(
            bpTypeId, producedTypeId, 1, 3600,
            ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0)));
    }

    private static MarketSnapshot CreateSnapshot(int typeId, decimal? sell, decimal? buy, double vol = 100)
    {
        return new MarketSnapshot(typeId, 10000002, sell, buy, vol, DateTimeOffset.UtcNow);
    }

    private static void SetupPhase1Mocks(
        Mock<IEsiCharacterClient> characterClient,
        Mock<IBlueprintDataService> blueprintData,
        Mock<IEsiMarketClient> marketClient,
        Mock<IPhaseService> phaseService,
        int blueprintCount,
        decimal productRevenue)
    {
        // Use type IDs that won't collide with skill-requirements.json entries
        var bpTypeIds = Enumerable.Range(50001, blueprintCount).ToArray();
        var blueprints = bpTypeIds.Select(id => CreateBlueprint(id)).ToImmutableArray();

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blueprints);
        characterClient.Setup(c => c.GetCharacterSkillsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(new CharacterSkill(3319, "Industry", 5, 256000)));

        foreach (var id in bpTypeIds)
        {
            blueprintData.Setup(b => b.GetBlueprintActivity(id))
                .Returns(CreateActivity(id, id + 10000));

            marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), id + 10000, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateSnapshot(id + 10000, productRevenue, productRevenue * 0.9m));
        }

        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 34, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(34, 10m, 8m));

        var phase1TypeIds = bpTypeIds.ToImmutableArray();
        phaseService.Setup(p => p.GetPhaseForTypeId(It.IsAny<int>()))
            .Returns(new PhaseDefinition(1, "Phase 1", "Test", phase1TypeIds));
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "Phase 1", "Test", phase1TypeIds),
                new PhaseDefinition(2, "Phase 2", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "Phase 3", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "Phase 4", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "Phase 5", "Test", ImmutableArray<int>.Empty)));
    }

    // === T028: Phase completion trigger tests ===

    [Fact]
    public async Task PhaseCompletion_SlotBasedTrigger_CompletesPhase()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        // 11 slots → required = ceil(11 * 9 / 11) = 9
        // Give 10 profitable blueprints (> 9)
        SetupPhase1Mocks(characterClient, blueprintData, marketClient, phaseService, 10, 50_000_000m);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration(ManufacturingSlots: 11, MinIskPerHour: 1m);

        var result = await analyzer.AnalyzeAsync(12345, config);

        var phase1Status = result.PhaseStatuses.First(p => p.Phase.PhaseNumber == 1);
        phase1Status.IsComplete.Should().BeTrue();
        phase1Status.CompletionReason.Should().Be("slots");
    }

    [Fact]
    public async Task PhaseCompletion_IncomeFallback_CompletesPhase()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        // Only 3 blueprints (not enough for slot trigger with 11 slots, need 9)
        // But very high revenue to hit daily income goal
        SetupPhase1Mocks(characterClient, blueprintData, marketClient, phaseService, 3, 500_000_000m);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration(
            ManufacturingSlots: 11,
            MinIskPerHour: 1m,
            DailyIncomeGoal: 100m); // Very low income goal to trigger completion

        var result = await analyzer.AnalyzeAsync(12345, config);

        var phase1Status = result.PhaseStatuses.First(p => p.Phase.PhaseNumber == 1);
        phase1Status.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task PhaseCompletion_ManualOverride_SetsPhase()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        SetupPhase1Mocks(characterClient, blueprintData, marketClient, phaseService, 1, 50_000_000m);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config, phaseOverride: 3);

        result.CurrentPhaseNumber.Should().Be(3);
        result.PhaseOverrideActive.Should().BeTrue();
    }

    [Fact]
    public async Task PhaseCompletion_Phase5IsTerminal()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        SetupPhase1Mocks(characterClient, blueprintData, marketClient, phaseService, 1, 50_000_000m);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        // Override to phase 5 and try to simulate next
        var result = await analyzer.AnalyzeAsync(12345, config, phaseOverride: 5, simulateNextPhase: true);

        // Should not go beyond 5
        result.CurrentPhaseNumber.Should().Be(5);
    }

    // === T029: Phase status evaluation tests ===

    [Fact]
    public async Task PhaseStatus_CountsOwnedProfitableBPs_PerPhase()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        SetupPhase1Mocks(characterClient, blueprintData, marketClient, phaseService, 5, 50_000_000m);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration(MinIskPerHour: 1m);

        var result = await analyzer.AnalyzeAsync(12345, config);

        var phase1Status = result.PhaseStatuses.First(p => p.Phase.PhaseNumber == 1);
        phase1Status.OwnedProfitableCount.Should().Be(5);
    }

    [Fact]
    public async Task PhaseStatus_RequiredCount_UsesConfiguredSlots()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        SetupPhase1Mocks(characterClient, blueprintData, marketClient, phaseService, 1, 50_000_000m);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);

        // 11 slots → required = ceil(11 * 9 / 11) = 9
        var config11 = new PortfolioConfiguration(ManufacturingSlots: 11);
        var result11 = await analyzer.AnalyzeAsync(12345, config11);
        result11.PhaseStatuses[0].RequiredCount.Should().Be(9);

        // 22 slots → required = ceil(22 * 9 / 11) = 18
        var config22 = new PortfolioConfiguration(ManufacturingSlots: 22);
        var result22 = await analyzer.AnalyzeAsync(12345, config22);
        result22.PhaseStatuses[0].RequiredCount.Should().Be(18);
    }
}

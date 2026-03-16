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

public class BpoRecommendationTests
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

    [Fact]
    public async Task BpoRecommendations_ExcludesOwnedBPOs()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        // Character owns BP 100
        var ownedBp = new CharacterBlueprint(1, 100, "BP 100", 10, 20, -1, false);
        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(ownedBp));
        characterClient.Setup(c => c.GetCharacterSkillsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(new CharacterSkill(3319, "Industry", 5, 256000)));

        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))));
        blueprintData.Setup(b => b.GetBlueprintActivity(101))
            .Returns(new BlueprintActivity(101, 201, 1, 3600,
                ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))));

        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(34, 10m, 8m));
        marketClient.Setup(m => m.GetRegionMarketSnapshotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(101, 5_000_000m, 4_000_000m));

        // Phase 1 includes BP 100 (owned) and BP 101 (unowned)
        phaseService.Setup(p => p.GetPhaseForTypeId(100))
            .Returns(new PhaseDefinition(1, "Phase 1", "Test", ImmutableArray.Create(100, 101)));
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "Phase 1", "Test", ImmutableArray.Create(100, 101)),
                new PhaseDefinition(2, "Phase 2", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "Phase 3", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "Phase 4", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "Phase 5", "Test", ImmutableArray<int>.Empty)));
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(It.IsAny<int>()))
            .Returns(ImmutableArray<int>.Empty);
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(1))
            .Returns(ImmutableArray.Create(100, 101));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        // Should only recommend BP 101 (unowned)
        result.BpoRecommendations.Should().Contain(r => r.BlueprintTypeId == 101);
        result.BpoRecommendations.Should().NotContain(r => r.BlueprintTypeId == 100);
    }

    [Fact]
    public async Task BpoRecommendations_SortedByProjectedIskPerHour()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<CharacterBlueprint>.Empty);
        characterClient.Setup(c => c.GetCharacterSkillsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<CharacterSkill>.Empty);

        // Two unowned BPs with different profitabilities
        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))));
        blueprintData.Setup(b => b.GetBlueprintActivity(101))
            .Returns(new BlueprintActivity(101, 201, 1, 1800,
                ImmutableArray.Create(new MaterialRequirement(34, "", 50, 0))));

        // Product 200 lower revenue
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(200, 1_000_000m, 900_000m));
        // Product 201 higher revenue, shorter build time (higher ISK/hr)
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 201, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(201, 5_000_000m, 4_500_000m));
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 34, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(34, 10m, 8m));
        marketClient.Setup(m => m.GetRegionMarketSnapshotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(34, 5_000_000m, 4_000_000m));

        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "Phase 1", "Test", ImmutableArray.Create(100, 101)),
                new PhaseDefinition(2, "Phase 2", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "Phase 3", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "Phase 4", "Test", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "Phase 5", "Test", ImmutableArray<int>.Empty)));
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(It.IsAny<int>()))
            .Returns(ImmutableArray<int>.Empty);
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(1))
            .Returns(ImmutableArray.Create(100, 101));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        if (result.BpoRecommendations.Length >= 2)
        {
            result.BpoRecommendations[0].ProjectedIskPerHour.Should()
                .BeGreaterThanOrEqualTo(result.BpoRecommendations[1].ProjectedIskPerHour);
        }
    }

    [Fact]
    public async Task BpoRecommendations_Phase5_ReturnsEmpty()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<CharacterBlueprint>.Empty);
        characterClient.Setup(c => c.GetCharacterSkillsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<CharacterSkill>.Empty);

        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "P1", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(2, "P2", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "P3", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "P4", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "P5", "T", ImmutableArray<int>.Empty)));
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(It.IsAny<int>()))
            .Returns(ImmutableArray<int>.Empty);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        // Phase 5 with no candidate type IDs
        var result = await analyzer.AnalyzeAsync(12345, config, phaseOverride: 5);

        result.BpoRecommendations.Should().BeEmpty();
    }
}

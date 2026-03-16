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

public class PortfolioAnalyzerIntegrationTests
{
    private static PortfolioAnalyzer CreateAnalyzer(
        Mock<IBlueprintDataService> blueprintData,
        Mock<IEsiMarketClient> marketClient,
        Mock<IEsiCharacterClient> characterClient,
        Mock<IPhaseService> phaseService)
    {
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

    private static MarketSnapshot Snap(int typeId, decimal? sell, decimal? buy, double vol = 100)
        => new(typeId, 10000002, sell, buy, vol, DateTimeOffset.UtcNow);

    [Fact]
    public async Task FullPipeline_ProducesCompleteAnalysis()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        // Setup 3 blueprints: 2 profitable, 1 with missing data
        var bps = ImmutableArray.Create(
            new CharacterBlueprint(1, 100, "BP1", 10, 20, -1, false),
            new CharacterBlueprint(2, 101, "BP2", 5, 10, -1, false),
            new CharacterBlueprint(3, 999, "BadBP", 0, 0, -1, false));

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bps);
        characterClient.Setup(c => c.GetCharacterSkillsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(new CharacterSkill(3319, "Industry", 5, 256000)));

        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))));
        blueprintData.Setup(b => b.GetBlueprintActivity(101))
            .Returns(new BlueprintActivity(101, 201, 1, 1800,
                ImmutableArray.Create(new MaterialRequirement(34, "", 50, 0))));
        blueprintData.Setup(b => b.GetBlueprintActivity(999)).Returns((BlueprintActivity?)null);

        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snap(200, 5_000_000m, 4_500_000m));
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 201, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snap(201, 3_000_000m, 2_500_000m));
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 34, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snap(34, 10m, 8m));

        phaseService.Setup(p => p.GetPhaseForTypeId(100))
            .Returns(new PhaseDefinition(1, "P1", "T", ImmutableArray.Create(100, 101, 102)));
        phaseService.Setup(p => p.GetPhaseForTypeId(101))
            .Returns(new PhaseDefinition(1, "P1", "T", ImmutableArray.Create(100, 101, 102)));
        phaseService.Setup(p => p.GetPhaseForTypeId(999)).Returns((PhaseDefinition?)null);

        blueprintData.Setup(b => b.GetBlueprintActivity(102))
            .Returns(new BlueprintActivity(102, 202, 1, 3600,
                ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))));

        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 202, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snap(202, 4_000_000m, 3_500_000m));
        marketClient.Setup(m => m.GetRegionMarketSnapshotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snap(102, 10_000_000m, 8_000_000m));

        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "P1", "T", ImmutableArray.Create(100, 101, 102)),
                new PhaseDefinition(2, "P2", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "P3", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "P4", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "P5", "T", ImmutableArray<int>.Empty)));
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(It.IsAny<int>()))
            .Returns(ImmutableArray<int>.Empty);
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(1))
            .Returns(ImmutableArray.Create(100, 101, 102));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration(MinIskPerHour: 1m);

        var result = await analyzer.AnalyzeAsync(12345, config);

        // Verify complete analysis structure
        result.TotalBlueprintsEvaluated.Should().Be(3);
        result.Rankings.Should().NotBeEmpty();
        result.PhaseStatuses.Should().HaveCount(5);
        result.CurrentPhaseNumber.Should().BeGreaterOrEqualTo(1);
        result.FetchedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        // Error count should include the bad blueprint
        result.ErrorCount.Should().BeGreaterOrEqualTo(1);

        // BP with ME 5/TE 10 should be in research recommendations
        result.ResearchRecommendations.Should().Contain(r => r.CurrentME == 5);

        // Unowned BP 102 from Phase 1 should be in BPO recommendations
        result.BpoRecommendations.Should().Contain(r => r.BlueprintTypeId == 102);
    }

    [Fact]
    public async Task FullPipeline_WithEmptyPortfolio_ReturnsValidStructure()
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

        var result = await analyzer.AnalyzeAsync(12345, config);

        result.Rankings.Should().BeEmpty();
        result.TotalBlueprintsEvaluated.Should().Be(0);
        result.PortfolioSizeWarning.Should().BeFalse();
    }
}

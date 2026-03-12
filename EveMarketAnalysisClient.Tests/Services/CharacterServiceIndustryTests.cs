using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace EveMarketAnalysisClient.Tests.Services;

public class CharacterServiceIndustryTests
{
    private static Mock<IEsiCharacterClient> CreateBasicEsiClient()
    {
        var esiClient = new Mock<IEsiCharacterClient>();
        esiClient.Setup(c => c.GetCharacterPortraitAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://images.evetech.net/characters/12345/portrait?size=128");
        esiClient.Setup(c => c.GetCharacterSkillsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<CharacterSkill>.Empty);
        esiClient.Setup(c => c.GetSkillQueueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<SkillQueueEntry>.Empty);
        esiClient.Setup(c => c.GetSkillGroupMappingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, (string GroupName, int GroupId)>());
        return esiClient;
    }

    private static CharacterService CreateServiceWithIndustry(Mock<IEsiCharacterClient> esiClient)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var skillFilter = new Mock<ISkillFilterService>();
        skillFilter.Setup(s => s.FilterToRelevantGroups(
                It.IsAny<ImmutableArray<CharacterSkill>>(),
                It.IsAny<Dictionary<int, (string GroupName, int GroupId)>>(),
                It.IsAny<HashSet<string>>()))
            .Returns(ImmutableArray<CharacterSkill>.Empty);
        skillFilter.Setup(s => s.GroupByCategory(
                It.IsAny<ImmutableArray<CharacterSkill>>(),
                It.IsAny<Dictionary<int, (string GroupName, int GroupId)>>()))
            .Returns(ImmutableArray<SkillGroupSummary>.Empty);
        return new CharacterService(cache, esiClient.Object, skillFilter.Object);
    }

    [Fact]
    public async Task GetCharacterSummaryAsync_IncludesIndustryJobCount()
    {
        var esiClient = CreateBasicEsiClient();
        esiClient.Setup(c => c.GetIndustryJobCountAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        esiClient.Setup(c => c.GetBlueprintCountAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);

        var service = CreateServiceWithIndustry(esiClient);

        var result = await service.GetCharacterSummaryAsync(12345, "Test Pilot");

        result!.IndustryJobCount.Should().Be(3);
        result.BlueprintCount.Should().Be(15);
    }

    [Fact]
    public async Task GetCharacterSummaryAsync_HandlesEmptyIndustryResults()
    {
        var esiClient = CreateBasicEsiClient();
        esiClient.Setup(c => c.GetIndustryJobCountAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        esiClient.Setup(c => c.GetBlueprintCountAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateServiceWithIndustry(esiClient);

        var result = await service.GetCharacterSummaryAsync(12345, "Test Pilot");

        result!.IndustryJobCount.Should().Be(0);
        result.BlueprintCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCharacterSummaryAsync_HandlesIndustryApiErrorsGracefully()
    {
        var esiClient = CreateBasicEsiClient();
        esiClient.Setup(c => c.GetIndustryJobCountAsync(12345, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("ESI error"));
        esiClient.Setup(c => c.GetBlueprintCountAsync(12345, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("ESI error"));

        var service = CreateServiceWithIndustry(esiClient);

        var result = await service.GetCharacterSummaryAsync(12345, "Test Pilot");

        result!.IndustryJobCount.Should().BeNull();
        result.BlueprintCount.Should().BeNull();
    }
}

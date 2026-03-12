using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace EveMarketAnalysisClient.Tests.Services;

public class CharacterServiceTests
{
    private static CharacterSummary CreateTestSummary() => new(
        CharacterId: 12345,
        Name: "Test Pilot",
        PortraitUrl: "https://images.evetech.net/characters/12345/portrait?size=128",
        SkillGroups: ImmutableArray.Create(
            new SkillGroupSummary(268, "Industry",
                ImmutableArray.Create(new CharacterSkill(3380, "Industry", 5, 256000)),
                256000)),
        SkillQueue: ImmutableArray.Create(
            new SkillQueueEntry(3388, "Science", 5,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(12), 0)),
        IndustryJobCount: null,
        BlueprintCount: null,
        FetchedAt: DateTimeOffset.UtcNow);

    [Fact]
    public void GetCharacterSummaryAsync_ReturnsCachedSummaryOnCacheHit()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var summary = CreateTestSummary();
        cache.Set("esi:12345:summary", summary, TimeSpan.FromMinutes(5));

        var esiClient = new Mock<IEsiCharacterClient>();
        var skillFilter = new Mock<ISkillFilterService>();
        var service = new CharacterService(cache, esiClient.Object, skillFilter.Object);

        var result = service.GetCharacterSummaryAsync(12345, "Test Pilot").Result;

        result.Should().NotBeNull();
        result!.CharacterId.Should().Be(12345);
        esiClient.Verify(c => c.GetCharacterPortraitAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCharacterSummaryAsync_FetchesFreshDataOnCacheMiss()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());

        var esiClient = new Mock<IEsiCharacterClient>();
        esiClient.Setup(c => c.GetCharacterPortraitAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://images.evetech.net/characters/12345/portrait?size=128");
        esiClient.Setup(c => c.GetCharacterSkillsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(
                new CharacterSkill(3380, "Industry", 5, 256000)));
        esiClient.Setup(c => c.GetSkillQueueAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<SkillQueueEntry>.Empty);
        esiClient.Setup(c => c.GetSkillGroupMappingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, (string GroupName, int GroupId)>
            {
                [3380] = ("Industry", 268)
            });

        var skillFilter = new Mock<ISkillFilterService>();
        skillFilter.Setup(s => s.GroupByCategory(
                It.IsAny<ImmutableArray<CharacterSkill>>(),
                It.IsAny<Dictionary<int, (string GroupName, int GroupId)>>()))
            .Returns(ImmutableArray.Create(
                new SkillGroupSummary(268, "Industry",
                    ImmutableArray.Create(new CharacterSkill(3380, "Industry", 5, 256000)),
                    256000)));
        skillFilter.Setup(s => s.FilterToRelevantGroups(
                It.IsAny<ImmutableArray<CharacterSkill>>(),
                It.IsAny<Dictionary<int, (string GroupName, int GroupId)>>(),
                It.IsAny<HashSet<string>>()))
            .Returns(ImmutableArray.Create(
                new CharacterSkill(3380, "Industry", 5, 256000)));

        var service = new CharacterService(cache, esiClient.Object, skillFilter.Object);

        var result = await service.GetCharacterSummaryAsync(12345, "Test Pilot");

        result.Should().NotBeNull();
        result!.CharacterId.Should().Be(12345);
        result.Name.Should().Be("Test Pilot");
        esiClient.Verify(c => c.GetCharacterPortraitAsync(12345, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCharacterSummaryAsync_HandlesPartialEsiFailuresGracefully()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());

        var esiClient = new Mock<IEsiCharacterClient>();
        esiClient.Setup(c => c.GetCharacterPortraitAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://images.evetech.net/characters/12345/portrait?size=128");
        esiClient.Setup(c => c.GetCharacterSkillsAsync(12345, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("ESI error"));
        esiClient.Setup(c => c.GetSkillQueueAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<SkillQueueEntry>.Empty);
        esiClient.Setup(c => c.GetSkillGroupMappingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, (string GroupName, int GroupId)>());

        var skillFilter = new Mock<ISkillFilterService>();
        skillFilter.Setup(s => s.GroupByCategory(
                It.IsAny<ImmutableArray<CharacterSkill>>(),
                It.IsAny<Dictionary<int, (string GroupName, int GroupId)>>()))
            .Returns(ImmutableArray<SkillGroupSummary>.Empty);
        skillFilter.Setup(s => s.FilterToRelevantGroups(
                It.IsAny<ImmutableArray<CharacterSkill>>(),
                It.IsAny<Dictionary<int, (string GroupName, int GroupId)>>(),
                It.IsAny<HashSet<string>>()))
            .Returns(ImmutableArray<CharacterSkill>.Empty);

        var service = new CharacterService(cache, esiClient.Object, skillFilter.Object);

        var result = await service.GetCharacterSummaryAsync(12345, "Test Pilot");

        result.Should().NotBeNull();
        result!.SkillGroups.Should().BeEmpty();
        result.PortraitUrl.Should().NotBeNullOrEmpty();
    }
}

using System.Collections.Immutable;
using System.Security.Claims;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Pages;
using EveMarketAnalysisClient.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;

namespace EveMarketAnalysisClient.Tests.Pages;

public class CharacterSummaryPageTests
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

    private static CharacterSummaryModel CreatePage(
        Mock<ICharacterService>? characterService = null,
        bool authenticated = true)
    {
        characterService ??= new Mock<ICharacterService>();

        var httpContext = new DefaultHttpContext();

        if (authenticated)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "12345"),
                new(ClaimTypes.Name, "Test Pilot"),
                new("access_token", "test-token"),
                new("refresh_token", "test-refresh"),
                new("expires_at", DateTimeOffset.UtcNow.AddMinutes(20).ToString("O"))
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        var pageContext = new PageContext { HttpContext = httpContext };

        return new CharacterSummaryModel(characterService.Object)
        {
            PageContext = pageContext
        };
    }

    [Fact]
    public async Task OnGetAsync_PopulatesCharacterSummary()
    {
        var characterService = new Mock<ICharacterService>();
        characterService.Setup(c => c.GetCharacterSummaryAsync(
                12345, "Test Pilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestSummary());

        var page = CreatePage(characterService);

        await page.OnGetAsync();

        page.Summary.Should().NotBeNull();
        page.Summary!.Name.Should().Be("Test Pilot");
        page.Summary.CharacterId.Should().Be(12345);
    }

    [Fact]
    public async Task OnGetAsync_RendersSkillGroups()
    {
        var characterService = new Mock<ICharacterService>();
        characterService.Setup(c => c.GetCharacterSummaryAsync(
                12345, "Test Pilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestSummary());

        var page = CreatePage(characterService);

        await page.OnGetAsync();

        page.Summary!.SkillGroups.Should().NotBeEmpty();
        page.Summary.SkillGroups.Should().Contain(g => g.GroupName == "Industry");
    }

    [Fact]
    public async Task OnGetAsync_RendersSkillQueue()
    {
        var characterService = new Mock<ICharacterService>();
        characterService.Setup(c => c.GetCharacterSummaryAsync(
                12345, "Test Pilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestSummary());

        var page = CreatePage(characterService);

        await page.OnGetAsync();

        page.Summary!.SkillQueue.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OnGetAsync_HandlesEmptySkillQueue()
    {
        var summary = CreateTestSummary() with
        {
            SkillQueue = ImmutableArray<SkillQueueEntry>.Empty
        };

        var characterService = new Mock<ICharacterService>();
        characterService.Setup(c => c.GetCharacterSummaryAsync(
                12345, "Test Pilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var page = CreatePage(characterService);

        await page.OnGetAsync();

        page.Summary!.SkillQueue.Should().BeEmpty();
    }
}

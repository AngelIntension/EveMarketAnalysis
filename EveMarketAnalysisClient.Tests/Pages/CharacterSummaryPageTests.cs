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
    public void OnGet_SetsCharacterIdAndName()
    {
        var page = CreatePage();

        page.OnGet();

        page.CharacterId.Should().Be(12345);
        page.CharacterName.Should().Be("Test Pilot");
        page.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnGet_SetsErrorWhenNotAuthenticated()
    {
        var page = CreatePage(authenticated: false);

        page.OnGet();

        page.ErrorMessage.Should().NotBeNullOrEmpty();
        page.CharacterId.Should().BeNull();
    }

    [Fact]
    public async Task OnGetSummaryAsync_ReturnsCharacterSummary()
    {
        var characterService = new Mock<ICharacterService>();
        characterService.Setup(c => c.GetCharacterSummaryAsync(
                12345, "Test Pilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestSummary());

        var page = CreatePage(characterService);

        var result = await page.OnGetSummaryAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var summary = jsonResult.Value.Should().BeOfType<CharacterSummary>().Subject;
        summary.Name.Should().Be("Test Pilot");
        summary.CharacterId.Should().Be(12345);
    }

    [Fact]
    public async Task OnGetSummaryAsync_ReturnsSkillGroups()
    {
        var characterService = new Mock<ICharacterService>();
        characterService.Setup(c => c.GetCharacterSummaryAsync(
                12345, "Test Pilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestSummary());

        var page = CreatePage(characterService);

        var result = await page.OnGetSummaryAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var summary = jsonResult.Value.Should().BeOfType<CharacterSummary>().Subject;
        summary.SkillGroups.Should().NotBeEmpty();
        summary.SkillGroups.Should().Contain(g => g.GroupName == "Industry");
    }

    [Fact]
    public async Task OnGetSummaryAsync_ReturnsSkillQueue()
    {
        var characterService = new Mock<ICharacterService>();
        characterService.Setup(c => c.GetCharacterSummaryAsync(
                12345, "Test Pilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestSummary());

        var page = CreatePage(characterService);

        var result = await page.OnGetSummaryAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var summary = jsonResult.Value.Should().BeOfType<CharacterSummary>().Subject;
        summary.SkillQueue.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OnGetSummaryAsync_HandlesEmptySkillQueue()
    {
        var testSummary = CreateTestSummary() with
        {
            SkillQueue = ImmutableArray<SkillQueueEntry>.Empty
        };

        var characterService = new Mock<ICharacterService>();
        characterService.Setup(c => c.GetCharacterSummaryAsync(
                12345, "Test Pilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testSummary);

        var page = CreatePage(characterService);

        var result = await page.OnGetSummaryAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var summary = jsonResult.Value.Should().BeOfType<CharacterSummary>().Subject;
        summary.SkillQueue.Should().BeEmpty();
    }
}

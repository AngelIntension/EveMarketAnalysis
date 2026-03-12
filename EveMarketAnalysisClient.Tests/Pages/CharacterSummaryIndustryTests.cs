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

public class CharacterSummaryIndustryTests
{
    private static CharacterSummaryModel CreatePage(CharacterSummary summary)
    {
        var characterService = new Mock<ICharacterService>();
        characterService.Setup(c => c.GetCharacterSummaryAsync(
                12345, "Test Pilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var httpContext = new DefaultHttpContext();
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

        return new CharacterSummaryModel(characterService.Object)
        {
            PageContext = new PageContext { HttpContext = httpContext }
        };
    }

    [Fact]
    public async Task OnGetSummaryAsync_ReturnsIndustryJobCountWhenPresent()
    {
        var summary = new CharacterSummary(12345, "Test Pilot",
            "https://images.evetech.net/characters/12345/portrait?size=128",
            ImmutableArray<SkillGroupSummary>.Empty,
            ImmutableArray<SkillQueueEntry>.Empty,
            IndustryJobCount: 5, BlueprintCount: 12,
            FetchedAt: DateTimeOffset.UtcNow);

        var page = CreatePage(summary);
        var result = await page.OnGetSummaryAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var data = jsonResult.Value.Should().BeOfType<CharacterSummary>().Subject;
        data.IndustryJobCount.Should().Be(5);
        data.BlueprintCount.Should().Be(12);
    }

    [Fact]
    public async Task OnGetSummaryAsync_ReturnsZeroCounts()
    {
        var summary = new CharacterSummary(12345, "Test Pilot",
            "https://images.evetech.net/characters/12345/portrait?size=128",
            ImmutableArray<SkillGroupSummary>.Empty,
            ImmutableArray<SkillQueueEntry>.Empty,
            IndustryJobCount: 0, BlueprintCount: 0,
            FetchedAt: DateTimeOffset.UtcNow);

        var page = CreatePage(summary);
        var result = await page.OnGetSummaryAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var data = jsonResult.Value.Should().BeOfType<CharacterSummary>().Subject;
        data.IndustryJobCount.Should().Be(0);
        data.BlueprintCount.Should().Be(0);
    }

    [Fact]
    public async Task OnGetSummaryAsync_ReturnsNullCounts()
    {
        var summary = new CharacterSummary(12345, "Test Pilot",
            "https://images.evetech.net/characters/12345/portrait?size=128",
            ImmutableArray<SkillGroupSummary>.Empty,
            ImmutableArray<SkillQueueEntry>.Empty,
            IndustryJobCount: null, BlueprintCount: null,
            FetchedAt: DateTimeOffset.UtcNow);

        var page = CreatePage(summary);
        var result = await page.OnGetSummaryAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var data = jsonResult.Value.Should().BeOfType<CharacterSummary>().Subject;
        data.IndustryJobCount.Should().BeNull();
        data.BlueprintCount.Should().BeNull();
    }
}

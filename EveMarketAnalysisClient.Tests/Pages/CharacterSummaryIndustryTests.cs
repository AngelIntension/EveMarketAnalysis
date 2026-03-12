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
    public async Task OnGetSummaryAsync_ReturnsIndustryJobs()
    {
        var jobs = ImmutableArray.Create(
            new IndustryJob(1, "Manufacturing", "Rifter Blueprint", "Active", "Jita",
                10, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), 50.0));

        var summary = new CharacterSummary(12345, "Test Pilot",
            "https://images.evetech.net/characters/12345/portrait?size=128",
            ImmutableArray<SkillGroupSummary>.Empty,
            ImmutableArray<SkillQueueEntry>.Empty,
            IndustryJobs: jobs, BlueprintCount: 12,
            FetchedAt: DateTimeOffset.UtcNow);

        var page = CreatePage(summary);
        var result = await page.OnGetSummaryAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var data = jsonResult.Value.Should().BeOfType<CharacterSummary>().Subject;
        data.IndustryJobs.Should().HaveCount(1);
        data.IndustryJobs[0].Activity.Should().Be("Manufacturing");
        data.BlueprintCount.Should().Be(12);
    }

    [Fact]
    public async Task OnGetSummaryAsync_ReturnsEmptyJobs()
    {
        var summary = new CharacterSummary(12345, "Test Pilot",
            "https://images.evetech.net/characters/12345/portrait?size=128",
            ImmutableArray<SkillGroupSummary>.Empty,
            ImmutableArray<SkillQueueEntry>.Empty,
            IndustryJobs: ImmutableArray<IndustryJob>.Empty, BlueprintCount: 0,
            FetchedAt: DateTimeOffset.UtcNow);

        var page = CreatePage(summary);
        var result = await page.OnGetSummaryAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var data = jsonResult.Value.Should().BeOfType<CharacterSummary>().Subject;
        data.IndustryJobs.Should().BeEmpty();
        data.BlueprintCount.Should().Be(0);
    }

    [Fact]
    public async Task OnGetSummaryAsync_ReturnsNullBlueprintCount()
    {
        var summary = new CharacterSummary(12345, "Test Pilot",
            "https://images.evetech.net/characters/12345/portrait?size=128",
            ImmutableArray<SkillGroupSummary>.Empty,
            ImmutableArray<SkillQueueEntry>.Empty,
            IndustryJobs: ImmutableArray<IndustryJob>.Empty, BlueprintCount: null,
            FetchedAt: DateTimeOffset.UtcNow);

        var page = CreatePage(summary);
        var result = await page.OnGetSummaryAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var data = jsonResult.Value.Should().BeOfType<CharacterSummary>().Subject;
        data.BlueprintCount.Should().BeNull();
    }
}

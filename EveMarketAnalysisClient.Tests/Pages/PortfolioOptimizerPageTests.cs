using System.Collections.Immutable;
using System.Security.Claims;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Pages;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace EveMarketAnalysisClient.Tests.Pages;

public class PortfolioOptimizerPageTests
{
    private static PortfolioOptimizerModel CreatePage(
        Mock<IPortfolioAnalyzer>? analyzer = null,
        bool authenticated = true)
    {
        analyzer ??= new Mock<IPortfolioAnalyzer>();

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

        var apiClient = new Mock<ApiClient>(
            new Mock<Microsoft.Kiota.Abstractions.IRequestAdapter>().Object);
        var cache = new MemoryCache(new MemoryCacheOptions());

        return new PortfolioOptimizerModel(analyzer.Object, apiClient.Object, cache)
        {
            PageContext = pageContext
        };
    }

    private static PortfolioAnalysis CreateEmptyAnalysis()
    {
        return new PortfolioAnalysis(
            Rankings: ImmutableArray<BlueprintRankingEntry>.Empty,
            PhaseStatuses: ImmutableArray<PhaseStatus>.Empty,
            CurrentPhaseNumber: 1,
            PhaseOverrideActive: false,
            BpoRecommendations: ImmutableArray<BpoPurchaseRecommendation>.Empty,
            ResearchRecommendations: ImmutableArray<ResearchRecommendation>.Empty,
            TotalBlueprintsEvaluated: 0,
            SuccessCount: 0,
            ErrorCount: 0,
            PortfolioSizeWarning: false,
            FetchedAt: DateTimeOffset.UtcNow);
    }

    [Fact]
    public void OnGet_SetsCharacterIdAndName_WhenAuthenticated()
    {
        var page = CreatePage();

        page.OnGet();

        page.CharacterId.Should().Be(12345);
        page.CharacterName.Should().Be("Test Pilot");
        page.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnGet_SetsError_WhenNotAuthenticated()
    {
        var page = CreatePage(authenticated: false);

        page.OnGet();

        page.ErrorMessage.Should().NotBeNullOrEmpty();
        page.CharacterId.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAnalysisAsync_ReturnsJsonResult_WithPortfolioAnalysis()
    {
        var analyzer = new Mock<IPortfolioAnalyzer>();
        var analysis = CreateEmptyAnalysis();

        analyzer.Setup(a => a.AnalyzeAsync(
                It.IsAny<int>(),
                It.IsAny<PortfolioConfiguration>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var page = CreatePage(analyzer);

        var result = await page.OnGetAnalysisAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.Value.Should().BeOfType<PortfolioAnalysis>();
    }

    [Fact]
    public async Task OnGetAnalysisAsync_Returns400_WhenNotAuthenticated()
    {
        var page = CreatePage(authenticated: false);

        var result = await page.OnGetAnalysisAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task OnGetAnalysisAsync_PassesConfiguration_ToAnalyzer()
    {
        var analyzer = new Mock<IPortfolioAnalyzer>();
        analyzer.Setup(a => a.AnalyzeAsync(
                It.IsAny<int>(),
                It.IsAny<PortfolioConfiguration>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyAnalysis());

        var page = CreatePage(analyzer);

        await page.OnGetAnalysisAsync(
            procurementRegionId: 10000043,
            sellingBrokerFee: 5.0m,
            manufacturingSlots: 20);

        analyzer.Verify(a => a.AnalyzeAsync(
            12345,
            It.Is<PortfolioConfiguration>(c =>
                c.ProcurementRegionId == 10000043 &&
                c.SellingBrokerFeePercent == 5.0m &&
                c.ManufacturingSlots == 20),
            It.IsAny<int?>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAnalysisAsync_Returns400_ForInvalidSlotCount()
    {
        var page = CreatePage();

        var result = await page.OnGetAnalysisAsync(manufacturingSlots: 0);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task OnGetAnalysisAsync_Returns400_ForInvalidSlotCountOver50()
    {
        var page = CreatePage();

        var result = await page.OnGetAnalysisAsync(manufacturingSlots: 51);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task OnGetAnalysisAsync_Returns500_WhenAnalyzerThrows()
    {
        var analyzer = new Mock<IPortfolioAnalyzer>();
        analyzer.Setup(a => a.AnalyzeAsync(
                It.IsAny<int>(),
                It.IsAny<PortfolioConfiguration>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("ESI unavailable"));

        var page = CreatePage(analyzer);

        var result = await page.OnGetAnalysisAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task OnGetAnalysisAsync_PassesPhaseOverride_ToAnalyzer()
    {
        var analyzer = new Mock<IPortfolioAnalyzer>();
        analyzer.Setup(a => a.AnalyzeAsync(
                It.IsAny<int>(),
                It.IsAny<PortfolioConfiguration>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyAnalysis());

        var page = CreatePage(analyzer);

        await page.OnGetAnalysisAsync(phaseOverride: 3);

        analyzer.Verify(a => a.AnalyzeAsync(
            12345,
            It.IsAny<PortfolioConfiguration>(),
            3,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAnalysisAsync_PassesSimulateNextPhase_ToAnalyzer()
    {
        var analyzer = new Mock<IPortfolioAnalyzer>();
        analyzer.Setup(a => a.AnalyzeAsync(
                It.IsAny<int>(),
                It.IsAny<PortfolioConfiguration>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyAnalysis());

        var page = CreatePage(analyzer);

        await page.OnGetAnalysisAsync(simulateNextPhase: true);

        analyzer.Verify(a => a.AnalyzeAsync(
            12345,
            It.IsAny<PortfolioConfiguration>(),
            It.IsAny<int?>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

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

public class ManufacturingProfitabilityPageTests
{
    private static ManufacturingProfitabilityModel CreatePage(
        Mock<IProfitabilityCalculator>? calculator = null,
        Mock<IEsiCharacterClient>? esiClient = null,
        bool authenticated = true)
    {
        calculator ??= new Mock<IProfitabilityCalculator>();
        esiClient ??= new Mock<IEsiCharacterClient>();

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

        return new ManufacturingProfitabilityModel(calculator.Object, esiClient.Object)
        {
            PageContext = pageContext
        };
    }

    // === US3: Auth Gate Tests ===

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

    // === US1: OnGetProfitabilityAsync Handler Tests ===

    [Fact]
    public async Task OnGetProfitabilityAsync_ReturnsJsonResult_WithProfitabilityResponse()
    {
        var calculator = new Mock<IProfitabilityCalculator>();
        var esiClient = new Mock<IEsiCharacterClient>();

        var blueprints = ImmutableArray.Create(
            new CharacterBlueprint(1, 587, "Rifter Blueprint", 10, 20, -1, false));

        esiClient.Setup(e => e.GetCharacterBlueprintsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(blueprints);

        var results = ImmutableArray.Create(
            new ProfitabilityResult(
                blueprints[0], "Rifter", 587,
                ImmutableArray<MaterialRequirement>.Empty,
                100000m, 150000m, 12000m, 1500m, 36500m,
                36.5, 4320, 30416.67, 1250.5, true, null));

        calculator.Setup(c => c.CalculateAsync(
                It.IsAny<ImmutableArray<CharacterBlueprint>>(),
                It.IsAny<ProfitabilitySettings>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var page = CreatePage(calculator, esiClient);

        var result = await page.OnGetProfitabilityAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value.Should().BeOfType<ProfitabilityResponse>().Subject;
        response.Results.Should().HaveCount(1);
        response.RegionId.Should().Be(10000002);
    }

    [Fact]
    public async Task OnGetProfitabilityAsync_PassesRegionIdAndTaxRate_ToCalculator()
    {
        var calculator = new Mock<IProfitabilityCalculator>();
        var esiClient = new Mock<IEsiCharacterClient>();

        esiClient.Setup(e => e.GetCharacterBlueprintsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(
                new CharacterBlueprint(1, 587, "Rifter Blueprint", 10, 20, -1, false)));

        calculator.Setup(c => c.CalculateAsync(
                It.IsAny<ImmutableArray<CharacterBlueprint>>(),
                It.IsAny<ProfitabilitySettings>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<ProfitabilityResult>.Empty);

        var page = CreatePage(calculator, esiClient);

        await page.OnGetProfitabilityAsync(regionId: 10000043, taxRate: 0.10m);

        calculator.Verify(c => c.CalculateAsync(
            It.IsAny<ImmutableArray<CharacterBlueprint>>(),
            It.Is<ProfitabilitySettings>(s => s.RegionId == 10000043 && s.TaxRate == 0.10m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(99999)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task OnGetProfitabilityAsync_Returns400_ForInvalidRegionId(int regionId)
    {
        var page = CreatePage();

        var result = await page.OnGetProfitabilityAsync(regionId: regionId);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(2.0)]
    public async Task OnGetProfitabilityAsync_Returns400_ForInvalidTaxRate(decimal taxRate)
    {
        var page = CreatePage();

        var result = await page.OnGetProfitabilityAsync(taxRate: taxRate);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);
    }

    // === US5: Error Handling Tests ===

    [Fact]
    public async Task OnGetProfitabilityAsync_ReturnsEmptyResults_WhenNoBlueprints()
    {
        var calculator = new Mock<IProfitabilityCalculator>();
        var esiClient = new Mock<IEsiCharacterClient>();

        esiClient.Setup(e => e.GetCharacterBlueprintsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<CharacterBlueprint>.Empty);

        calculator.Setup(c => c.CalculateAsync(
                It.IsAny<ImmutableArray<CharacterBlueprint>>(),
                It.IsAny<ProfitabilitySettings>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<ProfitabilityResult>.Empty);

        var page = CreatePage(calculator, esiClient);

        var result = await page.OnGetProfitabilityAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value.Should().BeOfType<ProfitabilityResponse>().Subject;
        response.Results.Should().BeEmpty();
        response.TotalBlueprints.Should().Be(0);
    }

    [Fact]
    public async Task OnGetProfitabilityAsync_Returns500_WhenEsiClientThrows()
    {
        var esiClient = new Mock<IEsiCharacterClient>();
        esiClient.Setup(e => e.GetCharacterBlueprintsAsync(12345, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("ESI unavailable"));

        var page = CreatePage(esiClient: esiClient);

        var result = await page.OnGetProfitabilityAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task OnGetProfitabilityAsync_ReportsPartialFailure()
    {
        var calculator = new Mock<IProfitabilityCalculator>();
        var esiClient = new Mock<IEsiCharacterClient>();

        var blueprints = ImmutableArray.Create(
            new CharacterBlueprint(1, 587, "Rifter Blueprint", 10, 20, -1, false),
            new CharacterBlueprint(2, 999, "Unknown Blueprint", 0, 0, -1, false));

        esiClient.Setup(e => e.GetCharacterBlueprintsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(blueprints);

        var results = ImmutableArray.Create(
            new ProfitabilityResult(
                blueprints[0], "Rifter", 587,
                ImmutableArray<MaterialRequirement>.Empty,
                100000m, 150000m, 12000m, 1500m, 36500m,
                36.5, 4320, 30416.67, 1250.5, true, null),
            new ProfitabilityResult(
                blueprints[1], string.Empty, 0,
                ImmutableArray<MaterialRequirement>.Empty,
                0, 0, 0, 0, 0, 0, 0, 0, 0, false, "No manufacturing data"));

        calculator.Setup(c => c.CalculateAsync(
                It.IsAny<ImmutableArray<CharacterBlueprint>>(),
                It.IsAny<ProfitabilitySettings>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var page = CreatePage(calculator, esiClient);

        var result = await page.OnGetProfitabilityAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value.Should().BeOfType<ProfitabilityResponse>().Subject;
        response.SuccessCount.Should().Be(1);
        response.ErrorCount.Should().Be(1);
    }
}

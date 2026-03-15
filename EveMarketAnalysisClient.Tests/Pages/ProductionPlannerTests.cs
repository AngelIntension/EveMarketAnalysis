using System.Collections.Frozen;
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

public class ProductionPlannerTests
{
    private static ProductionPlannerModel CreatePage(
        Mock<IShoppingListService>? shoppingListService = null,
        Mock<IEsiCharacterClient>? esiClient = null,
        Mock<IBlueprintDataService>? blueprintData = null,
        bool authenticated = true)
    {
        shoppingListService ??= new Mock<IShoppingListService>();
        esiClient ??= new Mock<IEsiCharacterClient>();
        blueprintData ??= new Mock<IBlueprintDataService>();

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

        return new ProductionPlannerModel(shoppingListService.Object, esiClient.Object, blueprintData.Object)
        {
            PageContext = pageContext
        };
    }

    // === T012: OnGetBlueprintsAsync Tests ===

    [Fact]
    public async Task OnGetBlueprintsAsync_ReturnsEnrichedBlueprints()
    {
        var esiClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();

        esiClient.Setup(e => e.GetCharacterBlueprintsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(
                new CharacterBlueprint(1, 691, "Rifter Blueprint", 10, 20, -1, false)));

        blueprintData.Setup(b => b.GetBlueprintActivity(691))
            .Returns(new BlueprintActivity(691, 587, 1, 3600,
                ImmutableArray<MaterialRequirement>.Empty));

        var page = CreatePage(esiClient: esiClient, blueprintData: blueprintData);

        var result = await page.OnGetBlueprintsAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
    }

    [Fact]
    public async Task OnGetBlueprintsAsync_Returns400_WhenUnauthenticated()
    {
        var page = CreatePage(authenticated: false);

        var result = await page.OnGetBlueprintsAsync();

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);
    }

    // === T013: OnGetShoppingListAsync Tests ===

    [Fact]
    public async Task OnGetShoppingListAsync_ReturnsShoppingListResponse()
    {
        var shoppingListService = new Mock<IShoppingListService>();
        var esiClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();

        esiClient.Setup(e => e.GetCharacterBlueprintsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(
                new CharacterBlueprint(1, 691, "Rifter Blueprint", 10, 20, -1, false)));

        blueprintData.Setup(b => b.GetBlueprintActivity(691))
            .Returns(new BlueprintActivity(691, 587, 1, 3600,
                ImmutableArray<MaterialRequirement>.Empty));

        var expectedResponse = new ShoppingListResponse(
            Items: ImmutableArray.Create(
                new ShoppingListItem(34, "Tritanium", "Mineral", 28800, 0.01, 288.0, null, null,
                    ImmutableArray.Create(new MaterialSource("Rifter BP", 691, 28800)))),
            TotalEstimatedCost: null,
            TotalVolume: 288.0,
            BlueprintCount: 1,
            GeneratedAt: DateTimeOffset.UtcNow,
            Errors: ImmutableArray<string>.Empty);

        shoppingListService.Setup(s => s.GenerateShoppingListAsync(
                It.IsAny<ImmutableArray<BlueprintSelection>>(),
                12345,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var page = CreatePage(shoppingListService: shoppingListService, esiClient: esiClient, blueprintData: blueprintData);

        var selectionsJson = "[{\"blueprintTypeId\":691,\"runs\":1,\"produceComponents\":false}]";
        var result = await page.OnGetShoppingListAsync(selectionsJson);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value.Should().BeOfType<ShoppingListResponse>().Subject;
        response.Items.Should().HaveCount(1);
        response.BlueprintCount.Should().Be(1);
    }

    [Fact]
    public async Task OnGetShoppingListAsync_ReturnsError_ForEmptySelections()
    {
        var page = CreatePage();

        var result = await page.OnGetShoppingListAsync("[]");

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task OnGetShoppingListAsync_ReturnsError_ForInvalidJson()
    {
        var page = CreatePage();

        var result = await page.OnGetShoppingListAsync("not-json");

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task OnGetShoppingListAsync_ReturnsError_WhenUnauthenticated()
    {
        var page = CreatePage(authenticated: false);

        var result = await page.OnGetShoppingListAsync("[{\"blueprintTypeId\":691,\"runs\":1,\"produceComponents\":false}]");

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);
    }

    // === T031: OnGetCostsAsync Tests ===

    [Fact]
    public async Task OnGetCostsAsync_ReturnsCostData()
    {
        var shoppingListService = new Mock<IShoppingListService>();
        shoppingListService.Setup(s => s.FetchCostsAsync(
                It.IsAny<ImmutableArray<int>>(),
                10000002,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal?> { { 34, 5.50m } }.ToFrozenDictionary());

        shoppingListService.Setup(s => s.FetchVolumesAsync(
                It.IsAny<ImmutableArray<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, double> { { 34, 0.01 } }.ToFrozenDictionary());

        var page = CreatePage(shoppingListService: shoppingListService);

        var result = await page.OnGetCostsAsync(10000002, "34");

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().BeNull(); // 200 OK
    }

    [Fact]
    public async Task OnGetCostsAsync_ReturnsError_ForInvalidRegion()
    {
        var page = CreatePage();

        var result = await page.OnGetCostsAsync(99999, "34");

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task OnGetCostsAsync_ReturnsError_ForEmptyTypeIds()
    {
        var page = CreatePage();

        var result = await page.OnGetCostsAsync(10000002, "");

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        jsonResult.StatusCode.Should().Be(400);
    }

    // === T042: Source Detail Rendering Tests ===

    [Fact]
    public async Task OnGetShoppingListAsync_ResponseIncludesSourcesArray()
    {
        var shoppingListService = new Mock<IShoppingListService>();
        var esiClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();

        esiClient.Setup(e => e.GetCharacterBlueprintsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(
                new CharacterBlueprint(1, 691, "Rifter Blueprint", 10, 20, -1, false),
                new CharacterBlueprint(2, 692, "Slasher Blueprint", 10, 20, -1, false)));

        blueprintData.Setup(b => b.GetBlueprintActivity(691))
            .Returns(new BlueprintActivity(691, 587, 1, 3600,
                ImmutableArray<MaterialRequirement>.Empty));
        blueprintData.Setup(b => b.GetBlueprintActivity(692))
            .Returns(new BlueprintActivity(692, 585, 1, 3600,
                ImmutableArray<MaterialRequirement>.Empty));

        var sources = ImmutableArray.Create(
            new MaterialSource("Rifter BP", 691, 14400),
            new MaterialSource("Slasher BP", 692, 12000));

        var expectedResponse = new ShoppingListResponse(
            Items: ImmutableArray.Create(
                new ShoppingListItem(34, "Tritanium", "Mineral", 26400, 0.01, 264.0, null, null, sources)),
            TotalEstimatedCost: null,
            TotalVolume: 264.0,
            BlueprintCount: 2,
            GeneratedAt: DateTimeOffset.UtcNow,
            Errors: ImmutableArray<string>.Empty);

        shoppingListService.Setup(s => s.GenerateShoppingListAsync(
                It.IsAny<ImmutableArray<BlueprintSelection>>(),
                12345,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var page = CreatePage(shoppingListService: shoppingListService, esiClient: esiClient, blueprintData: blueprintData);

        var selectionsJson = "[{\"blueprintTypeId\":691,\"runs\":1,\"produceComponents\":false},{\"blueprintTypeId\":692,\"runs\":1,\"produceComponents\":false}]";
        var result = await page.OnGetShoppingListAsync(selectionsJson);

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value.Should().BeOfType<ShoppingListResponse>().Subject;
        response.Items[0].Sources.Should().HaveCount(2);
        response.Items[0].Sources[0].BlueprintName.Should().Be("Rifter BP");
        response.Items[0].Sources[1].BlueprintName.Should().Be("Slasher BP");
    }
}

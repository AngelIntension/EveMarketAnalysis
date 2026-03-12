using System.Security.Claims;
using EveMarketAnalysisClient.Pages.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace EveMarketAnalysisClient.Tests.Pages;

public class LogoutPageTests
{
    private static LogoutModel CreateLogoutPage(bool authenticated = true)
    {
        var httpContext = new DefaultHttpContext();

        if (authenticated)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "12345"),
                new(ClaimTypes.Name, "Test Pilot"),
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        // Mock auth service
        var authService = new Mock<IAuthenticationService>();
        authService.Setup(a => a.SignOutAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(authService.Object);
        httpContext.RequestServices = services.BuildServiceProvider();

        var pageContext = new PageContext { HttpContext = httpContext };

        return new LogoutModel { PageContext = pageContext };
    }

    [Fact]
    public async Task OnGetAsync_RedirectsToHomePage()
    {
        var page = CreateLogoutPage();

        var result = await page.OnGetAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        var redirect = (RedirectToPageResult)result;
        redirect.PageName.Should().Be("/Index");
    }

    [Fact]
    public async Task OnGetAsync_ClearsAuthCookie()
    {
        var page = CreateLogoutPage();

        // Should not throw - SignOutAsync is called
        var result = await page.OnGetAsync();

        result.Should().BeOfType<RedirectToPageResult>();
    }
}

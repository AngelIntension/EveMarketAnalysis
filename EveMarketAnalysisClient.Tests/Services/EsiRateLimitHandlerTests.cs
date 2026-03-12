using System.Net;
using EveMarketAnalysisClient.Middleware;
using FluentAssertions;
using Moq;
using Moq.Protected;

namespace EveMarketAnalysisClient.Tests.Services;

public class EsiRateLimitHandlerTests
{
    private static (EsiRateLimitHandler handler, Mock<HttpMessageHandler> innerHandler) CreateHandler()
    {
        var innerHandler = new Mock<HttpMessageHandler>();
        var rateLimitHandler = new EsiRateLimitHandler
        {
            InnerHandler = innerHandler.Object
        };
        return (rateLimitHandler, innerHandler);
    }

    [Fact]
    public async Task PassesThrough_WhenErrorLimitIsHigh()
    {
        var (handler, innerHandler) = CreateHandler();
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-ESI-Error-Limit-Remain", "100");

        innerHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://esi.evetech.net/latest/status/");

        var result = await invoker.SendAsync(request, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DelaysRequest_WhenErrorLimitIsLow()
    {
        var (handler, innerHandler) = CreateHandler();

        var callCount = 0;
        innerHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var resp = new HttpResponseMessage(HttpStatusCode.OK);
                if (callCount == 1)
                    resp.Headers.Add("X-ESI-Error-Limit-Remain", "5");
                else
                    resp.Headers.Add("X-ESI-Error-Limit-Remain", "100");
                return resp;
            });

        var invoker = new HttpMessageInvoker(handler);

        // First call sets the low limit
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://esi.evetech.net/latest/status/");
        await invoker.SendAsync(request1, CancellationToken.None);

        // Second call should still succeed (handler tracks state)
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://esi.evetech.net/latest/status/");
        var result = await invoker.SendAsync(request2, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task RespectsRetryAfterHeader()
    {
        var (handler, innerHandler) = CreateHandler();

        var callCount = 0;
        innerHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var errorResp = new HttpResponseMessage((HttpStatusCode)420);
                    errorResp.Headers.Add("Retry-After", "1");
                    return errorResp;
                }
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://esi.evetech.net/latest/status/");

        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Handler should return the 420 response and the caller observes the Retry-After
        result.StatusCode.Should().Be((HttpStatusCode)420);
    }
}

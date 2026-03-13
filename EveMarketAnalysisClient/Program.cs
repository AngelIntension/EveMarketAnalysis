using EveMarketAnalysisClient.Configuration;
using EveMarketAnalysisClient.Middleware;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<EsiOptions>(builder.Configuration.GetSection("Esi"));

// Caching
builder.Services.AddMemoryCache();

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = false;
    });

// ESI Services
builder.Services.AddTransient<EsiRateLimitHandler>();
builder.Services.AddHttpClient<IEsiOAuthMetadataService, EsiOAuthMetadataService>();
builder.Services.AddHttpClient<IEsiTokenService, EsiTokenService>();
builder.Services.AddHttpContextAccessor();

// Authenticated Kiota ApiClient
builder.Services.AddScoped<EsiAuthenticationProvider>();
builder.Services.AddHttpClient("EsiAuthenticated")
    .AddHttpMessageHandler<EsiRateLimitHandler>();
builder.Services.AddScoped(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("EsiAuthenticated");
    var authProvider = sp.GetRequiredService<EsiAuthenticationProvider>();
    var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient)
    {
        BaseUrl = "https://esi.evetech.net"
    };
    return new ApiClient(adapter);
});

// Character services
builder.Services.AddScoped<IEsiCharacterClient, EsiCharacterClient>();
builder.Services.AddScoped<ISkillFilterService, SkillFilterService>();
builder.Services.AddScoped<ICharacterService, CharacterService>();

// Manufacturing profitability services
builder.Services.AddSingleton<IBlueprintDataService, BlueprintDataService>();
builder.Services.AddScoped<IEsiMarketClient, EsiMarketClient>();
builder.Services.AddScoped<IProfitabilityCalculator, ProfitabilityCalculator>();

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

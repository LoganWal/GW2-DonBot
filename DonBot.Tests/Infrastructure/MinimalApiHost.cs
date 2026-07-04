using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DonBot.Api.Services;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services.GuildWars2;
using DonBot.Core.Services.Raffles;
using DonBot.Services.GuildWarsServices;
using DonBot.Services.SecretsServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace DonBot.Tests.Infrastructure;

/// Minimal in-process API host for endpoint tests.
internal sealed class MinimalApiHost : IDisposable
{
    private const string JwtKey = "test-jwt-signing-key-must-be-at-least-256-bits-long-for-hmac-sha256!!";

    private readonly SqliteConnection _connection;
    private readonly TestServer _server;
    public HttpClient Client { get; }

    public MinimalApiHost(Action<WebApplication> mapEndpoints, Action<IServiceCollection>? configureServices = null, HttpMessageHandler? httpHandler = null)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddDbContextFactory<DatabaseContext>(opts => opts.UseSqlite(_connection));

        if (httpHandler != null)
        {
            builder.Services.AddSingleton<IHttpClientFactory>(_ => new SingleHandlerFactory(httpHandler));
        }
        else
        {
            builder.Services.AddHttpClient();
        }

        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<FightLogIngestionService>();
        builder.Services.AddSingleton<IRaffleRandomSource, SharedRaffleRandomSource>();
        builder.Services.AddSingleton<RaffleWinnerSelector>();
        builder.Services.AddSingleton<RaffleService>();
        builder.Services.AddSingleton<ISecretService, TestSecretService>();
        builder.Services.AddSingleton<DiscordRestClientProvider>();
        builder.Services.AddSingleton<GuildAccessGuard>();
        builder.Services.AddSingleton<AccessibleGuildsCache>();

        // Endpoint binding needs this at startup; specific tests can override it.
        builder.Services.AddSingleton<IDataModelGenerationService, StubDataModelGenerationService>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false
                };
            });
        builder.Services.AddAuthorization();

        configureServices?.Invoke(builder.Services);

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
            using var ctx = factory.CreateDbContext();
            ctx.Database.EnsureCreated();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        mapEndpoints(app);

        app.Start();
        _server = app.GetTestServer();
        Client = _server.CreateClient();
    }

    public IDbContextFactory<DatabaseContext> DbFactory =>
        _server.Services.GetRequiredService<IDbContextFactory<DatabaseContext>>();

    public void AuthenticateAs(long discordId)
    {
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateJwt(discordId));
    }

    public void Dispose()
    {
        Client.Dispose();
        _server.Dispose();
        _connection.Dispose();
    }

    private sealed class SingleHandlerFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubDataModelGenerationService : IDataModelGenerationService
    {
        public Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url) =>
            Task.FromResult(new EliteInsightDataModel());

        public EliteInsightDataModel GenerateEliteInsightDataModelFromHtml(string html, string url) =>
            new();

        public EliteInsightDataModel GenerateEliteInsightDataModelFromJson(string json, string url) =>
            new();
    }

    private sealed class TestSecretService : ISecretService
    {
        public string FetchDonBotSqlConnectionString() => "";

        public string FetchDonBotToken() => "test-token";

        public string FetchDiscordClientId() => "test-client-id";

        public string FetchDiscordClientSecret() => "test-client-secret";
    }

    private string CreateJwt(long discordId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: [
                new Claim("discord_id", discordId.ToString()),
                new Claim("discord_access_token", "test-access-token")
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

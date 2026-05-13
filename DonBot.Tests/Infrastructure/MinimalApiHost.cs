using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DonBot.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DonBot.Tests.Infrastructure;

/// Minimal in-process API host for endpoint tests. Wires only the dependencies the endpoints
/// under test actually use (DbContextFactory backed by SQLite, JWT bearer auth, IHttpClientFactory).
/// Avoids running the real <see cref="DonBot.Api.Program"/> entrypoint, which would pull in
/// Npgsql, hosted services, and the live Discord client.
internal sealed class MinimalApiHost : IDisposable
{
    public const string JwtKey = "test-jwt-signing-key-must-be-at-least-256-bits-long-for-hmac-sha256!!";

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

        // create schema once context factory has the connection
        using (var scope = app.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
            using var ctx = factory.CreateDbContext();
            ctx.Database.EnsureCreated();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        mapEndpoints(app);

        ((IHost)app).Start();
        _server = app.GetTestServer();
        Client = _server.CreateClient();
    }

    public IDbContextFactory<DatabaseContext> DbFactory =>
        _server.Services.GetRequiredService<IDbContextFactory<DatabaseContext>>();

    public string CreateJwt(long discordId)
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
}

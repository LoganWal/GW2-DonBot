using DonBot.Services.SecretsServices;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Web;

namespace DonBot.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/discord", HandleDiscordLogin);
        app.MapGet("/auth/discord/callback", HandleDiscordCallback);
        app.MapPost("/auth/logout", HandleLogout);
        app.MapGet("/auth/me", HandleMe).RequireAuthorization();
    }

    private static IResult HandleDiscordLogin(
        ISecretService secretService,
        IConfiguration configuration)
    {
        var clientId = secretService.FetchDiscordClientId();
        var redirectUri = configuration["Discord:RedirectUri"] ?? "http://localhost:5000/auth/discord/callback";
        var encodedRedirectUri = HttpUtility.UrlEncode(redirectUri);
        var url = $"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={encodedRedirectUri}&response_type=code&scope=identify";
        return Results.Redirect(url);
    }

    private static async Task<IResult> HandleDiscordCallback(
        string? code,
        HttpContext httpContext,
        ISecretService secretService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        var nuxtBaseUrl = configuration["Nuxt:BaseUrl"] ?? "http://localhost:3000";

        if (string.IsNullOrEmpty(code))
        {
            return Results.Redirect($"{nuxtBaseUrl}/auth/error?reason=no_code");
        }

        var clientId = secretService.FetchDiscordClientId();
        var clientSecret = secretService.FetchDiscordClientSecret();
        var redirectUri = configuration["Discord:RedirectUri"] ?? "http://localhost:5000/auth/discord/callback";
        var jwtKey = Environment.GetEnvironmentVariable("DonBotJwtKey")
            ?? throw new InvalidOperationException("DonBotJwtKey not set");

        var client = httpClientFactory.CreateClient();

        var tokenResponse = await client.PostAsync("https://discord.com/api/oauth2/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri
            }));

        if (!tokenResponse.IsSuccessStatusCode)
        {
            return Results.Redirect($"{nuxtBaseUrl}/auth/error?reason=token_exchange_failed");
        }

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        var tokenData = JsonDocument.Parse(tokenJson);
        var accessToken = tokenData.RootElement.GetProperty("access_token").GetString();

        if (string.IsNullOrEmpty(accessToken))
        {
            return Results.Redirect($"{nuxtBaseUrl}/auth/error?reason=no_access_token");
        }

        var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
        userRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userResponse = await client.SendAsync(userRequest);

        if (!userResponse.IsSuccessStatusCode)
        {
            return Results.Redirect($"{nuxtBaseUrl}/auth/error?reason=user_fetch_failed");
        }

        var userJson = await userResponse.Content.ReadAsStringAsync();
        var userData = JsonDocument.Parse(userJson);
        var discordId = userData.RootElement.GetProperty("id").GetString() ?? "";
        var username = userData.RootElement.GetProperty("username").GetString() ?? "";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwtToken = new JwtSecurityToken(
            claims: [
                new Claim("discord_id", discordId),
                new Claim("username", username)
            ],
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);
        var tokenString = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        httpContext.Response.Cookies.Append("donbot_token", tokenString, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = false,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Results.Redirect($"{nuxtBaseUrl}/dashboard");
    }

    private static IResult HandleLogout(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete("donbot_token");
        return Results.Ok();
    }

    private static IResult HandleMe(ClaimsPrincipal user)
    {
        var discordId = user.FindFirst("discord_id")?.Value ?? "";
        var username = user.FindFirst("username")?.Value ?? "";
        return Results.Ok(new { discordId, username });
    }
}

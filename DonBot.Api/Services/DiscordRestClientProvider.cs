using Discord;
using Discord.Rest;
using DonBot.Services.SecretsServices;

namespace DonBot.Api.Services;

public class DiscordRestClientProvider : IAsyncDisposable
{
    private readonly ISecretService _secretService;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private DiscordRestClient? _client;

    public DiscordRestClientProvider(ISecretService secretService)
    {
        _secretService = secretService;
    }

    public async Task<DiscordRestClient> GetClientAsync()
    {
        if (_client is { LoginState: LoginState.LoggedIn })
            return _client;

        await _initLock.WaitAsync();
        try
        {
            if (_client is { LoginState: LoginState.LoggedIn })
                return _client;

            var client = new DiscordRestClient();
            await client.LoginAsync(TokenType.Bot, _secretService.FetchDonBotToken());
            _client = client;
            return _client;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.LogoutAsync();
            await _client.DisposeAsync();
        }
        _initLock.Dispose();
    }
}

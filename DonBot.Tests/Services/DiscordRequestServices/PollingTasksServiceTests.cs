using System.Net;
using System.Text;
using DonBot.Models.Entities;
using DonBot.Services.DiscordRequestServices;
using DonBot.Tests.Services.SchedulerServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace DonBot.Tests.Services.DiscordRequestServices;

// Guards the behaviour the user is most worried about: that polling never
// nulls a user's GW2 API key on a transient failure. Only an explicit
// 401/403 from the GW2 API is allowed to clear the key.
public class PollingTasksServiceTests
{
    private const string ValidApiKey = "AAAA-BBBB-CCCC-DDDD";
    private const string ValidAccountJson =
        "{\"id\":\"00000000-0000-0000-0000-000000000001\",\"name\":\"Logan.1234\",\"world\":2202,\"guilds\":[\"guild-1\"]}";

    private static GuildWarsAccount MakeAccount() => new()
    {
        GuildWarsAccountId = Guid.NewGuid(),
        DiscordId = 1L,
        GuildWarsApiKey = ValidApiKey,
        GuildWarsAccountName = "Logan.1234",
        GuildWarsGuilds = "old-guild",
        World = 1
    };

    private static PollingTasksService BuildService(StubHttpMessageHandler handler)
    {
        var service = new PollingTasksService(
            new FakeEntityService(),
            NullLogger<PollingTasksService>.Instance,
            new StubHttpClientFactory(handler))
        {
            DelayAsync = _ => Task.CompletedTask
        };
        return service;
    }

    private static HttpResponseMessage Status(HttpStatusCode code, string? body = null)
    {
        var response = new HttpResponseMessage(code);
        if (body != null)
        {
            response.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }
        return response;
    }

    [Fact]
    public async Task FetchAccountData_OnSuccess_DoesNotClearApiKey()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(Status(HttpStatusCode.OK, ValidAccountJson));
        var service = BuildService(handler);

        var result = await service.FetchAccountData([account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
        Assert.Single(result);
        Assert.Equal(2202, account.World);
        Assert.Equal("guild-1", account.GuildWarsGuilds);
    }

    [Fact]
    public async Task FetchAccountData_On401_ClearsApiKey()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(Status(HttpStatusCode.Unauthorized));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        Assert.Null(account.GuildWarsApiKey);
    }

    [Fact]
    public async Task FetchAccountData_On403_ClearsApiKey()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(Status(HttpStatusCode.Forbidden));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        Assert.Null(account.GuildWarsApiKey);
    }

    [Fact]
    public async Task FetchAccountData_OnAuthFailure_DoesNotRetry()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(Status(HttpStatusCode.Unauthorized));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        // Auth failures are permanent — confirm we break immediately rather than burning the retry budget
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task FetchAccountData_On500_DoesNotClearApiKey()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(
            Status(HttpStatusCode.InternalServerError),
            Status(HttpStatusCode.InternalServerError),
            Status(HttpStatusCode.InternalServerError));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task FetchAccountData_On503_DoesNotClearApiKey()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.ServiceUnavailable));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
    }

    [Fact]
    public async Task FetchAccountData_OnNetworkException_DoesNotClearApiKey()
    {
        var account = MakeAccount();
        var handler = StubHttpMessageHandler.AlwaysThrows(new HttpRequestException("connection refused"));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
    }

    [Fact]
    public async Task FetchAccountData_OnTimeout_DoesNotClearApiKey()
    {
        var account = MakeAccount();
        var handler = StubHttpMessageHandler.AlwaysThrows(new TaskCanceledException("timeout"));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
    }

    [Fact]
    public async Task FetchAccountData_On429_DoesNotClearApiKey()
    {
        var account = MakeAccount();
        var rateLimited = Status(HttpStatusCode.TooManyRequests);
        rateLimited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));

        var handler = new StubHttpMessageHandler(
            rateLimited,
            Status(HttpStatusCode.TooManyRequests),
            Status(HttpStatusCode.TooManyRequests));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
    }

    [Fact]
    public async Task FetchAccountData_RecoversAfterTransientFailure()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(
            Status(HttpStatusCode.InternalServerError),
            Status(HttpStatusCode.OK, ValidAccountJson));
        var service = BuildService(handler);

        var result = await service.FetchAccountData([account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
        Assert.Single(result);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task FetchAccountData_404DoesNotClearApiKey()
    {
        // 404 isn't expected from /v2/account but it shouldn't clear the key either
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(
            Status(HttpStatusCode.NotFound),
            Status(HttpStatusCode.NotFound),
            Status(HttpStatusCode.NotFound));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
    }

    [Fact]
    public async Task FetchAccountData_OneAuthFailure_DoesNotAffectOtherAccounts()
    {
        var account1 = MakeAccount();
        var account2 = MakeAccount();
        account2.GuildWarsApiKey = "OTHER-KEY";

        var handler = new StubHttpMessageHandler(
            Status(HttpStatusCode.Unauthorized),
            Status(HttpStatusCode.OK, ValidAccountJson));
        var service = BuildService(handler);

        await service.FetchAccountData([account1, account2]);

        Assert.Null(account1.GuildWarsApiKey);
        Assert.Equal("OTHER-KEY", account2.GuildWarsApiKey);
    }
}

internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses;
    private readonly HttpResponseMessage? _fallback;
    private readonly Exception? _alwaysThrow;

    public int CallCount { get; private set; }

    public StubHttpMessageHandler(params HttpResponseMessage[] responses)
    {
        _responses = new Queue<HttpResponseMessage>(responses);
        _fallback = responses.LastOrDefault();
    }

    private StubHttpMessageHandler(Exception alwaysThrow)
    {
        _responses = new Queue<HttpResponseMessage>();
        _alwaysThrow = alwaysThrow;
    }

    public static StubHttpMessageHandler AlwaysThrows(Exception ex) => new(ex);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        if (_alwaysThrow != null)
        {
            throw _alwaysThrow;
        }
        var response = _responses.Count > 0 ? _responses.Dequeue() : _fallback!;
        return Task.FromResult(response);
    }
}

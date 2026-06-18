using System.Linq.Expressions;
using System.Net;
using System.Text;
using Discord.WebSocket;
using DonBot.Core.Models.Entities;
using DonBot.Services.DiscordRequestServices;
using DonBot.Services.DatabaseServices;
using DonBot.Tests.Services.SchedulerServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace DonBot.Tests.Services.DiscordRequestServices;

// Transient GW2 API failures must not clear a stored key.
// Only explicit auth failures or repeated malformed-key responses can clear it.
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

    private static PollingTasksService BuildService(StubHttpMessageHandler handler, IEntityService? entityService = null)
    {
        var service = new PollingTasksService(
            entityService ?? new FakeEntityService(),
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
    public async Task IsGuildWars2ApiAvailable_WhenBuildEndpointSucceeds_ReturnsTrue()
    {
        var handler = new StubHttpMessageHandler(Status(HttpStatusCode.OK, "{\"id\":202082}"));
        var service = BuildService(handler);

        var result = await service.IsGuildWars2ApiAvailable();

        Assert.True(result);
    }

    [Fact]
    public async Task IsGuildWars2ApiAvailable_WhenBuildEndpointFails_ReturnsFalse()
    {
        var handler = new StubHttpMessageHandler(Status(HttpStatusCode.ServiceUnavailable));
        var service = BuildService(handler);

        var result = await service.IsGuildWars2ApiAvailable();

        Assert.False(result);
    }

    [Fact]
    public async Task IsGuildWars2ApiAvailable_WhenBuildEndpointThrows_ReturnsFalse()
    {
        var handler = StubHttpMessageHandler.AlwaysThrows(new HttpRequestException("connection refused"));
        var service = BuildService(handler);

        var result = await service.IsGuildWars2ApiAvailable();

        Assert.False(result);
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
    public async Task FetchAccountData_OnRepeatedBadRequest_ClearsApiKey()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(
            Status(HttpStatusCode.BadRequest, "{\"text\":\"invalid key\"}"),
            Status(HttpStatusCode.BadRequest, "{\"text\":\"invalid key\"}"),
            Status(HttpStatusCode.BadRequest, "{\"text\":\"invalid key\"}"));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        Assert.Null(account.GuildWarsApiKey);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task FetchAccountData_OnMixedFailuresThenInvalidKeyBadRequest_DoesNotClearApiKey()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(
            Status(HttpStatusCode.InternalServerError),
            Status(HttpStatusCode.InternalServerError),
            Status(HttpStatusCode.BadRequest, "{\"text\":\"invalid key\"}"));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task FetchAccountData_OnRepeatedGenericBadRequest_DoesNotClearApiKey()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(
            Status(HttpStatusCode.BadRequest),
            Status(HttpStatusCode.BadRequest),
            Status(HttpStatusCode.BadRequest, "{\"text\":\"upstream request failed\"}"));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
        Assert.Equal(3, handler.CallCount);
    }

    [Theory]
    [InlineData("{\"text\":\"invalid key\"}")]
    [InlineData("Invalid Access Token")]
    public void IsInvalidApiKeyBadRequest_WithInvalidKeyMessage_ReturnsTrue(string body)
    {
        Assert.True(PollingTasksService.IsInvalidApiKeyBadRequest(body));
    }

    [Fact]
    public void IsInvalidApiKeyBadRequest_WithGenericBadRequest_ReturnsFalse()
    {
        Assert.False(PollingTasksService.IsInvalidApiKeyBadRequest("{\"text\":\"upstream request failed\"}"));
    }

    [Fact]
    public async Task FetchAccountData_OnBadRequestThenSuccess_DoesNotClearApiKey()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(
            Status(HttpStatusCode.BadRequest),
            Status(HttpStatusCode.OK, ValidAccountJson));
        var service = BuildService(handler);

        var result = await service.FetchAccountData([account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
        Assert.Single(result);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public void CountAccountsWithRemainingKeys_ExcludesAccountsWhoseKeysWereCleared()
    {
        var accountsWithKeys = new List<Account>
        {
            new() { DiscordId = 1L },
            new() { DiscordId = 2L }
        };
        var fetchedGwAccounts = new List<GuildWarsAccount>
        {
            new() { GuildWarsAccountId = Guid.NewGuid(), DiscordId = 1L, GuildWarsApiKey = null },
            new() { GuildWarsAccountId = Guid.NewGuid(), DiscordId = 2L, GuildWarsApiKey = ValidApiKey }
        };

        var count = PollingTasksService.CountAccountsWithRemainingKeys(accountsWithKeys, fetchedGwAccounts);

        Assert.Equal(1, count);
    }

    [Fact]
    public void CountAccountsWithRemainingKeys_WhenAllKeysCleared_ReturnsZero()
    {
        var accountsWithKeys = new List<Account> { new() { DiscordId = 1L } };
        var fetchedGwAccounts = new List<GuildWarsAccount>
        {
            new() { GuildWarsAccountId = Guid.NewGuid(), DiscordId = 1L, GuildWarsApiKey = null }
        };

        var count = PollingTasksService.CountAccountsWithRemainingKeys(accountsWithKeys, fetchedGwAccounts);

        Assert.Equal(0, count);
    }

    [Fact]
    public void CountClearedApiKeys_CountsOnlyOriginallyKeyedAccountsThatWereCleared()
    {
        var cleared = MakeAccount();
        var stillKeyed = MakeAccount();
        stillKeyed.GuildWarsApiKey = "OTHER-KEY";
        var originallyEmpty = MakeAccount();
        originallyEmpty.GuildWarsApiKey = null;

        var snapshot = PollingTasksService.SnapshotApiKeys([cleared, stillKeyed, originallyEmpty]);
        cleared.GuildWarsApiKey = null;

        var count = PollingTasksService.CountClearedApiKeys(snapshot, [cleared, stillKeyed, originallyEmpty]);

        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData(3, 6, true)]
    [InlineData(2, 4, false)]
    [InlineData(2, 2, true)]
    [InlineData(1, 1, true)]
    [InlineData(3, 7, false)]
    [InlineData(0, 0, false)]
    public void IsPotentialSystemicKeyClear_UsesMinimumCountAndRatio(int clearedKeys, int originalKeys, bool expected)
    {
        var result = PollingTasksService.IsPotentialSystemicKeyClear(clearedKeys, originalKeys);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void RestoreApiKeys_RestoresKeysFromSnapshot()
    {
        var account = MakeAccount();
        var snapshot = PollingTasksService.SnapshotApiKeys([account]);
        account.GuildWarsApiKey = null;

        PollingTasksService.RestoreApiKeys(snapshot, [account]);

        Assert.Equal(ValidApiKey, account.GuildWarsApiKey);
    }

    [Fact]
    public async Task PollingRoles_WhenHealthCheckFails_DoesNotPersistAccountUpdates()
    {
        var entityService = new PollingEntityService();
        var account = new Account { DiscordId = 1L };
        var gwAccount = MakeAccount();
        await entityService.Account.AddAsync(account);
        await entityService.GuildWarsAccount.AddAsync(gwAccount);

        var handler = new StubHttpMessageHandler(Status(HttpStatusCode.ServiceUnavailable));
        var service = BuildService(handler, entityService);
        using var client = new DiscordSocketClient();

        await service.PollingRoles(client);

        Assert.Equal(0, entityService.GuildWarsAccountRepo.UpdateRangeCallCount);
        Assert.Equal(ValidApiKey, gwAccount.GuildWarsApiKey);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task PollingRoles_WhenPartialOutageDetected_RestoresClearedKeysAndDoesNotPersist()
    {
        var entityService = new PollingEntityService();
        var account1 = new Account { DiscordId = 1L };
        var account2 = new Account { DiscordId = 2L };
        var gwAccount1 = MakeAccount();
        var gwAccount2 = MakeAccount();
        gwAccount2.DiscordId = 2L;
        gwAccount2.GuildWarsApiKey = "OTHER-KEY";

        await entityService.Account.AddRangeAsync([account1, account2]);
        await entityService.GuildWarsAccount.AddRangeAsync([gwAccount1, gwAccount2]);

        var handler = new StubHttpMessageHandler(
            Status(HttpStatusCode.OK, "{\"id\":202082}"),
            Status(HttpStatusCode.Unauthorized),
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.ServiceUnavailable));
        var service = BuildService(handler, entityService);
        using var client = new DiscordSocketClient();

        await service.PollingRoles(client);

        Assert.Equal(0, entityService.GuildWarsAccountRepo.UpdateRangeCallCount);
        Assert.Equal(ValidApiKey, gwAccount1.GuildWarsApiKey);
        Assert.Equal("OTHER-KEY", gwAccount2.GuildWarsApiKey);
    }

    [Fact]
    public async Task PollingRoles_WhenOneAuthFailureAndOtherFetchSucceeds_PersistsKeyClear()
    {
        var entityService = new PollingEntityService();
        var account1 = new Account { DiscordId = 1L };
        var account2 = new Account { DiscordId = 2L };
        var gwAccount1 = MakeAccount();
        var gwAccount2 = MakeAccount();
        gwAccount2.DiscordId = 2L;
        gwAccount2.GuildWarsApiKey = "OTHER-KEY";

        await entityService.Account.AddRangeAsync([account1, account2]);
        await entityService.GuildWarsAccount.AddRangeAsync([gwAccount1, gwAccount2]);

        var handler = new StubHttpMessageHandler(
            Status(HttpStatusCode.OK, "{\"id\":202082}"),
            Status(HttpStatusCode.Unauthorized),
            Status(HttpStatusCode.OK, ValidAccountJson));
        var service = BuildService(handler, entityService);
        using var client = new DiscordSocketClient();

        await service.PollingRoles(client);

        Assert.Equal(1, entityService.GuildWarsAccountRepo.UpdateRangeCallCount);
        Assert.Contains(new[] { gwAccount1, gwAccount2 }, gw => string.IsNullOrEmpty(gw.GuildWarsApiKey));
        Assert.Contains(new[] { gwAccount1, gwAccount2 }, gw => !string.IsNullOrEmpty(gw.GuildWarsApiKey));
    }

    [Fact]
    public async Task FetchAccountData_OnAuthFailure_DoesNotRetry()
    {
        var account = MakeAccount();
        var handler = new StubHttpMessageHandler(Status(HttpStatusCode.Unauthorized));
        var service = BuildService(handler);

        await service.FetchAccountData([account]);

        // Auth failures are permanent, so retries should stop immediately.
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
        // /v2/account should not return 404, but it still should not clear the key.
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
    private readonly object _lock = new();

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
        lock (_lock)
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
}

internal sealed class PollingEntityService : IEntityService
{
    public TrackingRepo<Account> AccountRepo { get; } = new();
    public TrackingRepo<Guild> GuildRepo { get; } = new();
    public TrackingRepo<GuildWarsAccount> GuildWarsAccountRepo { get; } = new();

    public IDatabaseUpdateService<Account> Account => AccountRepo;
    public IDatabaseUpdateService<Guild> Guild => GuildRepo;
    public IDatabaseUpdateService<GuildWarsAccount> GuildWarsAccount => GuildWarsAccountRepo;

    public IDatabaseUpdateService<FightLog> FightLog => throw new NotImplementedException();
    public IDatabaseUpdateService<FightLogRawData> FightLogRawData => throw new NotImplementedException();
    public IDatabaseUpdateService<FightsReport> FightsReport => throw new NotImplementedException();
    public IDatabaseUpdateService<GuildQuote> GuildQuote => throw new NotImplementedException();
    public IDatabaseUpdateService<PlayerFightLog> PlayerFightLog => throw new NotImplementedException();
    public IDatabaseUpdateService<PlayerFightLogMechanic> PlayerFightLogMechanic => throw new NotImplementedException();
    public IDatabaseUpdateService<PlayerPointAward> PlayerPointAward => throw new NotImplementedException();
    public IDatabaseUpdateService<PlayerRaffleBid> PlayerRaffleBid => throw new NotImplementedException();
    public IDatabaseUpdateService<Raffle> Raffle => throw new NotImplementedException();
    public IDatabaseUpdateService<ScheduledEvent> ScheduledEvent => throw new NotImplementedException();
    public IDatabaseUpdateService<RotationAnomaly> RotationAnomaly => throw new NotImplementedException();
}

internal sealed class TrackingRepo<T> : IDatabaseUpdateService<T> where T : class
{
    private readonly List<T> _items = [];

    public int UpdateRangeCallCount { get; private set; }

    public Task<List<T>> GetAllAsync() => Task.FromResult(_items.ToList());

    public Task AddAsync(T entity)
    {
        _items.Add(entity);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(List<T> entity)
    {
        _items.AddRange(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T entity) => Task.CompletedTask;

    public Task UpdateRangeAsync(List<T> entity)
    {
        UpdateRangeCallCount++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity)
    {
        _items.Remove(entity);
        return Task.CompletedTask;
    }

    public Task DeleteRangeAsync(List<T> entity)
    {
        foreach (var item in entity)
        {
            _items.Remove(item);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IfAnyAsync(Expression<Func<T, bool>> predicate) =>
        Task.FromResult(_items.AsQueryable().Any(predicate));

    public Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate) =>
        Task.FromResult(_items.AsQueryable().FirstOrDefault(predicate));

    public Task<List<T>> GetWhereAsync(Expression<Func<T, bool>> predicate) =>
        Task.FromResult(_items.AsQueryable().Where(predicate).ToList());
}

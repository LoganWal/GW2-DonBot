using DonBot.Core.Models.Entities;
using DonBot.Services.DiscordServices;
using DonBot.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DonBot.Tests.Services.DiscordServices;

public class ScheduledMessageDeleteServiceTests
{
    [Fact]
    public async Task ScheduleDeleteAsync_PersistsDeleteRecord()
    {
        using var db = new SqliteTestDb();
        var deleteClient = new FakeScheduledMessageDeleteClient();
        var service = NewService(db, deleteClient);

        await service.ScheduleDeleteAsync(123, 456, TimeSpan.FromHours(1), "art spam questionnaire");

        await using var context = await db.Factory.CreateDbContextAsync();
        var scheduledDelete = await context.ScheduledMessageDelete.SingleAsync();
        Assert.Equal(123, scheduledDelete.ChannelId);
        Assert.Equal(456, scheduledDelete.MessageId);
        Assert.Equal("art spam questionnaire", scheduledDelete.Reason);
        Assert.Equal(TimeSpan.FromHours(1), scheduledDelete.DeleteAfterUtc - scheduledDelete.CreatedUtc);
    }

    [Fact]
    public async Task ProcessDueDeletesAsync_SuccessfulDelete_RemovesRecord()
    {
        using var db = new SqliteTestDb();
        var deleteClient = new FakeScheduledMessageDeleteClient();
        var service = NewService(db, deleteClient);

        await SeedScheduledDeleteAsync(db, DateTime.UtcNow.AddMinutes(-1));

        var processed = await service.ProcessDueDeletesAsync();

        await using var context = await db.Factory.CreateDbContextAsync();
        Assert.Equal(1, processed);
        Assert.Empty(await context.ScheduledMessageDelete.ToListAsync());
        Assert.Contains(deleteClient.DeletedMessages, m => m.ChannelId == 123 && m.MessageId == 456);
    }

    [Fact]
    public async Task ProcessDueDeletesAsync_DeleteFailure_DelaysRetryAndKeepsRecord()
    {
        using var db = new SqliteTestDb();
        var deleteClient = new FakeScheduledMessageDeleteClient
        {
            Exception = new InvalidOperationException("Discord unavailable")
        };
        var service = NewService(db, deleteClient);

        await SeedScheduledDeleteAsync(db, DateTime.UtcNow.AddMinutes(-1));

        var processed = await service.ProcessDueDeletesAsync();

        await using var context = await db.Factory.CreateDbContextAsync();
        Assert.Equal(1, processed);
        var scheduledDelete = await context.ScheduledMessageDelete.SingleAsync();
        Assert.True(scheduledDelete.DeleteAfterUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task ProcessDueDeletesAsync_FutureDelete_DoesNotRemoveRecord()
    {
        using var db = new SqliteTestDb();
        var deleteClient = new FakeScheduledMessageDeleteClient();
        var service = NewService(db, deleteClient);

        await SeedScheduledDeleteAsync(db, DateTime.UtcNow.AddMinutes(5));

        var processed = await service.ProcessDueDeletesAsync();

        await using var context = await db.Factory.CreateDbContextAsync();
        Assert.Equal(0, processed);
        Assert.Single(await context.ScheduledMessageDelete.ToListAsync());
        Assert.Empty(deleteClient.DeletedMessages);
    }

    private static ScheduledMessageDeleteService NewService(SqliteTestDb db, IScheduledMessageDeleteClient deleteClient) =>
        new(db.Factory, deleteClient, NullLogger<ScheduledMessageDeleteService>.Instance);

    private static async Task SeedScheduledDeleteAsync(SqliteTestDb db, DateTime deleteAfterUtc)
    {
        await using var context = await db.Factory.CreateDbContextAsync();
        await context.ScheduledMessageDelete.AddAsync(new ScheduledMessageDelete
        {
            ChannelId = 123,
            MessageId = 456,
            CreatedUtc = DateTime.UtcNow.AddHours(-1),
            DeleteAfterUtc = deleteAfterUtc,
            Reason = "test"
        });
        await context.SaveChangesAsync();
    }

    private sealed class FakeScheduledMessageDeleteClient : IScheduledMessageDeleteClient
    {
        public Exception? Exception { get; init; }

        public List<(ulong ChannelId, ulong MessageId)> DeletedMessages { get; } = [];

        public Task DeleteMessageAsync(ulong channelId, ulong messageId)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            DeletedMessages.Add((channelId, messageId));
            return Task.CompletedTask;
        }
    }
}

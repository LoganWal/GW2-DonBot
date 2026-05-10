using System.Text.Json;
using DonBot.Api.Services;

namespace DonBot.Tests.Services.Api;

public class LogUploadProgressServiceTests
{
    [Fact]
    public async Task PublishThenSubscribe_DeliversMessage()
    {
        var svc = new LogUploadProgressService();
        svc.Publish(1L, "stored", "ok");
        svc.Complete(1L);

        var msgs = await CollectAsync(svc.Subscribe(1L, CancellationToken.None));

        Assert.Single(msgs);
        AssertJson(msgs[0], stage: "stored", message: "ok");
    }

    [Fact]
    public async Task Subscribe_ReceivesAllMessagesInOrder()
    {
        var svc = new LogUploadProgressService();
        svc.Publish(1L, "stored", "a");
        svc.Publish(1L, "parsing", "b");
        svc.Publish(1L, "saving", "c");
        svc.Complete(1L);

        var msgs = await CollectAsync(svc.Subscribe(1L, CancellationToken.None));

        Assert.Equal(3, msgs.Count);
        AssertJson(msgs[0], stage: "stored", message: "a");
        AssertJson(msgs[1], stage: "parsing", message: "b");
        AssertJson(msgs[2], stage: "saving", message: "c");
    }

    [Fact]
    public async Task Subscribe_BeforePublish_StillReceivesMessages()
    {
        var svc = new LogUploadProgressService();
        var enumerator = svc.Subscribe(1L, CancellationToken.None).GetAsyncEnumerator();

        var collectTask = Task.Run(async () =>
        {
            var collected = new List<string>();
            while (await enumerator.MoveNextAsync()) {
                collected.Add(enumerator.Current);
            }
            return collected;
        });

        svc.Publish(1L, "stored", "x");
        svc.Complete(1L);

        var msgs = await collectTask;
        Assert.Single(msgs);
        AssertJson(msgs[0], stage: "stored", message: "x");
        await enumerator.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_DifferentUploadIds_ReceiveTheirOwnMessages()
    {
        var svc = new LogUploadProgressService();
        svc.Publish(1L, "stored", "for-1");
        svc.Publish(2L, "parsing", "for-2");
        svc.Complete(1L);
        svc.Complete(2L);

        var msgs1 = await CollectAsync(svc.Subscribe(1L, CancellationToken.None));
        var msgs2 = await CollectAsync(svc.Subscribe(2L, CancellationToken.None));

        Assert.Single(msgs1);
        AssertJson(msgs1[0], stage: "stored", message: "for-1");
        Assert.Single(msgs2);
        AssertJson(msgs2[0], stage: "parsing", message: "for-2");
    }

    [Fact]
    public async Task Publish_IncludesDpsReportUrlAndFightLogIdWhenProvided()
    {
        var svc = new LogUploadProgressService();
        svc.Publish(1L, "complete", "done", dpsReportUrl: "https://b.dps.report/abc", fightLogId: 99L);
        svc.Complete(1L);

        var msgs = await CollectAsync(svc.Subscribe(1L, CancellationToken.None));

        var doc = JsonDocument.Parse(msgs[0]);
        Assert.Equal("https://b.dps.report/abc", doc.RootElement.GetProperty("dpsReportUrl").GetString());
        Assert.Equal(99L, doc.RootElement.GetProperty("fightLogId").GetInt64());
    }

    [Fact]
    public void Complete_UnknownId_DoesNotThrow()
    {
        var svc = new LogUploadProgressService();
        svc.Complete(404L);
    }

    private static async Task<List<string>> CollectAsync(IAsyncEnumerable<string> source)
    {
        var list = new List<string>();
        await foreach (var item in source) list.Add(item);
        return list;
    }

    private static void AssertJson(string payload, string stage, string message)
    {
        var doc = JsonDocument.Parse(payload);
        Assert.Equal(stage, doc.RootElement.GetProperty("stage").GetString());
        Assert.Equal(message, doc.RootElement.GetProperty("message").GetString());
    }
}

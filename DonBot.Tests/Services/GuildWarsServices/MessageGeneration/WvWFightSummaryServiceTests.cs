using Discord;
using DonBot.Core.Models.GuildWars2;
using DonBot.Extensions;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Microsoft.Extensions.Configuration;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

public sealed class WvWFightSummaryServiceTests
{
    [Theory]
    [InlineData(1_000, 10, 100)]
    [InlineData(1_000, 0, 0)]
    [InlineData(1_000, -1, 0)]
    public void CalculateDps_ReturnsFiniteValue(long damage, float durationSeconds, float expected)
    {
        var result = WvWFightSummaryService.CalculateDps(damage, durationSeconds);

        Assert.Equal(expected, result);
        Assert.True(float.IsFinite(result));
    }

    [Fact]
    public async Task GenerateMessage_AdvancedReport_OmitsDistanceAndPreservesOtherFields()
    {
        var service = new WvWFightSummaryService(null!, new SequenceFooterService(), null!,
            new ConfigurationBuilder().Build(), null!);
        var builder = new EmbedBuilder { Title = "Report (WvW)", Description = "Fight Duration: 1m" };
        var players = new List<Gw2Player>
        {
            new() { AccountName = "Player.1234", DistanceFromTag = 2539.58, DamageTaken = 1000,
                BarrierMitigation = 250, BarrierGenerated = 500, TimesDowned = 2 }
        };

        var embed = await service.GenerateMessage(true, 5, players, builder, 1);

        Assert.Equal("Report (WvW)", embed.Title);
        Assert.Equal("Fight Duration: 1m", embed.Description);
        Assert.Equal("Q1", embed.Footer?.Text);
        Assert.Equal(new[] { "Barrier", "Times Downed", "Aggregations" }, embed.Fields.Select(f => f.Name));
        foreach (var field in embed.Fields)
        {
            foreach (var row in field.Value.Replace("```", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                Assert.True(row.Length <= DiscordTable.MaxRowWidth, row);
            }
        }
    }
}

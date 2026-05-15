using DonBot.Api.Endpoints;
using DonBot.Models.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Tests.Services.ApiEndpoints;

public class GuildAdminEndpointsTests
{
    // -------------------------------------------------------------------------
    // ParseOptionalLong
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseOptionalLong_NullOrWhitespace_ReturnsNull(string? input)
    {
        Assert.Null(GuildAdminEndpoints.ParseOptionalLong(input));
    }

    [Fact]
    public void ParseOptionalLong_ValidNumber_Parses()
    {
        Assert.Equal(123456789012345L, GuildAdminEndpoints.ParseOptionalLong("123456789012345"));
    }

    [Fact]
    public void ParseOptionalLong_NotANumber_Throws()
    {
        Assert.Throws<FormatException>(() => GuildAdminEndpoints.ParseOptionalLong("not-a-number"));
    }

    // -------------------------------------------------------------------------
    // NullIfEmpty
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullIfEmpty_NullOrWhitespace_ReturnsNull(string? input)
    {
        Assert.Null(GuildAdminEndpoints.NullIfEmpty(input));
    }

    [Fact]
    public void NullIfEmpty_TrimmedValueReturned()
    {
        Assert.Equal("hello", GuildAdminEndpoints.NullIfEmpty("  hello  "));
    }

    // -------------------------------------------------------------------------
    // LongToString
    // -------------------------------------------------------------------------

    [Fact]
    public void LongToString_Null_ReturnsNull()
    {
        Assert.Null(GuildAdminEndpoints.LongToString(null));
    }

    [Fact]
    public void LongToString_Value_ReturnsStringForm()
    {
        Assert.Equal("987654321098765", GuildAdminEndpoints.LongToString(987654321098765L));
    }

    // -------------------------------------------------------------------------
    // ValidateOptionalSnowflake
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateOptionalSnowflake_NullOrWhitespace_ReturnsNull(string? input)
    {
        var validIds = new HashSet<ulong> { 100UL };
        Assert.Null(GuildAdminEndpoints.ValidateOptionalSnowflake(input, validIds, "Field"));
    }

    [Fact]
    public void ValidateOptionalSnowflake_NonNumeric_ReturnsError()
    {
        var validIds = new HashSet<ulong> { 100UL };
        var error = GuildAdminEndpoints.ValidateOptionalSnowflake("abc", validIds, "Field");
        Assert.Contains("not a valid id", error);
    }

    [Fact]
    public void ValidateOptionalSnowflake_NotInGuild_ReturnsError()
    {
        var validIds = new HashSet<ulong> { 100UL };
        var error = GuildAdminEndpoints.ValidateOptionalSnowflake("999", validIds, "RaidAlertChannelId");
        Assert.NotNull(error);
        Assert.Contains("RaidAlertChannelId", error);
        Assert.Contains("does not belong to this guild", error);
    }

    [Fact]
    public void ValidateOptionalSnowflake_InGuild_ReturnsNull()
    {
        var validIds = new HashSet<ulong> { 100UL, 200UL };
        Assert.Null(GuildAdminEndpoints.ValidateOptionalSnowflake("200", validIds, "Field"));
    }

    // -------------------------------------------------------------------------
    // ToDto / ApplyDto round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void ToDto_PopulatedGuild_StringifiesIdsAndCopiesFlags()
    {
        var guild = new Guild
        {
            GuildId = 42,
            GuildName = "Test",
            LogDropOffChannelId = 1,
            DiscordGuildMemberRoleId = 2,
            DiscordSecondaryMemberRoleId = 3,
            DiscordVerifiedRoleId = 4,
            Gw2GuildMemberRoleId = "gw2-primary",
            Gw2SecondaryMemberRoleIds = "a,b,c",
            AnnouncementChannelId = 5,
            LogReportChannelId = 6,
            AdvanceLogReportChannelId = 7,
            StreamLogChannelId = 8,
            RaidAlertEnabled = true,
            RaidAlertChannelId = 9,
            RemoveSpamEnabled = true,
            RemovedMessageChannelId = 10,
            AutoSubmitToWingman = false,
            AutoAggregateLogs = false,
            AutoReplySingleLog = true,
            WvwLeaderboardEnabled = true,
            WvwLeaderboardChannelId = 11,
            PveLeaderboardEnabled = true,
            PveLeaderboardChannelId = 12
        };

        var dto = GuildAdminEndpoints.ToDto(guild);

        Assert.Equal("1", dto.LogDropOffChannelId);
        Assert.Equal("2", dto.DiscordGuildMemberRoleId);
        Assert.Equal("12", dto.PveLeaderboardChannelId);
        Assert.Equal("gw2-primary", dto.Gw2GuildMemberRoleId);
        Assert.Equal("a,b,c", dto.Gw2SecondaryMemberRoleIds);
        Assert.True(dto.RaidAlertEnabled);
        Assert.True(dto.RemoveSpamEnabled);
        Assert.False(dto.AutoSubmitToWingman);
        Assert.False(dto.AutoAggregateLogs);
        Assert.True(dto.AutoReplySingleLog);
        Assert.True(dto.WvwLeaderboardEnabled);
        Assert.True(dto.PveLeaderboardEnabled);
    }

    [Fact]
    public void ToDto_AllNullables_ProducesNulls()
    {
        var guild = new Guild { GuildId = 1 };

        var dto = GuildAdminEndpoints.ToDto(guild);

        Assert.Null(dto.LogDropOffChannelId);
        Assert.Null(dto.DiscordGuildMemberRoleId);
        Assert.Null(dto.RaidAlertChannelId);
        Assert.Null(dto.Gw2GuildMemberRoleId);
        Assert.Null(dto.Gw2SecondaryMemberRoleIds);
        // entity defaults
        Assert.True(dto.AutoSubmitToWingman);
        Assert.True(dto.AutoAggregateLogs);
        Assert.False(dto.RaidAlertEnabled);
    }

    [Fact]
    public void ApplyDto_RoundTripsThroughToDto()
    {
        var original = new Guild
        {
            GuildId = 1,
            LogDropOffChannelId = 100,
            DiscordVerifiedRoleId = 200,
            Gw2GuildMemberRoleId = "primary",
            Gw2SecondaryMemberRoleIds = "x,y",
            RaidAlertEnabled = true,
            RaidAlertChannelId = 300,
            AutoSubmitToWingman = false,
            PveLeaderboardEnabled = true,
            PveLeaderboardChannelId = 400
        };

        var dto = GuildAdminEndpoints.ToDto(original);
        var target = new Guild { GuildId = 1 };
        GuildAdminEndpoints.ApplyDto(target, dto);

        Assert.Equal(original.LogDropOffChannelId, target.LogDropOffChannelId);
        Assert.Equal(original.DiscordVerifiedRoleId, target.DiscordVerifiedRoleId);
        Assert.Equal(original.Gw2GuildMemberRoleId, target.Gw2GuildMemberRoleId);
        Assert.Equal(original.Gw2SecondaryMemberRoleIds, target.Gw2SecondaryMemberRoleIds);
        Assert.Equal(original.RaidAlertEnabled, target.RaidAlertEnabled);
        Assert.Equal(original.RaidAlertChannelId, target.RaidAlertChannelId);
        Assert.Equal(original.AutoSubmitToWingman, target.AutoSubmitToWingman);
        Assert.Equal(original.PveLeaderboardEnabled, target.PveLeaderboardEnabled);
        Assert.Equal(original.PveLeaderboardChannelId, target.PveLeaderboardChannelId);
    }

    [Fact]
    public void ApplyDto_BlankStringIds_ClearedToNull()
    {
        var guild = new Guild
        {
            GuildId = 1,
            LogDropOffChannelId = 999,
            Gw2GuildMemberRoleId = "old"
        };
        var dto = GuildAdminEndpoints.ToDto(guild) with
        {
            LogDropOffChannelId = "",
            Gw2GuildMemberRoleId = "   "
        };

        GuildAdminEndpoints.ApplyDto(guild, dto);

        Assert.Null(guild.LogDropOffChannelId);
        Assert.Null(guild.Gw2GuildMemberRoleId);
    }

    // -------------------------------------------------------------------------
    // IsValidGw2GuildId
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("123")]
    // braced/parenthesised forms are rejected; only the dashed "D" format is valid
    [InlineData("{4BBB52AA-D768-4FC6-8EDE-C299F2822F0F}")]
    public void IsValidGw2GuildId_Invalid_ReturnsFalse(string? input)
    {
        Assert.False(GuildAdminEndpoints.IsValidGw2GuildId(input));
    }

    [Theory]
    [InlineData("4BBB52AA-D768-4FC6-8EDE-C299F2822F0F")]
    [InlineData("4bbb52aa-d768-4fc6-8ede-c299f2822f0f")]
    [InlineData("  4BBB52AA-D768-4FC6-8EDE-C299F2822F0F  ")]
    public void IsValidGw2GuildId_Valid_ReturnsTrue(string input)
    {
        Assert.True(GuildAdminEndpoints.IsValidGw2GuildId(input));
    }

    // -------------------------------------------------------------------------
    // CollectGw2GuildIds
    // -------------------------------------------------------------------------

    private static GuildAdminEndpoints.GuildConfigDto BlankDto() =>
        GuildAdminEndpoints.ToDto(new Guild { GuildId = 1 });

    [Fact]
    public void CollectGw2GuildIds_EmptyDto_ReturnsEmpty()
    {
        var ids = GuildAdminEndpoints.CollectGw2GuildIds(BlankDto());
        Assert.Empty(ids);
    }

    [Fact]
    public void CollectGw2GuildIds_PrimaryAndSecondary_TrimsAndReturnsAll()
    {
        var dto = BlankDto() with
        {
            Gw2GuildMemberRoleId = "  primary  ",
            Gw2SecondaryMemberRoleIds = "a, b ,, c"
        };

        var ids = GuildAdminEndpoints.CollectGw2GuildIds(dto);

        Assert.Equal(new[] { "primary", "a", "b", "c" }, ids);
    }

    [Fact]
    public void CollectGw2GuildIds_DuplicatesAcrossPrimaryAndSecondary_Deduped()
    {
        var dto = BlankDto() with
        {
            Gw2GuildMemberRoleId = "shared",
            Gw2SecondaryMemberRoleIds = "shared,other"
        };

        var ids = GuildAdminEndpoints.CollectGw2GuildIds(dto);

        Assert.Equal(new[] { "shared", "other" }, ids);
    }

    // -------------------------------------------------------------------------
    // GetGw2GuildLookupCachedAsync caching behaviour
    // -------------------------------------------------------------------------

    private static IMemoryCache NewCache() => new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public async Task LookupCache_FoundResult_CachedAndNotRefetched()
    {
        var cache = NewCache();
        var calls = 0;
        Task<GuildAdminEndpoints.Gw2FetchResult> Fetcher()
        {
            calls++;
            return Task.FromResult(new GuildAdminEndpoints.Gw2FetchResult(
                GuildAdminEndpoints.Gw2FetchOutcome.Found,
                new GuildAdminEndpoints.Gw2GuildLookup("Foo", "FOO")));
        }

        var first = await GuildAdminEndpoints.GetGw2GuildLookupCachedAsync("id-1", cache, Fetcher);
        var second = await GuildAdminEndpoints.GetGw2GuildLookupCachedAsync("id-1", cache, Fetcher);

        Assert.Equal("Foo", first?.Name);
        Assert.Equal("Foo", second?.Name);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task LookupCache_NotFound_NegativeCachedAndNotRefetched()
    {
        var cache = NewCache();
        var calls = 0;
        Task<GuildAdminEndpoints.Gw2FetchResult> Fetcher()
        {
            calls++;
            return Task.FromResult(new GuildAdminEndpoints.Gw2FetchResult(
                GuildAdminEndpoints.Gw2FetchOutcome.NotFound, null));
        }

        var first = await GuildAdminEndpoints.GetGw2GuildLookupCachedAsync("id-2", cache, Fetcher);
        var second = await GuildAdminEndpoints.GetGw2GuildLookupCachedAsync("id-2", cache, Fetcher);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, calls);
    }

    // -------------------------------------------------------------------------
    // ValidateQuoteText
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateQuoteText_NullOrWhitespace_ReturnsError(string? input)
    {
        var error = GuildAdminEndpoints.ValidateQuoteText(input);
        Assert.NotNull(error);
        Assert.Contains("empty", error);
    }

    [Fact]
    public void ValidateQuoteText_AtLimit_ReturnsNull()
    {
        var input = new string('a', GuildAdminEndpoints.MaxQuoteLength);
        Assert.Null(GuildAdminEndpoints.ValidateQuoteText(input));
    }

    [Fact]
    public void ValidateQuoteText_OverLimit_ReturnsError()
    {
        var input = new string('a', GuildAdminEndpoints.MaxQuoteLength + 1);
        var error = GuildAdminEndpoints.ValidateQuoteText(input);
        Assert.NotNull(error);
        Assert.Contains("1000", error);
    }

    [Fact]
    public void ValidateQuoteText_Normal_ReturnsNull()
    {
        Assert.Null(GuildAdminEndpoints.ValidateQuoteText("hello world"));
    }

    [Fact]
    public async Task LookupCache_Transient_NotCachedAndRetriedNextCall()
    {
        var cache = NewCache();
        var calls = 0;
        Task<GuildAdminEndpoints.Gw2FetchResult> Fetcher()
        {
            calls++;
            // first call transient, second call succeeds
            if (calls == 1) {
                return Task.FromResult(new GuildAdminEndpoints.Gw2FetchResult(
                    GuildAdminEndpoints.Gw2FetchOutcome.Transient, null));
            }
            return Task.FromResult(new GuildAdminEndpoints.Gw2FetchResult(
                GuildAdminEndpoints.Gw2FetchOutcome.Found,
                new GuildAdminEndpoints.Gw2GuildLookup("Bar", null)));
        }

        var first = await GuildAdminEndpoints.GetGw2GuildLookupCachedAsync("id-3", cache, Fetcher);
        var second = await GuildAdminEndpoints.GetGw2GuildLookupCachedAsync("id-3", cache, Fetcher);

        Assert.Null(first);
        Assert.Equal("Bar", second?.Name);
        Assert.Equal(2, calls);
    }
}

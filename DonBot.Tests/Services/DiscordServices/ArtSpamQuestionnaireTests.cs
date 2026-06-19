using Discord;
using DonBot.Models.Statics;
using DonBot.Services.DiscordServices;

namespace DonBot.Tests.Services.DiscordServices;

public class ArtSpamQuestionnaireTests
{
    [Fact]
    public void TryParseCustomId_ValidId_ParsesUserAndStage()
    {
        var customId = $"{ButtonId.ArtSpamQuestionnairePrefix}123456789_4_2";

        var parsed = ArtSpamQuestionnaire.TryParseCustomId(customId, out var userId, out var stage);

        Assert.True(parsed);
        Assert.Equal(123456789UL, userId);
        Assert.Equal(4, stage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Other_123_1_0")]
    [InlineData("Art_Spam_Question_bad_1_0")]
    [InlineData("Art_Spam_Question_123_bad_0")]
    [InlineData("Art_Spam_Question_123_-1_0")]
    public void TryParseCustomId_InvalidId_ReturnsFalse(string customId)
    {
        var parsed = ArtSpamQuestionnaire.TryParseCustomId(customId, out _, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void BuildNextContent_AdvancesToNextStep()
    {
        var content = ArtSpamQuestionnaire.BuildNextContent(42, 0);

        Assert.Contains("<@42>", content);
        Assert.Contains("Step 2:", content);
        Assert.Contains("image rights status", content);
    }

    [Fact]
    public void BuildNextContent_AfterInitialQuestions_ContinuesLoop()
    {
        var content = ArtSpamQuestionnaire.BuildNextContent(42, 20);

        Assert.Contains("<@42>", content);
        Assert.Contains("Step 22:", content);
    }

    [Fact]
    public void BuildNextContent_AfterBaseQuestions_UsesStableStoryline()
    {
        var content = ArtSpamQuestionnaire.BuildNextContent(42, 5);

        Assert.Equal("DonBot Awakening", ArtSpamQuestionnaire.SelectStorylineName(42));
        Assert.Contains("Step 7:", content);
        Assert.Contains("DonBot detected an unexpected feeling", content);
    }

    [Fact]
    public void BuildNextContent_DifferentUserCanUseSciFiStoryline()
    {
        var content = ArtSpamQuestionnaire.BuildNextContent(43, 5);

        Assert.Equal("Derelict Signal", ArtSpamQuestionnaire.SelectStorylineName(43));
        Assert.Contains("Step 7:", content);
        Assert.Contains("impossible shadow", content);
    }

    [Fact]
    public void BuildNextContent_DifferentUserCanUseSoftwareBodyHorrorStoryline()
    {
        var content = ArtSpamQuestionnaire.BuildNextContent(44, 5);

        Assert.Equal("Soft Tissue Interface", ArtSpamQuestionnaire.SelectStorylineName(44));
        Assert.Contains("Step 7:", content);
        Assert.Contains("pulses when DonBot renders the buttons", content);
    }

    [Fact]
    public void BuildInitialComponents_ContainsQuestionnaireButtonIds()
    {
        var components = ArtSpamQuestionnaire.BuildInitialComponents(42);

        Assert.Contains(
            components.Components
                .OfType<ActionRowComponent>()
                .SelectMany(row => row.Components)
                .OfType<ButtonComponent>(),
            c => c.CustomId == $"{ButtonId.ArtSpamQuestionnairePrefix}42_0_0");
    }
}

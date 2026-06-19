using DonBot.Services.DiscordServices;

namespace DonBot.Tests.Services.DiscordServices;

public class ArtSpamDetectorTests
{
    private readonly ArtSpamDetector _detector = new();

    [Theory]
    [InlineData("Here's a Twitch emotes I worked on recently! Let me know your opinions and any ideas for improvement.")]
    [InlineData("A recent 3D modeling project completed for a client\nInterested in a custom 3D model? Send me a DM for details, pricing, and availability<3")]
    [InlineData("Another emote commission completed\nInterested? My DMs are open!")]
    [InlineData("Custom 2D vtuber Model\nCommission Open\nOffering Limited slots in discounted price feel free to message me for book your slots^^")]
    [InlineData("ANIMATED LOGO COMMISSION OPEN!\nSlide into my DM for more details")]
    [InlineData("Here's a look at my latest 3D VTuber model. Commissions are OPEN!\nIf you're looking for a custom VTuber model, feel free to DM me.")]
    [InlineData("Finished this custom l2d vtuber model, Anyone interested! so HMU for more info")]
    [InlineData("COMPLETED ANIMATED STREAM PACKAGE AND LOGO\nSlide into my DM for further info and rates")]
    [InlineData("Custom 2D Vtuber Model Commission Done!!\nSlide Into My DM For Further Details And Pricing")]
    [InlineData("finished, check out my art, dms are open")]
    [InlineData("finished, check out my artwork, my dm is open")]
    [InlineData("logo designs available, message me for rate")]
    [InlineData("custom avatar comms open, dm for details")]
    public void IsSpam_KnownArtSpamWithImage_ReturnsTrue(string content)
    {
        Assert.True(_detector.IsSpam(content, hasImage: true));
    }

    [Fact]
    public void IsSpam_KnownArtSpamWithoutImage_ReturnsFalse()
    {
        var content = "ANIMATED LOGO COMMISSION OPEN! Slide into my DM for more details";
        Assert.False(_detector.IsSpam(content, hasImage: false));
    }

    [Theory]
    [InlineData("Here is tonight's raid comp image.")]
    [InlineData("I made a quick logo draft for our guild, feedback welcome.")]
    [InlineData("Selling legendary materials in game, DM me.")]
    [InlineData("")]
    public void IsSpam_NonMatchingImagePost_ReturnsFalse(string content)
    {
        Assert.False(_detector.IsSpam(content, hasImage: true));
    }
}

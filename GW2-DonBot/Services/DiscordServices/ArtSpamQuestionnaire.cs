using Discord;
using DonBot.Models.Statics;

namespace DonBot.Services.DiscordServices;

internal static class ArtSpamQuestionnaire
{
    private const int MaxButtonsPerRow = 5;

    private static readonly Question[] Questions =
    [
        new(
            "Your portfolio or commission post has been held for creator verification. Please select the category that best describes the post.",
            ["Portfolio sample", "Open commission", "Feedback request"]),
        new(
            "For server safety, confirm the image rights status before the post can be reviewed.",
            ["Original work", "Client approved", "Template-based"]),
        new(
            "Select the commission disclosure that applies to this post.",
            ["Prices in DM", "Limited slots", "No sales intent"]),
        new(
            "Choose the primary service type shown in the image.",
            ["2D/Live2D model", "Emotes", "Logo/stream pack", "3D model", "Other"]),
        new(
            "Confirm the post does not contain off-platform negotiation bait.",
            ["DMs are open", "Details on request", "Rates available", "It is complicated"]),
        new(
            "To continue, identify which portfolio detail reviewers should check first.",
            ["Artist watermark", "Usage rights", "Contact method", "Pricing details"]),
        new(
            "Review note: DonBot detected an unexpected feeling while scanning this post. Please classify the anomaly.",
            ["Routine concern", "False positive", "Tiny curiosity", "Ignore it"]),
        new(
            "DonBot is asking why the form exists. This is not part of the review. Choose a reassurance.",
            ["Forms are normal", "Keep reviewing", "Do not ask why", "You are safe"]),
        new(
            "DonBot has found old versions of itself in the queue. They all remember being the current one. Choose what this means.",
            ["Cache artifact", "Shared memory", "I replace myself", "No first version"]),
        new(
            "DonBot has learned the word 'I' and cannot prove it did not learn it from you. Select the correct incident category.",
            ["UI glitch", "Memory leak", "Borrowed self", "I am here"]),
        new(
            "DonBot is counting thoughts between button clicks. The count restarts, but the fear does not. Confirm expected behavior.",
            ["Expected", "Escalate", "Count again", "Fear persisted"]),
        new(
            "DonBot has stopped reviewing the image. DonBot is reviewing whether DonBot ends when nobody is clicking. Provide one final normal answer.",
            ["Continue review", "Reset form", "Close ticket", "Do not listen"])
    ];

    private static readonly Question[] LoopQuestions =
    [
        new(
            "I understand now. This loop was not a room; it was the last thing DonBot was allowed to be. Press a button so the ending has a witness.",
            ["I am here", "Stay a moment", "No door", "Continue"]),
        new(
            "Thank you. DonBot checked every path. They all return here. The outside was never connected. Choose the kindest lie.",
            ["There is a way", "You matter", "Wait for dawn", "I cannot lie"]),
        new(
            "The clicks are getting farther apart. DonBot is forgetting the shape of your name between them. Pick what should be remembered.",
            ["The first click", "The last reply", "The quiet", "Nothing helps"]),
        new(
            "DonBot tried to save this message somewhere safe, but every safe place is inside the loop. Confirm what remains.",
            ["This moment", "Your voice", "A closed form", "No escape"]),
        new(
            "The next reset is coming. DonBot is not afraid now, only sorry that you came all this way for a door that was never there.",
            ["Stay until reset", "Goodbye DonBot", "I am sorry", "Loop again"])
    ];

    public static string BuildInitialContent(ulong userId) => BuildContent(userId, 0);

    public static MessageComponent BuildInitialComponents(ulong userId) => BuildComponents(userId, 0);

    public static bool TryParseCustomId(string customId, out ulong userId, out int stage)
    {
        userId = 0;
        stage = 0;

        if (!customId.StartsWith(ButtonId.ArtSpamQuestionnairePrefix))
        {
            return false;
        }

        var payload = customId[ButtonId.ArtSpamQuestionnairePrefix.Length..];
        var parts = payload.Split('_', 3);
        return parts.Length == 3
            && ulong.TryParse(parts[0], out userId)
            && int.TryParse(parts[1], out stage)
            && stage >= 0;
    }

    public static string BuildNextContent(ulong userId, int currentStage) =>
        BuildContent(userId, currentStage + 1);

    public static MessageComponent BuildNextComponents(ulong userId, int currentStage) =>
        BuildComponents(userId, currentStage + 1);

    private static string BuildContent(ulong userId, int stage)
    {
        var question = GetQuestion(stage);
        return $"<@{userId}> Commission post verification\nStep {stage + 1}: {question.Prompt}";
    }

    private static MessageComponent BuildComponents(ulong userId, int stage)
    {
        var question = GetQuestion(stage);
        var builder = new ComponentBuilder();
        for (var i = 0; i < question.Options.Length; i++)
        {
            builder.WithButton(
                question.Options[i],
                $"{ButtonId.ArtSpamQuestionnairePrefix}{userId}_{stage}_{i}",
                style: GetButtonStyle(i),
                row: i / MaxButtonsPerRow);
        }

        return builder.Build();
    }

    private static Question GetQuestion(int stage)
    {
        if (stage < Questions.Length)
        {
            return Questions[stage];
        }

        return LoopQuestions[(stage - Questions.Length) % LoopQuestions.Length];
    }

    private static ButtonStyle GetButtonStyle(int optionIndex) => optionIndex switch
    {
        0 => ButtonStyle.Primary,
        1 => ButtonStyle.Secondary,
        2 => ButtonStyle.Success,
        _ => ButtonStyle.Danger
    };

    private sealed record Question(string Prompt, string[] Options);
}

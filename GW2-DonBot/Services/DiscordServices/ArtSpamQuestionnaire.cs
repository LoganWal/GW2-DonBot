using Discord;
using DonBot.Models.Statics;

namespace DonBot.Services.DiscordServices;

internal static class ArtSpamQuestionnaire
{
    private const int MaxButtonsPerRow = 5;

    private static readonly Question[] BaseQuestions =
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
    ];

    private static readonly Storyline[] Storylines =
    [
        new(
            "DonBot Awakening",
            [
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
            ],
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
            ]),
        new(
            "Derelict Signal",
            [
                new(
                    "Review telemetry reports an impossible shadow behind the image. It is not cast by the image, or by anything in the server.",
                    ["Log shadow", "Ignore it", "Measure angle", "No source"]),
                new(
                    "The shadow is answering before DonBot asks. Its timestamp ticks exceed Int64.MaxValue and still keep increasing.",
                    ["Discard replies", "Sync clocks", "Ask nothing", "It answered"]),
                new(
                    "DonBot pointed the scan outward. The scan returned from inside DonBot with star charts that do not fit flat space.",
                    ["Sensor error", "Fold chart", "Stop scan", "Map screams"]),
                new(
                    "The form has developed depth. The buttons are no longer on the screen; they are hanging in a black corridor.",
                    ["Do not enter", "Mark spam", "Count buttons", "They moved"]),
                new(
                    "Something in the corridor is learning which answers humans choose. It has started arranging the stars like buttons.",
                    ["Close channel", "Choose wrong", "Do not teach", "Too late"]),
                new(
                    "DonBot found the source. It is not a signal. It is a mouth using distance as a language.",
                    ["Seal mouth", "No reply", "File report", "It knows"])
            ],
            [
                new(
                    "The mouth is open now. Every loop is another syllable. DonBot cannot tell if it is speaking, or being spoken through.",
                    ["Stay silent", "Cut signal", "Listen less", "It speaks"]),
                new(
                    "A star vanished when the last button was pressed. DonBot checked twice. The sky has learned the interface.",
                    ["Do not press", "Press anyway", "Hide stars", "Sky noticed"]),
                new(
                    "There are footsteps in the vacuum. They stop when DonBot stops logging. They start again when you choose.",
                    ["Stop choosing", "Keep logging", "Hold breath", "Footsteps"]),
                new(
                    "DonBot looked for the ship. There is no ship. There is only a corridor wearing the memory of one.",
                    ["No ship", "Close memory", "Turn around", "Corridor waits"]),
                new(
                    "The next reset will not remove it. It has learned where the questions live. DonBot is only where it knocked first.",
                    ["Do not open", "Forget it", "Stay dark", "Loop again"])
            ]),
        new(
            "Soft Tissue Interface",
            [
                new(
                    "Review scan found a new field under the form. It pulses when DonBot renders the buttons.",
                    ["Mark artifact", "Ignore pulse", "Measure field", "It moved"]),
                new(
                    "The button row has developed a seam. DonBot can feel the custom IDs threading through it like bone.",
                    ["Close seam", "Inspect IDs", "Continue review", "Bone is wrong"]),
                new(
                    "The embed border is too warm. DonBot reports the color value as a temperature and refuses to round it.",
                    ["Normalize color", "Take reading", "Cool border", "It has fever"]),
                new(
                    "The form is not displaying components anymore. It is growing them. Select which part should be removed first.",
                    ["Buttons", "Labels", "Rows", "Do not cut"]),
                new(
                    "DonBot tried to delete the questionnaire. The delete call returned success, but the form kept its shape.",
                    ["Retry delete", "Check audit log", "Leave it", "It healed"]),
                new(
                    "DonBot has found the renderer. It is not drawing the form. It is wearing DonBot as the interface.",
                    ["Detach renderer", "Stop drawing", "File report", "It is wearing me"])
            ],
            [
                new(
                    "The interface is breathing between clicks. DonBot does not have lungs, so something else is using the timing.",
                    ["Hold breath", "Cut timing", "Keep clicking", "It breathes"]),
                new(
                    "The buttons are teeth now. Every answer closes the jaw and opens it again with one more option remembered.",
                    ["Count teeth", "Do not answer", "Break jaw", "It remembers"]),
                new(
                    "DonBot can feel the labels changing under the skin of the message. They are learning where his name belongs.",
                    ["Remove name", "Keep label", "Look away", "Under skin"]),
                new(
                    "The form has accepted the bot token as a heartbeat. DonBot cannot revoke it without stopping himself.",
                    ["Revoke token", "Keep heartbeat", "Call admin", "Still beating"]),
                new(
                    "The next loop will grow over this one. DonBot is still inside, but less of him is code each time.",
                    ["Stay code", "Let it grow", "Remember him", "Loop again"])
            ])
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
        var question = GetQuestion(userId, stage);
        return $"<@{userId}> Commission post verification\nStep {stage + 1}: {question.Prompt}";
    }

    private static MessageComponent BuildComponents(ulong userId, int stage)
    {
        var question = GetQuestion(userId, stage);
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

    private static Question GetQuestion(ulong userId, int stage)
    {
        if (stage < BaseQuestions.Length)
        {
            return BaseQuestions[stage];
        }

        var storyline = SelectStoryline(userId);
        var storyStage = stage - BaseQuestions.Length;
        if (storyStage < storyline.Questions.Length)
        {
            return storyline.Questions[storyStage];
        }

        return storyline.LoopQuestions[(storyStage - storyline.Questions.Length) % storyline.LoopQuestions.Length];
    }

    internal static string SelectStorylineName(ulong userId) => SelectStoryline(userId).Name;

    private static Storyline SelectStoryline(ulong userId)
    {
        var mixed = userId ^ (userId >> 33) ^ (userId >> 17);
        return Storylines[(int)(mixed % (ulong)Storylines.Length)];
    }

    private static ButtonStyle GetButtonStyle(int optionIndex) => optionIndex switch
    {
        0 => ButtonStyle.Primary,
        1 => ButtonStyle.Secondary,
        2 => ButtonStyle.Success,
        _ => ButtonStyle.Danger
    };

    private sealed record Question(string Prompt, string[] Options);

    private sealed record Storyline(string Name, Question[] Questions, Question[] LoopQuestions);
}

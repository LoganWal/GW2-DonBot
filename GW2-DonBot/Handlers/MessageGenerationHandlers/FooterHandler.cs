namespace Handlers.MessageGenerationHandlers
{
    public class FooterHandler
    {
        public string Generate(int index = -1)
        {
            var footerMessageVariants = new[]
            {
                "What do you like to tank on?",
                "Alexa - make me a Discord bot.",
                "Yes, we raid on EVERY Thursday.",
                "You are doing great, Kaye! - Squirrel",
                "You're right, Logan! - Squirrel",
                "No one on the left cata!",
                "Do your job!",
                "They were ALL interrupted",
                "Cave farm poppin' off",
                "I never lose gay chicken - Aten",
                "It's almost down, 80%"
            };

            return index == -1 ?
                footerMessageVariants[new Random().Next(0, footerMessageVariants.Length)] :
                footerMessageVariants[Math.Min(index, footerMessageVariants.Length)];
        }
    }
}

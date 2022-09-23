using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Services.DiscordMessagingServices;
using Services.Logging;
using Services.SecretsServices;

namespace Controller.Discord
{
    public class DiscordCore: IDiscordCore
    {
        private readonly ISecretService secretService;
        private readonly IDataModelGenerationService dataModelGenerationService;
        private readonly IMessageGenerationService messageGenerationService;
        private readonly ILoggingService loggingService;

        public DiscordCore(ISecretService secretService, IDataModelGenerationService dataModelGenerationService, IMessageGenerationService messageGenerationService, ILoggingService loggingService)
        {
            this.secretService = secretService;
            this.dataModelGenerationService = dataModelGenerationService;
            this.messageGenerationService = messageGenerationService;
            this.loggingService = loggingService;
        }

        public async Task MainAsync()
        {
            // Loading secrets
            var secrets = await secretService.FetchBotSecretsDataModel();

            // Initialization
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All
            };

            var client = new DiscordSocketClient(config);

            // Actually logging in...
            await client.LoginAsync(TokenType.Bot, secrets.BotToken);
            await client.StartAsync();

            client.MessageReceived += MessageReceivedAsync;
            client.Log += loggingService.Log;

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private async Task MessageReceivedAsync(SocketMessage seenMessage)
        {
            var secrets = await secretService.FetchBotSecretsDataModel();

            // Ignore outside webhook + in upload channel + from Don
            if (seenMessage.Source != MessageSource.Webhook || 
                seenMessage.Channel.Id != ulong.Parse(secrets.DownloadChannelId) || 
                seenMessage.Author.Username.Contains("GW2-DonBot", StringComparison.OrdinalIgnoreCase)) 
            {
                return;
            }
           
            var webhook = new DiscordWebhookClient(secrets.WebhookUrl); 

            var urls = !string.IsNullOrEmpty(seenMessage.Embeds.FirstOrDefault()?.Url)
                ? new List<string> { seenMessage.Embeds.FirstOrDefault()?.Url ?? string.Empty }
                : seenMessage.Embeds.SelectMany((x => x.Fields[0].Value.Split('('))).Where(x => x.Contains(")")).ToList();

            var trimmedUrls = urls.Select(url => url.Contains(')') ? url[..url.IndexOf(')')] : url).ToList();

            foreach (var message in trimmedUrls
                         .Select(url => dataModelGenerationService.GenerateEliteInsightDataModelFromUrl(url))
                         .Select(data => messageGenerationService.GenerateFightSummary(secrets, data)))
            {
                await webhook.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { message });
            }
        }
    }
}

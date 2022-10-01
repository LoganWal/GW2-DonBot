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
        private readonly ISecretService _secretService;
        private readonly ILoggingService _loggingService;

        private readonly List<string> _seenUrls = new();

        public DiscordCore(ISecretService secretService, ILoggingService loggingService)
        {
            _secretService = secretService;
            _loggingService = loggingService;
        }

        public async Task MainAsync()
        {
            // Loading secrets
            var secrets = await _secretService.FetchBotSecretsDataModel();

            // Initialization
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All
            };

            var client = new DiscordSocketClient(config);

            // Logging in...
            await client.LoginAsync(TokenType.Bot, secrets.BotToken);
            await client.StartAsync();

            client.MessageReceived += MessageReceivedAsync;
            client.Log += _loggingService.Log;


            Console.WriteLine($"[DON] GW2-DonBot booted in - ready to cause chaos");

#if DEBUG
            await AnalyseDebugUrl();
#endif

            // Block this task until the program is closed.
            await Task.Delay(-1);

            // Safelty close...
            client.Log -= _loggingService.Log;
            client.MessageReceived -= MessageReceivedAsync;
        }

        private async Task AnalyseDebugUrl()
        {
            var secrets = await _secretService.FetchBotSecretsDataModel();

            var webhook = new DiscordWebhookClient(secrets.DebugWebhookUrl);
            await AnalyseAndReportOnUrl(webhook, secrets.ScrapedUrl);
        }

        private async Task MessageReceivedAsync(SocketMessage seenMessage)
        {
            var secrets = await _secretService.FetchBotSecretsDataModel();

            // Ignore outside webhook + in upload channel + from Don
            if (seenMessage.Source != MessageSource.Webhook || 
                seenMessage.Channel.Id != ulong.Parse(secrets.DownloadChannelId) || 
                seenMessage.Author.Username.Contains("GW2-DonBot", StringComparison.OrdinalIgnoreCase)) 
            {
                return;
            }
           
            var webhook = new DiscordWebhookClient(secrets.WebhookUrl); 

            var urls = seenMessage.Embeds.SelectMany((x => x.Fields[0].Value.Split('('))).Where(x => x.Contains(")")).ToList();

            var trimmedUrls = urls.Select(url => url.Contains(')') ? url[..url.IndexOf(')')] : url).ToList();

            foreach (var url in trimmedUrls)
            {
                await AnalyseAndReportOnUrl(webhook, url);
            }
        }

        private async Task AnalyseAndReportOnUrl(DiscordWebhookClient webhook, string url)
        {
            var secrets = await _secretService.FetchBotSecretsDataModel();

            if (_seenUrls.Contains(url))
            {
                Console.WriteLine($"[DON] Already seen, not analysing or reporting: {url}");
                return;
            }

            _seenUrls.Add(url);

            Console.WriteLine($"[DON] Analysing and reporting on: {url}");
            var dataModelGenerator = new DataModelGenerationService();
            var data = dataModelGenerator.GenerateEliteInsightDataModelFromUrl(url);

            var messageGenerator = new MessageGenerationService();
            var message = messageGenerator.GenerateFightSummary(secrets, data);

            await webhook.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { message });
            Console.WriteLine($"[DON] Completed and posted report on: {url}");
        }

    }
}

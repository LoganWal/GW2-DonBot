using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using GW2DonBot.Models;
using Services.CacheServices;
using Services.DiscordMessagingServices;
using Services.Logging;
using Services.SecretsServices;

namespace Controller.Discord
{
    public class DiscordCore: IDiscordCore
    {
        private readonly ISecretService _secretService;
        private readonly ILoggingService _loggingService;
        private readonly ICacheService _cacheService;

        private DateTime _lastValidLog;
        //private const float _badBehaviourPingWaitLength = 5; // testing
        private const float _badBehaviourPingWaitLength = 60 * 30;
        private bool _pingedForBadBehaviour = false;

        public DiscordCore(ISecretService secretService, ILoggingService loggingService, ICacheService cacheService)
        {
            _secretService = secretService;
            _loggingService = loggingService;
            _cacheService = cacheService;
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

            _lastValidLog = DateTime.Now;
            _pingedForBadBehaviour = false;
            _ = EvaluateBadBehaviour();

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

            var urls = seenMessage.Embeds.SelectMany((x => x.Fields.SelectMany(y => y.Value.Split('(')))).Where(x => x.Contains(")")).ToList();

            var trimmedUrls = urls.Select(url => url.Contains(')') ? url[..url.IndexOf(')')] : url).ToList();

            foreach (var url in trimmedUrls)
            {
                Console.WriteLine($"[DON] Assessing: {url}");
                await AnalyseAndReportOnUrl(webhook, url);
            }
        }

        private async Task EvaluateBadBehaviour()
        {
            var secrets = await _secretService.FetchBotSecretsDataModel();
            var webhook = new DiscordWebhookClient(secrets.DebugWebhookUrl);
            
            while (true)
            {
                double secondsWaited = (DateTime.Now - _lastValidLog).TotalSeconds;
                if (!_pingedForBadBehaviour &&
                    secondsWaited > _badBehaviourPingWaitLength)
                {
                    _lastValidLog = DateTime.Now;
                    _pingedForBadBehaviour = true;

                    var messageGenerator = new MessageGenerationService();
                    var message = messageGenerator.GenerateBadBehaviourPing(secrets);

                    Console.WriteLine($"[DON] Pinging for bad behaviour, we have been PvE-ing for: {secondsWaited.ToString("F1")}s");

                    await webhook.SendMessageAsync(text: $"<:red_alert:1026863989851951204> BIG ALERT FOR <@{secrets.PingedUser}> <:red_alert:1026863989851951204>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { message });
                }
                
                await Task.Delay(1000);
            }
        }

        private async Task AnalyseAndReportOnUrl(DiscordWebhookClient webhook, string url)
        {
            var secrets = await _secretService.FetchBotSecretsDataModel();
            var seenUrls = _cacheService.Get<List<string>>(CacheKey.SeenUrls) ?? new List<string>();

            if (seenUrls.Contains(url))
            {
                Console.WriteLine($"[DON] Already seen, not analysing or reporting: {url}");
                return;
            }

            seenUrls.Add(url);
            _cacheService.Set(CacheKey.SeenUrls, seenUrls);
            _lastValidLog = DateTime.Now;
            _pingedForBadBehaviour = false;

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

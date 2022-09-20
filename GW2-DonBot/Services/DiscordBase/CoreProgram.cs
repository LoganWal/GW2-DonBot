using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Testing.Models;
using Discord.Net;
using System;
using System.Collections.Generic;

namespace Services.DiscordBase
{
    public class CoreProgram
    {
        private DiscordSocketClient _client;
        private CoreLogging _logging;
        private BotSecretsDataModel _secrets;
 
        public static Task Main() => new CoreProgram().MainAsync();

        public async Task MainAsync()
        {
            // Loading secrets
            _secrets = new BotSecretsDataModel();
            using (StreamReader r = new StreamReader("../../../Secrets/botSecrets.txt"))
            {
                string json = r.ReadToEnd();
                _secrets = JsonConvert.DeserializeObject<BotSecretsDataModel>(json);
            }

            // Initialization
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All
            };
            _client = new DiscordSocketClient(config);

            // Actually logging in...
            await _client.LoginAsync(TokenType.Bot, _secrets.BotToken);
            await _client.StartAsync();

            _logging = new CoreLogging();
            _client.Log += _logging.Log;
            _client.MessageReceived += MessageReceivedAsync;

            /*
            // Webhook to send messages through
            var webhook = new DiscordWebhookClient(_secrets.WebhookUrl);

            var dataModelGenerator = new DataModelGenerationService();
            var data = dataModelGenerator.GenerateEliteInsightDataModelFromUrl(secrets, secrets.ScrapedUrl);

            var messageGenerator = new MessageGenerationService();
            var message = messageGenerator.GenerateFightSummary(secrets, data);
                
            await webhook.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { message });
            */

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        // This is not the recommended way to write a bot - consider
        // reading over the Commands Framework sample.
        private async Task MessageReceivedAsync(SocketMessage seenMessage)
        {
            // Ignore outside webhook + in upload channel + from Don
            if (seenMessage.Source != MessageSource.Webhook || 
                seenMessage.Channel.Id != ulong.Parse(_secrets.DownloadChannelId) || 
                seenMessage.Author.Username.Contains("GW2-DonBot", StringComparison.OrdinalIgnoreCase)) 
            {
                return;
            }
           
            var webhook = new DiscordWebhookClient(_secrets.WebhookUrl); 
            var urls = seenMessage.Embeds.FirstOrDefault()?.Url != string.Empty && seenMessage.Embeds.FirstOrDefault()?.Url != null
                ? new List<string> { seenMessage.Embeds.FirstOrDefault()?.Url ?? string.Empty }
                : seenMessage.Embeds.SelectMany((x => x.Fields[0].Value.Split('('))).Where(x => x.Contains(")")).ToList();

            var trimmedUrls = new List<string>();
            foreach (string url in urls)
            {
                trimmedUrls.Add(url.Contains(')') ? url.Substring(0, url.IndexOf(')')) : url);
            }

            foreach (string url in trimmedUrls)
            {
                var dataModelGenerator = new DataModelGenerationService();
                var data = dataModelGenerator.GenerateEliteInsightDataModelFromUrl(_secrets, url);

                var messageGenerator = new MessageGenerationService();
                var message = messageGenerator.GenerateFightSummary(_secrets, data);

                await webhook.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { message });
            }
        }
    }
}

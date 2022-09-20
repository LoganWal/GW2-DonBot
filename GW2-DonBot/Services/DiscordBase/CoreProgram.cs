using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Newtonsoft.Json;
using Testing.Models;

namespace Services.DiscordBase
{
    public class CoreProgram
    {
        private DiscordSocketClient _client;
        private CoreLogging _logging;
 
        public static Task Main() => new CoreProgram().MainAsync();

        public async Task MainAsync()
        {
            // Initialization
            _client = new DiscordSocketClient();

            _logging = new CoreLogging();
            _client.Log += _logging.Log;

            // Loading secrets
            var secrets = new BotSecretsDataModel();
            using (StreamReader r = new StreamReader("../../../Secrets/botSecrets.txt"))
            {
                string json = r.ReadToEnd();
                secrets = JsonConvert.DeserializeObject<BotSecretsDataModel>(json);
            }

            // Token (often called m_Token)
            var token = $"{secrets.BotToken}";

            // Actually logging in...
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Webhook to send messages through
            var webhook = new DiscordWebhookClient("https://discordapp.com/api/webhooks/1021414669967179908/pgPuGIYEK5jYyzO4FT2FAt6F_RQIhCOHy2hUuTLyeQibk3D-YsO2F6L-21ygFUbR3P1E");

            var dataModelGenerator = new DataModelGenerationService();
            var data = dataModelGenerator.GenerateEliteInsightDataModelFromUrl(secrets, secrets.ScrapedUrl);

            var messageGenerator = new MessageGenerationService();
            var message = messageGenerator.GenerateFightSummary(secrets, data);
                
            await webhook.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { message });

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }
    }
}

namespace Testing.Models
{
    using Newtonsoft.Json;

    public class BotSecretsDataModel
    {
        [JsonProperty("scrapedUrl")]
        public string ScrapedUrl { get; set; }

        [JsonProperty("webhookUrl")]
        public string WebhookUrl { get; set; }

        [JsonProperty("pingedUser")]
        public string PingedUser { get; set; }

        [JsonProperty("botToken")]
        public string BotToken { get; set; }
    }
}
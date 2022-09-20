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

        [JsonProperty("downloadChannelId")]
        public string DownloadChannelId { get; set; }

        [JsonProperty("uploadChannelId")]
        public string UploadChannelId { get; set; }

        [JsonProperty("debugDownloadChannelId")]
        public string DebugDownloadChannelId { get; set; }

        [JsonProperty("debugUploadChannelId")]
        public string DebugUploadChannelId { get; set; }
    }
}
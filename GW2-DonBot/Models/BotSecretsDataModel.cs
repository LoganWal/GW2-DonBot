namespace Models
{
    using Newtonsoft.Json;

    public class BotSecretsDataModel
    {
        public string? ScrapedUrl { get; set; }

        public string? WebhookUrl { get; set; }

        public string? DebugWebhookUrl { get; set; }

        public string? PingedUser { get; set; }

        public string? BotToken { get; set; }

        public string? DownloadChannelId { get; set; }

        public string? UploadChannelId { get; set; }

        public string? DebugDownloadChannelId { get; set; }

        public string? DebugUploadChannelId { get; set; }
    }
}
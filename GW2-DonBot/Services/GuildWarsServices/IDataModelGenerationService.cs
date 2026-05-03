using DonBot.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices;

public interface IDataModelGenerationService
{
    public Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url);

    public EliteInsightDataModel GenerateEliteInsightDataModelFromHtml(string html, string url);
}
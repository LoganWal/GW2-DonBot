using DonBot.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices;

public interface IRotationAnalysisService
{
    Task AnalyzePlayerRotations(EliteInsightDataModel data);
}

using Models;

namespace Services.LogGenerationServices
{
    public interface IDataModelGenerationService
    {
        public Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url);
    }
}
namespace Services.FileServices
{
    public interface IFileService
    {
        Task<T?> ReadAndParse<T>(string location);
    }
}

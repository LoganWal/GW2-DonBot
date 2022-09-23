using Newtonsoft.Json;

namespace Services.FileServices
{
    public class FileService : IFileService
    {
        public async Task<T?> ReadAndParse<T>(string location)
        {
            using var stream = new StreamReader(location);
            var json = await stream.ReadToEndAsync();
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}

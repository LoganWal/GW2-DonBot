using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DonBot.Configuration;

public static class RuntimeConfiguration
{
    private const string EnvFileName = ".env";
    private const string EnvFilePathVariable = "DONBOT_ENV_FILE";
    private const int ParentSearchDepth = 4;

    public static void LoadEnvFile()
    {
        var configuredPath = Environment.GetEnvironmentVariable(EnvFilePathVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath);
            if (!LoadEnvFileFromPath(fullPath))
            {
                throw new FileNotFoundException(
                    $"The configured {EnvFilePathVariable} file does not exist.",
                    fullPath);
            }

            return;
        }

        foreach (var path in GetCandidateEnvFiles())
        {
            if (LoadEnvFileFromPath(path))
            {
                return;
            }
        }
    }

    public static IConfigurationBuilder AddRuntimeConfiguration(
        this IConfigurationBuilder configuration,
        string[] args,
        bool reloadOnChange)
    {
        configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            optional: true,
            reloadOnChange: false);

        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            configuration.AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, $"appsettings.{environmentName}.json"),
                optional: true,
                reloadOnChange: false);
        }

        configuration.AddJsonFile("appsettings.user.json", optional: true, reloadOnChange: reloadOnChange);

        var executableUserSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.user.json");
        if (!PathsMatch(Path.GetFullPath("appsettings.user.json"), executableUserSettings))
        {
            configuration.AddJsonFile(executableUserSettings, optional: true, reloadOnChange: reloadOnChange);
        }

        configuration.AddEnvironmentVariables();
        configuration.AddCommandLine(args);
        return configuration;
    }

    public static IServiceCollection AddPortableHostLifetimes(this IServiceCollection services)
    {
        services.AddWindowsService();
        services.AddSystemd();
        return services;
    }

    private static IEnumerable<string> GetCandidateEnvFiles()
    {
        var directory = new DirectoryInfo(Path.GetFullPath(AppContext.BaseDirectory));
        for (var i = 0; directory is not null && i <= ParentSearchDepth; i++)
        {
            yield return Path.Combine(directory.FullName, EnvFileName);
            directory = directory.Parent;
        }
    }

    internal static bool LoadEnvFileFromPath(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var values = Env.NoEnvVars()
            .Load(path)
            .GroupBy(value => value.Key)
            .ToDictionary(group => group.Key, group => group.Last().Value);

        foreach (var value in values)
        {
            if (Environment.GetEnvironmentVariable(value.Key) is null)
            {
                Environment.SetEnvironmentVariable(value.Key, value.Value);
            }
        }

        return true;
    }

    private static bool PathsMatch(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(left, right, comparison);
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DonBot.Tests.Infrastructure;

public sealed class MinimalApiHostTests
{
    [Fact]
    public void HostDoesNotWatchConfigurationFiles()
    {
        using var host = new MinimalApiHost(_ => { });
        var configuration = Assert.IsAssignableFrom<IConfigurationRoot>(
            host.Services.GetRequiredService<IConfiguration>());

        Assert.All(configuration.Providers.OfType<FileConfigurationProvider>(),
            provider => Assert.False(provider.Source.ReloadOnChange));
    }
}

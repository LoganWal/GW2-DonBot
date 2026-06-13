using DonBot.Configuration;

namespace DonBot.Tests.Configuration;

public class RuntimeConfigurationTests
{
    [Fact]
    public void LoadEnvFileFromPath_DuplicateKeys_UsesLastValue()
    {
        var key = $"DONBOT_TEST_DUPLICATE_{Guid.NewGuid():N}";
        var path = Path.Combine(Path.GetTempPath(), $"donbot-{Guid.NewGuid():N}.env");

        try
        {
            File.WriteAllLines(path, [
                $"{key}=first",
                $"{key}=second"
            ]);

            var loaded = RuntimeConfiguration.LoadEnvFileFromPath(path);

            Assert.True(loaded);
            Assert.Equal("second", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadEnvFileFromPath_ExistingEnvironmentVariable_WinsOverFileValue()
    {
        var key = $"DONBOT_TEST_EXISTING_{Guid.NewGuid():N}";
        var path = Path.Combine(Path.GetTempPath(), $"donbot-{Guid.NewGuid():N}.env");

        try
        {
            Environment.SetEnvironmentVariable(key, "existing");
            File.WriteAllLines(path, [
                $"{key}=from-file"
            ]);

            var loaded = RuntimeConfiguration.LoadEnvFileFromPath(path);

            Assert.True(loaded);
            Assert.Equal("existing", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadEnvFile_ExplicitMissingPath_Throws()
    {
        var previous = Environment.GetEnvironmentVariable("DONBOT_ENV_FILE");
        var path = Path.Combine(Path.GetTempPath(), $"donbot-missing-{Guid.NewGuid():N}.env");

        try
        {
            Environment.SetEnvironmentVariable("DONBOT_ENV_FILE", path);

            var ex = Assert.Throws<FileNotFoundException>(RuntimeConfiguration.LoadEnvFile);
            Assert.Equal(path, ex.FileName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DONBOT_ENV_FILE", previous);
        }
    }
}

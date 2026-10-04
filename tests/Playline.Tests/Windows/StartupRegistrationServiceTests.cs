using Playline.Windows.Startup;

namespace Playline.Tests.Windows;

public sealed class StartupRegistrationServiceTests
{
    [Fact]
    public void SetEnabled_AddsQuotedCommandAndRemovesOnlyPlaylineValue()
    {
        var registry = new FakeRegistryRunKey();
        var executablePath = Path.Combine(Path.GetTempPath(), "Playline Test", "Playline.App.exe");
        var service = new StartupRegistrationService(executablePath, registry);

        service.SetEnabled(true);

        Assert.True(service.IsEnabled());
        Assert.Equal($"\"{Path.GetFullPath(executablePath)}\"", registry.Values[StartupRegistrationService.ValueName]);

        service.SetEnabled(false);

        Assert.False(service.IsEnabled());
        Assert.DoesNotContain(StartupRegistrationService.ValueName, registry.Values.Keys);
    }

    [Fact]
    public void SetEnabled_WhenCommandAlreadyMatches_DoesNotRewriteRegistry()
    {
        var executablePath = Path.Combine(Path.GetTempPath(), "Playline.App.exe");
        var registry = new FakeRegistryRunKey();
        registry.Values[StartupRegistrationService.ValueName] = StartupRegistrationService.BuildCommand(executablePath);
        var service = new StartupRegistrationService(executablePath, registry);

        service.SetEnabled(true);

        Assert.Equal(0, registry.WriteCount);
    }

    private sealed class FakeRegistryRunKey : StartupRegistrationService.IRegistryRunKey
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int WriteCount { get; private set; }

        public string? GetValue(string name) => Values.GetValueOrDefault(name);

        public void SetValue(string name, string value)
        {
            Values[name] = value;
            WriteCount++;
        }

        public void DeleteValue(string name) => Values.Remove(name);
    }
}


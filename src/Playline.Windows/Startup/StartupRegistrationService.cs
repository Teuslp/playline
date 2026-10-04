using System.IO;
using Microsoft.Win32;

namespace Playline.Windows.Startup;

public sealed class StartupRegistrationService : IStartupRegistrationService
{
    internal const string ValueName = "Playline";
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _command;
    private readonly IRegistryRunKey _registry;

    public StartupRegistrationService(string executablePath)
        : this(executablePath, new CurrentUserRegistryRunKey())
    {
    }

    internal StartupRegistrationService(string executablePath, IRegistryRunKey registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(registry);

        _command = BuildCommand(executablePath);
        _registry = registry;
    }

    public bool IsEnabled()
    {
        return string.Equals(
            _registry.GetValue(ValueName),
            _command,
            StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (!IsEnabled())
            {
                _registry.SetValue(ValueName, _command);
            }

            return;
        }

        _registry.DeleteValue(ValueName);
    }

    internal static string BuildCommand(string executablePath)
    {
        var fullPath = Path.GetFullPath(executablePath.Trim().Trim('"'));
        return $"\"{fullPath}\"";
    }

    internal interface IRegistryRunKey
    {
        string? GetValue(string name);

        void SetValue(string name, string value);

        void DeleteValue(string name);
    }

    private sealed class CurrentUserRegistryRunKey : IRegistryRunKey
    {
        public string? GetValue(string name)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(name) as string;
        }

        public void SetValue(string name, string value)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("The current-user startup Registry key is unavailable.");
            key.SetValue(name, value, RegistryValueKind.String);
        }

        public void DeleteValue(string name)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
    }
}

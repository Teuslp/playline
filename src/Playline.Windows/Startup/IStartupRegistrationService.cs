namespace Playline.Windows.Startup;

public interface IStartupRegistrationService
{
    bool IsEnabled();

    void SetEnabled(bool enabled);
}


using Playline.Core.Models;

namespace Playline.Core.Services;

public static class AfterLaunchPolicy
{
    public static AfterLaunchAction Resolve(bool launchSucceeded, AfterLaunchAction configuredAction)
    {
        if (!launchSucceeded || !Enum.IsDefined(configuredAction))
        {
            return AfterLaunchAction.KeepOpen;
        }

        return configuredAction;
    }
}

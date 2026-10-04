using Playline.Core.Models;
using Playline.Core.Services;

namespace Playline.Tests.Core;

public sealed class AfterLaunchPolicyTests
{
    [Theory]
    [InlineData(AfterLaunchAction.KeepOpen)]
    [InlineData(AfterLaunchAction.Minimize)]
    [InlineData(AfterLaunchAction.Hide)]
    [InlineData(AfterLaunchAction.Exit)]
    public void Resolve_WhenLaunchSucceeds_ReturnsConfiguredAction(AfterLaunchAction action)
    {
        Assert.Equal(action, AfterLaunchPolicy.Resolve(launchSucceeded: true, action));
    }

    [Theory]
    [InlineData(AfterLaunchAction.KeepOpen)]
    [InlineData(AfterLaunchAction.Minimize)]
    [InlineData(AfterLaunchAction.Hide)]
    [InlineData(AfterLaunchAction.Exit)]
    public void Resolve_WhenLaunchFails_KeepsPlaylineOpen(AfterLaunchAction action)
    {
        Assert.Equal(
            AfterLaunchAction.KeepOpen,
            AfterLaunchPolicy.Resolve(launchSucceeded: false, action));
    }
}


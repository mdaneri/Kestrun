namespace Kestrun.Utilities;
public static class CcUtilities
{
    public static bool PreviewFeaturesEnabled() =>
        AppContext.TryGetSwitch(
            "System.Runtime.EnablePreviewFeatures", out bool on) && on;
}
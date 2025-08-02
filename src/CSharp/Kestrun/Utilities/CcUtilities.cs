namespace Kestrun.Utilities;
/// <summary>
/// Provides utility methods for Kestrun.
/// </summary>
public static class CcUtilities
{
    /// <summary>
    /// Determines whether preview features are enabled in the current AppContext.
    /// </summary>
    public static bool PreviewFeaturesEnabled() =>
        AppContext.TryGetSwitch(
            "System.Runtime.EnablePreviewFeatures", out bool on) && on;
}
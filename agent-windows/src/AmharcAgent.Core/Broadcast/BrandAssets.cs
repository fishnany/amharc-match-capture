namespace AmharcAgent.Core.Broadcast;

/// <summary>
/// Canonical AMHARC brand asset identifiers. Consumers load these files unchanged;
/// the logo must never be reconstructed from text, CSS, SVG primitives or generated artwork.
/// </summary>
public static class BrandAssets
{
    public const string PrimaryLogo = "/branding/amharc-logo-transparent.png";
    public const string DarkBackgroundLogo = "/branding/amharc-app-icon.png";

    public static readonly BroadcastTheme DefaultTheme = new(
        Id: "amharc-default-v1",
        Black: "#000000",
        Green: "#1C8551",
        Lime: "#B6DC46",
        White: "#FFFFFF",
        PrimaryLogoAsset: PrimaryLogo,
        DarkBackgroundLogoAsset: DarkBackgroundLogo);
}

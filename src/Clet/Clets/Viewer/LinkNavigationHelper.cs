using Terminal.Gui.Views;

namespace Clet;

/// <summary>
/// Shared link-click handling logic for viewer clets. Provides the common skeleton:
/// custom-scheme dispatch → optional URL opening (http/https only) → status-bar update → Handled=true.
/// </summary>
internal static class LinkNavigationHelper
{
    /// <summary>
    /// Handles a <see cref="MarkdownLinkEventArgs"/> with the standard viewer pattern.
    /// </summary>
    /// <param name="e">The link-clicked event args.</param>
    /// <param name="customSchemeHandler">
    /// Optional handler for custom URL schemes (e.g. <c>clet:help</c>).
    /// Return <c>true</c> if the link was handled by this delegate (short-circuits further processing).
    /// </param>
    /// <param name="openHttpLinks">
    /// When <c>true</c>, http:// and https:// links are opened in the default browser.
    /// When <c>false</c>, they are only displayed in the status bar (SurfaceOnly policy).
    /// </param>
    /// <param name="statusUpdater">
    /// Callback to update the status bar with the URL. Called for all links not handled by <paramref name="customSchemeHandler"/>.
    /// </param>
    public static void HandleLinkClicked (
        MarkdownLinkEventArgs e,
        Func<string, bool>? customSchemeHandler,
        bool openHttpLinks,
        Action<string> statusUpdater)
    {
        // Step 1: Custom scheme dispatch
        if (customSchemeHandler is not null && customSchemeHandler (e.Url))
        {
            e.Handled = true;

            return;
        }

        // Step 2: Open http/https links if allowed
        if (openHttpLinks && IsHttpUrl (e.Url))
        {
            Link.OpenUrl (e.Url);
        }

        // Step 3: Update status bar
        statusUpdater (e.Url);

        // Step 4: Mark handled
        e.Handled = true;
    }

    /// <summary>
    /// Returns <c>true</c> if the URL starts with http:// or https://.
    /// </summary>
    internal static bool IsHttpUrl (string url) =>
        url.StartsWith ("http://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith ("https://", StringComparison.OrdinalIgnoreCase);
}

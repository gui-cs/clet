using Xunit;

namespace Clet.UnitTests;

public class LinkNavigationHelperTests
{
    [Theory]
    [InlineData ("http://example.com", true)]
    [InlineData ("https://example.com", true)]
    [InlineData ("HTTP://EXAMPLE.COM", true)]
    [InlineData ("HTTPS://EXAMPLE.COM", true)]
    [InlineData ("file:///etc/passwd", false)]
    [InlineData ("clet:help:select", false)]
    [InlineData ("ftp://example.com", false)]
    [InlineData ("relative/path.md", false)]
    public void IsHttpUrl_ClassifiesCorrectly (string url, bool expected)
    {
        Assert.Equal (expected, LinkNavigationHelper.IsHttpUrl (url));
    }

    [Fact]
    public void HandleLinkClicked_CustomSchemeHandler_ReturnsTrue_SetsHandled ()
    {
        var args = CreateArgs ("clet:help:select");
        string? statusUrl = null;

        LinkNavigationHelper.HandleLinkClicked (
            args,
            url => url.StartsWith ("clet:", StringComparison.OrdinalIgnoreCase),
            openHttpLinks: true,
            url => statusUrl = url);

        Assert.True (args.Handled);
        Assert.Null (statusUrl); // status updater not called when custom handler handles it
    }

    [Fact]
    public void HandleLinkClicked_CustomSchemeHandler_ReturnsFalse_FallsThrough ()
    {
        var args = CreateArgs ("https://example.com");
        string? statusUrl = null;

        LinkNavigationHelper.HandleLinkClicked (
            args,
            url => url.StartsWith ("clet:", StringComparison.OrdinalIgnoreCase),
            openHttpLinks: false,
            url => statusUrl = url);

        Assert.True (args.Handled);
        Assert.Equal ("https://example.com", statusUrl);
    }

    [Fact]
    public void HandleLinkClicked_NoCustomHandler_UpdatesStatus ()
    {
        var args = CreateArgs ("file:///etc/passwd");
        string? statusUrl = null;

        LinkNavigationHelper.HandleLinkClicked (
            args,
            customSchemeHandler: null,
            openHttpLinks: false,
            url => statusUrl = url);

        Assert.True (args.Handled);
        Assert.Equal ("file:///etc/passwd", statusUrl);
    }

    [Fact]
    public void HandleLinkClicked_HttpLink_OpenHttpLinksTrue_UpdatesStatus ()
    {
        // We can't easily test that Link.OpenUrl is called, but we can verify
        // that the status updater is always called and Handled is set.
        var args = CreateArgs ("https://example.com");
        string? statusUrl = null;

        LinkNavigationHelper.HandleLinkClicked (
            args,
            customSchemeHandler: null,
            openHttpLinks: true,
            url => statusUrl = url);

        Assert.True (args.Handled);
        Assert.Equal ("https://example.com", statusUrl);
    }

    private static Terminal.Gui.Views.MarkdownLinkEventArgs CreateArgs (string url)
    {
        return new Terminal.Gui.Views.MarkdownLinkEventArgs (url);
    }
}

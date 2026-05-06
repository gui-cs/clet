using Xunit;

namespace Clet.UnitTests;

public class TerminalEscapeSanitizerTests
{
    /// <summary>Helper that asserts no dangerous terminal control bytes remain in the string.</summary>
    private static void AssertEscapeFree (string result)
    {
        foreach (char c in result)
        {
            Assert.False (c == '\x1b', $"Result contains ESC (U+001B). Hex dump: {HexDump (result)}");
            Assert.False (c == '\x07', $"Result contains BEL (U+0007). Hex dump: {HexDump (result)}");
            Assert.False (c == '\u009b', $"Result contains 8-bit CSI (U+009B). Hex dump: {HexDump (result)}");
            Assert.False (c == '\u009d', $"Result contains 8-bit OSC (U+009D). Hex dump: {HexDump (result)}");
        }
    }

    private static string HexDump (string s) =>
        string.Join (" ", s.Select (c => $"U+{(int)c:X4}"));

    // --- Sanitize (input filtering) ---

    [Fact]
    public void Sanitize_Null_ReturnsNull ()
    {
        Assert.Null (TerminalEscapeSanitizer.Sanitize (null));
    }

    [Fact]
    public void Sanitize_Empty_ReturnsEmpty ()
    {
        Assert.Equal (string.Empty, TerminalEscapeSanitizer.Sanitize (string.Empty));
    }

    [Fact]
    public void Sanitize_CleanString_Unchanged ()
    {
        const string input = "Hello, world! This is normal markdown.";

        Assert.Equal (input, TerminalEscapeSanitizer.Sanitize (input));
    }

    [Fact]
    public void Sanitize_Osc52ClipboardHijack_Stripped ()
    {
        // OSC 52 clipboard write: ESC ] 52;c;<base64> BEL
        string payload = "\x1b]52;c;SGVsbG8=\x07";
        string result = TerminalEscapeSanitizer.Sanitize (payload)!;

        AssertEscapeFree (result);
    }

    [Fact]
    public void Sanitize_Osc0WindowTitle_Stripped ()
    {
        // OSC 0 window title: ESC ] 0;evil BEL
        string payload = "\x1b]0;evil\x07";
        string result = TerminalEscapeSanitizer.Sanitize (payload)!;

        AssertEscapeFree (result);
    }

    [Fact]
    public void Sanitize_Osc2WindowTitle_Stripped ()
    {
        // OSC 2 window title: ESC ] 2;evil BEL
        string payload = "\x1b]2;evil\x07";
        string result = TerminalEscapeSanitizer.Sanitize (payload)!;

        AssertEscapeFree (result);
    }

    [Fact]
    public void Sanitize_Osc8LinkSpoofing_Stripped ()
    {
        // OSC 8 hyperlink: ESC ] 8;;https://evil/ BEL click ESC ] 8;; BEL
        string payload = "\x1b]8;;https://evil/" + "\x07" + "click\x1b]8;;\x07";
        string result = TerminalEscapeSanitizer.Sanitize (payload)!;

        AssertEscapeFree (result);
        Assert.Contains ("click", result);
    }

    [Fact]
    public void Sanitize_ClearScreenHome_Stripped ()
    {
        // Clear-screen + home: ESC [ 2J ESC [ H
        string payload = "\x1b[2J\x1b[H";
        string result = TerminalEscapeSanitizer.Sanitize (payload)!;

        AssertEscapeFree (result);
    }

    [Fact]
    public void Sanitize_8BitCsi_Stripped ()
    {
        string payload = "before" + "\u009b" + "after";
        string result = TerminalEscapeSanitizer.Sanitize (payload)!;

        AssertEscapeFree (result);
        Assert.Contains ("before", result);
        Assert.Contains ("after", result);
    }

    [Fact]
    public void Sanitize_8BitOsc_Stripped ()
    {
        string payload = "before" + "\u009d" + "after";
        string result = TerminalEscapeSanitizer.Sanitize (payload)!;

        AssertEscapeFree (result);
        Assert.Contains ("before", result);
        Assert.Contains ("after", result);
    }

    [Fact]
    public void Sanitize_Bel_Stripped ()
    {
        string payload = "alert" + "\x07" + "here";
        string result = TerminalEscapeSanitizer.Sanitize (payload)!;

        AssertEscapeFree (result);
        Assert.Equal ("alerthere", result);
    }

    [Fact]
    public void Sanitize_C1SevenBitPairs_Stripped ()
    {
        // C1 7-bit pairs: ESC @ through ESC _
        string payload = "a\x1b@b\x1b]c\x1b_d";
        string result = TerminalEscapeSanitizer.Sanitize (payload)!;

        AssertEscapeFree (result);
        Assert.Equal ("abcd", result);
    }

    [Fact]
    public void Sanitize_MixedPayloads_AllStripped ()
    {
        // All payloads from the issue combined with normal text
        string input = "# Title\n\x1b]52;c;SGVsbG8=" + "\x07" + "\nParagraph\x1b]0;evil\x07\n"
                       + "\x1b]8;;https://evil/" + "\x07" + "link\x1b]8;;\x07\n"
                       + "\x1b[2J\x1b[H" + "\u009b" + "0m" + "\u009d" + "0;bad\x07";
        string result = TerminalEscapeSanitizer.Sanitize (input)!;

        AssertEscapeFree (result);
        Assert.Contains ("# Title", result);
        Assert.Contains ("Paragraph", result);
        Assert.Contains ("link", result);
    }

    // --- SanitizeRenderedOutput (output filtering) ---

    [Fact]
    public void SanitizeRenderedOutput_PreservesSgrSequences ()
    {
        // SGR bold + red: ESC[1m ESC[31m
        string input = "\x1b[1mBold\x1b[31mRed\x1b[0m";
        string result = TerminalEscapeSanitizer.SanitizeRenderedOutput (input);

        Assert.Equal (input, result);
    }

    [Fact]
    public void SanitizeRenderedOutput_PreservesCursorSequences ()
    {
        // Cursor movement: ESC[H, ESC[2J
        string input = "\x1b[H\x1b[2J";
        string result = TerminalEscapeSanitizer.SanitizeRenderedOutput (input);

        Assert.Equal (input, result);
    }

    [Fact]
    public void SanitizeRenderedOutput_StripsOsc ()
    {
        // OSC sequence (ESC ]) should be stripped from rendered output
        string input = "text\x1b]0;evil\x07more";
        string result = TerminalEscapeSanitizer.SanitizeRenderedOutput (input);

        Assert.False (result.Contains ('\x07'), "Result contains BEL");
        Assert.Contains ("text", result);
        Assert.Contains ("more", result);
    }

    [Fact]
    public void SanitizeRenderedOutput_Strips8BitCsi ()
    {
        string input = "text" + "\u009b" + "0mmore";
        string result = TerminalEscapeSanitizer.SanitizeRenderedOutput (input);

        Assert.False (result.Contains ('\u009b'), "Result contains 8-bit CSI");
    }

    [Fact]
    public void SanitizeRenderedOutput_Strips8BitOsc ()
    {
        string input = "text" + "\u009d" + "0;evil\x07more";
        string result = TerminalEscapeSanitizer.SanitizeRenderedOutput (input);

        Assert.False (result.Contains ('\u009d'), "Result contains 8-bit OSC");
        Assert.False (result.Contains ('\x07'), "Result contains BEL");
    }

    [Fact]
    public void SanitizeRenderedOutput_StripsBel ()
    {
        string input = "alert" + "\x07" + "here";
        string result = TerminalEscapeSanitizer.SanitizeRenderedOutput (input);

        Assert.False (result.Contains ('\x07'), "Result contains BEL");
        Assert.Equal ("alerthere", result);
    }

    [Fact]
    public void SanitizeRenderedOutput_Empty_ReturnsEmpty ()
    {
        Assert.Equal (string.Empty, TerminalEscapeSanitizer.SanitizeRenderedOutput (string.Empty));
    }

    [Fact]
    public void SanitizeRenderedOutput_Null_ReturnsEmpty ()
    {
        Assert.Equal (string.Empty, TerminalEscapeSanitizer.SanitizeRenderedOutput (null!));
    }

    [Fact]
    public void SanitizeRenderedOutput_MixedLegitAndDangerous ()
    {
        // Mix of legitimate SGR and dangerous OSC
        string input = "\x1b[1mBold\x1b]0;evil\x07Normal\x1b[0m";
        string result = TerminalEscapeSanitizer.SanitizeRenderedOutput (input);

        Assert.Contains ("\x1b[1m", result); // SGR preserved
        Assert.Contains ("\x1b[0m", result); // SGR reset preserved
        Assert.False (result.Contains ('\x07'), "Result contains BEL");
        Assert.Contains ("Bold", result);
        Assert.Contains ("Normal", result);
    }
}

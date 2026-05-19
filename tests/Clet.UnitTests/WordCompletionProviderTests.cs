using Terminal.Gui.Document;
using Terminal.Gui.Input;
using Xunit;

namespace Clet.UnitTests;

public class WordCompletionProviderTests
{
    [Fact]
    public void GetCompletions_ReturnsUniqueMatchingWords ()
    {
        WordCompletionProvider provider = new ();
        TextDocument document = new ("alpha alphabet AlphaBeta beta alphabet");

        IReadOnlyList<Terminal.Gui.Editor.Completion.CompletionItem> completions =
            provider.GetCompletions (document, 0, "alph");

        Assert.Equal (["alpha", "alphabet", "AlphaBeta"], completions.Select (c => c.Label));
    }

    [Fact]
    public void ShouldTrigger_OnlyOnCtrlSpace ()
    {
        WordCompletionProvider provider = new ();

        Assert.True (provider.ShouldTrigger (Key.Space.WithCtrl));
        Assert.False (provider.ShouldTrigger (Key.Space));
    }
}

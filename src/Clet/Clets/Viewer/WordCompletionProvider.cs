using Terminal.Gui.Document;
using Terminal.Gui.Editor.Completion;
using Terminal.Gui.Input;

namespace Clet;

/// <summary>
/// Provides word completions from the current editor document.
/// </summary>
internal sealed class WordCompletionProvider : IEditorCompletionProvider
{
    public IReadOnlyList<CompletionItem> GetCompletions (TextDocument document, int caretOffset, string prefix)
    {
        if (string.IsNullOrEmpty (prefix))
        {
            return [];
        }

        string text = document.Text;
        HashSet<string> seen = new (StringComparer.OrdinalIgnoreCase);
        List<CompletionItem> results = [];
        int i = 0;

        while (i < text.Length)
        {
            if (!IsWordChar (text[i]))
            {
                i++;

                continue;
            }

            int start = i;

            while (i < text.Length && IsWordChar (text[i]))
            {
                i++;
            }

            string word = text[start..i];

            if (word.Length <= prefix.Length || string.Equals (word, prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (word.StartsWith (prefix, StringComparison.OrdinalIgnoreCase) && seen.Add (word))
            {
                results.Add (new CompletionItem { Label = word });
            }
        }

        results.Sort ((a, b) => string.Compare (a.Label, b.Label, StringComparison.OrdinalIgnoreCase));

        return results;
    }

    public bool ShouldTrigger (Key key) => key == Key.Space.WithCtrl;

    private static bool IsWordChar (char ch) => char.IsLetterOrDigit (ch) || ch == '_';
}

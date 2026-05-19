using System.Reflection;
using System.Text;
using Terminal.Gui.Document;
using Terminal.Gui.Editor;
using Xunit;

namespace Clet.UnitTests;

public class EditorLoadApiTests
{
    [Fact]
    public void TerminalGuiEditor_ExposesProgressiveLoadAsyncApi ()
    {
        MethodInfo? method = typeof (Editor)
            .GetMethods ()
            .SingleOrDefault (m =>
                m.Name == nameof (Editor.LoadAsync)
                && m.GetParameters () is
                [
                { ParameterType: { } streamType },
                { ParameterType: { } encodingType },
                { ParameterType: { } progressType },
                { ParameterType: { } cancellationTokenType },
                { ParameterType: { } marshalType },
                ]
                && streamType == typeof (Stream)
                && encodingType == typeof (Encoding)
                && progressType == typeof (IProgress<TextDocumentProgress>)
                && cancellationTokenType == typeof (CancellationToken)
                && marshalType == typeof (Func<Action, Task>));

        Assert.NotNull (method);
    }
}

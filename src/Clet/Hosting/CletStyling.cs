using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;

namespace Clet;

internal static class CletStyling
{
    public static string BaseSchemeName => SchemeManager.SchemesToSchemeName (Schemes.Base)!;
}

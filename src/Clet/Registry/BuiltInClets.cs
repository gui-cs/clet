namespace Clet;

internal static class BuiltInClets
{
    public static void RegisterAll (ICletRegistry registry)
    {
        registry.Register (new SelectClet ());
    }
}

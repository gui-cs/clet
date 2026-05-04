namespace Clet;

internal interface ICletRegistry
{
    void Register (IClet clet);
    bool TryResolve (string alias, out IClet? clet);
    IReadOnlyCollection<IClet> All { get; }
}

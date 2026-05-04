namespace Clet;

internal sealed class CletRegistry : ICletRegistry
{
    private readonly Dictionary<string, IClet> _byAlias = new (StringComparer.OrdinalIgnoreCase);
    private readonly List<IClet> _all = [];

    public void Register (IClet clet)
    {
        foreach (string alias in clet.Aliases)
        {
            if (!_byAlias.TryAdd (alias, clet))
            {
                throw new InvalidOperationException ($"Duplicate alias '{alias}' is already registered.");
            }
        }

        _all.Add (clet);
    }

    public bool TryResolve (string alias, out IClet? clet)
    {
        return _byAlias.TryGetValue (alias, out clet);
    }

    public IReadOnlyCollection<IClet> All => _all.AsReadOnly ();
}

using System;
using System.Collections.Generic;

public sealed class TowerRunGate
{
    private readonly Dictionary<string, int> owners =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public event Action<bool> Changed;
    public bool IsBlocked => owners.Count > 0;

    public TowerRunGateLease Acquire(string owner)
    {
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("A gate owner is required.", nameof(owner));

        bool wasBlocked = IsBlocked;
        owners.TryGetValue(owner, out int count);
        owners[owner] = count + 1;

        if (wasBlocked != IsBlocked)
            Changed?.Invoke(IsBlocked);

        return new TowerRunGateLease(this, owner);
    }

    public bool IsBlockedByOtherThan(string owner)
    {
        if (!IsBlocked)
            return false;

        foreach (KeyValuePair<string, int> pair in owners)
        {
            if (pair.Value > 0 && !string.Equals(pair.Key, owner, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    internal void Release(string owner)
    {
        if (!owners.TryGetValue(owner, out int count))
            return;

        bool wasBlocked = IsBlocked;

        if (count <= 1)
            owners.Remove(owner);
        else
            owners[owner] = count - 1;

        if (wasBlocked != IsBlocked)
            Changed?.Invoke(IsBlocked);
    }

    public void Clear()
    {
        if (!IsBlocked)
            return;

        owners.Clear();
        Changed?.Invoke(false);
    }
}

public sealed class TowerRunGateLease : IDisposable
{
    private TowerRunGate gate;
    private readonly string owner;

    internal TowerRunGateLease(TowerRunGate gate, string owner)
    {
        this.gate = gate;
        this.owner = owner;
    }

    public void Dispose()
    {
        TowerRunGate current = gate;

        if (current == null)
            return;

        gate = null;
        current.Release(owner);
    }
}

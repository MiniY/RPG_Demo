using System;

public enum RewardSettlementKind
{
    Rejected,
    Inventory,
    Health,
    Mana
}

/// <summary>One committed ground settlement. Observers cannot turn it back into a rejection.</summary>
public readonly struct RewardPickupResult
{
    public RewardSettlementKind Kind { get; }
    public int AcceptedAmount { get; }
    public float RestoredValue { get; }
    public bool Accepted => AcceptedAmount > 0;

    private RewardPickupResult(RewardSettlementKind kind, int amount, float restored)
    {
        Kind = kind;
        AcceptedAmount = amount;
        RestoredValue = restored;
    }

    public static RewardPickupResult IntoInventory(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        return new RewardPickupResult(RewardSettlementKind.Inventory, amount, 0f);
    }

    public static RewardPickupResult Recovered(PlayerStatType stat, float restored)
    {
        if ((stat != PlayerStatType.Health && stat != PlayerStatType.Mana) ||
            restored <= 0f || float.IsNaN(restored) || float.IsInfinity(restored))
            throw new ArgumentOutOfRangeException(nameof(restored));
        return new RewardPickupResult(stat == PlayerStatType.Health ? RewardSettlementKind.Health : RewardSettlementKind.Mana, 1, restored);
    }
}

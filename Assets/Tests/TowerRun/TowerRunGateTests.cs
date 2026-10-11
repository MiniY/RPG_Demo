using NUnit.Framework;

public class TowerRunGateTests
{
    [Test]
    public void MultipleOwners_ReleaseIndependentlyWithoutLastWriterWins()
    {
        TowerRunGate gate = new TowerRunGate();
        TowerRunGateLease package = gate.Acquire("package");
        TowerRunGateLease death = gate.Acquire("death");

        package.Dispose();
        Assert.That(gate.IsBlocked, Is.True);
        Assert.That(gate.IsBlockedByOtherThan("package"), Is.True);

        death.Dispose();
        Assert.That(gate.IsBlocked, Is.False);
    }

    [Test]
    public void DuplicateOwnerLeases_AreReferenceCounted()
    {
        TowerRunGate gate = new TowerRunGate();
        TowerRunGateLease first = gate.Acquire("transition");
        TowerRunGateLease second = gate.Acquire("transition");

        first.Dispose();
        Assert.That(gate.IsBlocked, Is.True);

        second.Dispose();
        Assert.That(gate.IsBlocked, Is.False);
    }
}

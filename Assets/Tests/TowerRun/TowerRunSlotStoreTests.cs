using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

public class TowerRunSlotStoreTests
{
    [Test]
    public void CommitAndRead_ProducesAnIsolatedValidSlot()
    {
        string root = Path.Combine(Path.GetTempPath(), "RPG_Demo_TowerRunTests", Guid.NewGuid().ToString("N"));

        try
        {
            TowerRunCompatibilityPolicy policy = new TowerRunCompatibilityPolicy(
                TowerRunCheckpointRecord.CurrentSchemaVersion,
                generatorVersion: 1,
                configurationIdentity: "random-map-default-v1");
            TowerRunSlotStore store = new TowerRunSlotStore(root, policy);
            TowerRunCheckpointRecord source = CreateValidRecord();

            TowerRunSlotWriteResult write = store.TryCommit(2, source, overwriteConfirmed: false);
            source.player.inventory[0].amount = 999;

            TowerRunSlotReadResult firstRead = store.ReadSlot(2);
            firstRead.record.player.inventory[0].amount = 555;
            TowerRunSlotReadResult secondRead = store.ReadSlot(2);

            Assert.That(write.succeeded, Is.True, write.message);
            Assert.That(firstRead.state, Is.EqualTo(TowerRunSlotState.Valid));
            Assert.That(secondRead.state, Is.EqualTo(TowerRunSlotState.Valid));
            Assert.That(secondRead.record.player.inventory[0].amount, Is.EqualTo(12));
            Assert.That(secondRead.summary.slotNumber, Is.EqualTo(2));
            Assert.That(secondRead.summary.playerName, Is.EqualTo("玩家"));
            Assert.That(secondRead.summary.floorDisplayName, Is.EqualTo("第 1 层"));
            Assert.That(secondRead.summary.currentHealth, Is.EqualTo(40f));
            Assert.That(secondRead.summary.maxHealth, Is.EqualTo(100));
            Assert.That(secondRead.summary.savedAtUtc, Is.Not.Empty);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public void ReadSlot_ClassifiesEmptyCorruptAndIncompatibleRecords()
    {
        string root = CreateTemporaryRoot();

        try
        {
            TowerRunCompatibilityPolicy policy = CreatePolicy();
            TowerRunSlotStore store = new TowerRunSlotStore(root, policy);

            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "tower-run-slot-2.json"), "not-json");

            TowerRunCheckpointRecord incompatible = CreateValidRecord();
            incompatible.generation.generatorVersion = 99;
            File.WriteAllText(
                Path.Combine(root, "tower-run-slot-3.json"),
                UnityEngine.JsonUtility.ToJson(incompatible));

            Assert.That(store.ReadSlot(1).state, Is.EqualTo(TowerRunSlotState.Empty));
            Assert.That(store.ReadSlot(2).state, Is.EqualTo(TowerRunSlotState.Corrupt));
            Assert.That(store.ReadSlot(3).state, Is.EqualTo(TowerRunSlotState.Incompatible));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Test]
    public void Compatibility_RejectsNonFiniteOrOutOfRangeStats_AndClassifiesFutureSchemaFirst()
    {
        TowerRunCompatibilityPolicy policy = CreatePolicy();
        TowerRunCheckpointRecord invalidStats = CreateValidRecord();
        invalidStats.player.stats.currentHealth = float.NaN;

        Assert.That(
            policy.Evaluate(invalidStats, out _),
            Is.EqualTo(TowerRunSlotState.Corrupt));

        invalidStats = CreateValidRecord();
        invalidStats.player.stats.maxHealth = 1000;

        Assert.That(
            policy.Evaluate(invalidStats, out _),
            Is.EqualTo(TowerRunSlotState.Corrupt));

        TowerRunCheckpointRecord future = new TowerRunCheckpointRecord { schemaVersion = 999 };
        Assert.That(
            policy.Evaluate(future, out _),
            Is.EqualTo(TowerRunSlotState.Incompatible));
    }

    [Test]
    public void ReadSlot_WhenPersistedGenerationInputIsMissing_ClassifiesRecordAsCorrupt()
    {
        string root = CreateTemporaryRoot();

        try
        {
            TowerRunSlotStore store = new TowerRunSlotStore(root, CreatePolicy());
            Assert.That(store.TryCommit(1, CreateValidRecord(), false).succeeded, Is.True);

            string path = Path.Combine(root, "tower-run-slot-1.json");
            string incomplete = File.ReadAllText(path).Replace("\"requestedSeed\": 12345,", string.Empty);
            int rootEnd = incomplete.LastIndexOf('}');
            incomplete = incomplete.Insert(rootEnd, ",\n  \"requestedSeed\": 12345\n");
            File.WriteAllText(path, incomplete);

            Assert.That(store.ReadSlot(1).state, Is.EqualTo(TowerRunSlotState.Corrupt));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Test]
    public void Commit_RequiresConfirmationAndKeepsAllThreeSlotsIndependent()
    {
        string root = CreateTemporaryRoot();

        try
        {
            TowerRunSlotStore store = new TowerRunSlotStore(root, CreatePolicy());

            for (int slot = 1; slot <= 3; slot++)
            {
                TowerRunCheckpointRecord record = CreateValidRecord();
                record.runId = $"run-{slot}";
                record.floor.floorNumber = slot;
                Assert.That(store.TryCommit(slot, record, false).succeeded, Is.True);
            }

            TowerRunCheckpointRecord replacement = CreateValidRecord();
            replacement.runId = "replacement";
            TowerRunSlotWriteResult rejected = store.TryCommit(2, replacement, false);

            Assert.That(rejected.succeeded, Is.False);
            Assert.That(rejected.requiresOverwriteConfirmation, Is.True);
            Assert.That(store.ReadSlot(1).record.runId, Is.EqualTo("run-1"));
            Assert.That(store.ReadSlot(2).record.runId, Is.EqualTo("run-2"));
            Assert.That(store.ReadSlot(3).record.runId, Is.EqualTo("run-3"));

            string confirmationToken = store.ReadSlot(2).confirmationToken;
            Assert.That(store.TryCommit(2, replacement, true, confirmationToken).succeeded, Is.True);
            Assert.That(store.ReadSlot(2).record.runId, Is.EqualTo("replacement"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Test]
    public void Commit_WhenFinalVerificationFails_RestoresPreviousFormalRecord()
    {
        string root = CreateTemporaryRoot();

        try
        {
            FailFinalVerificationFileSystem fileSystem = new FailFinalVerificationFileSystem();
            TowerRunSlotStore store = new TowerRunSlotStore(root, CreatePolicy(), fileSystem);
            TowerRunCheckpointRecord original = CreateValidRecord();
            original.runId = "original";
            Assert.That(store.TryCommit(1, original, false).succeeded, Is.True);

            TowerRunCheckpointRecord replacement = CreateValidRecord();
            replacement.runId = "replacement";
            fileSystem.FailNextFormalReadAfterReplace = true;

            string confirmationToken = store.ReadSlot(1).confirmationToken;
            TowerRunSlotWriteResult result = store.TryCommit(1, replacement, true, confirmationToken);

            Assert.That(result.succeeded, Is.False);
            Assert.That(store.ReadSlot(1).state, Is.EqualTo(TowerRunSlotState.Valid));
            Assert.That(store.ReadSlot(1).record.runId, Is.EqualTo("original"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Test]
    public void Commit_WhenSlotChangesAfterConfirmation_RequiresFreshConfirmation()
    {
        string root = CreateTemporaryRoot();

        try
        {
            TowerRunSlotStore store = new TowerRunSlotStore(root, CreatePolicy());
            Assert.That(store.TryCommit(1, CreateValidRecord(), false).succeeded, Is.True);
            string staleToken = store.ReadSlot(1).confirmationToken;

            TowerRunCheckpointRecord concurrent = CreateValidRecord();
            concurrent.runId = "concurrent";
            concurrent.floor.floorNumber = 2;
            concurrent.floor.displayName = "第 2 层";
            concurrent.savedAtUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(
                Path.Combine(root, "tower-run-slot-1.json"),
                UnityEngine.JsonUtility.ToJson(concurrent, true));

            TowerRunCheckpointRecord replacement = CreateValidRecord();
            replacement.runId = "replacement";
            TowerRunSlotWriteResult result = store.TryCommit(1, replacement, true, staleToken);

            Assert.That(result.succeeded, Is.False);
            Assert.That(result.requiresOverwriteConfirmation, Is.True);
            Assert.That(store.ReadSlot(1).record.runId, Is.EqualTo("concurrent"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Test]
    public void ReadSlot_WhenCorruptContentsChange_ChangesConfirmationToken()
    {
        string root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "tower-run-slot-1.json");
            TowerRunSlotStore store = new TowerRunSlotStore(root, CreatePolicy());

            File.WriteAllText(path, "{ corrupt-a }");
            TowerRunSlotReadResult first = store.ReadSlot(1);
            File.WriteAllText(path, "{ corrupt-b }");
            TowerRunSlotReadResult second = store.ReadSlot(1);

            Assert.That(first.state, Is.EqualTo(TowerRunSlotState.Corrupt));
            Assert.That(second.state, Is.EqualTo(TowerRunSlotState.Corrupt));
            Assert.That(first.confirmationToken, Is.Not.Null.And.Not.Empty);
            Assert.That(second.confirmationToken, Is.Not.EqualTo(first.confirmationToken));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Test]
    public void ReadSlot_WhenFileProbeIsUnavailable_DoesNotReportEmpty()
    {
        TowerRunSlotStore store = new TowerRunSlotStore(
            CreateTemporaryRoot(),
            CreatePolicy(),
            new UnavailableFileSystem());

        Assert.That(store.ReadSlot(1).state, Is.EqualTo(TowerRunSlotState.Unavailable));
    }

    [Test]
    public void Commit_WhenFinalVerificationAndRollbackFail_RequiresVerification()
    {
        string root = CreateTemporaryRoot();

        try
        {
            FailFinalVerificationFileSystem fileSystem = new FailFinalVerificationFileSystem();
            TowerRunSlotStore store = new TowerRunSlotStore(root, CreatePolicy(), fileSystem);
            Assert.That(store.TryCommit(1, CreateValidRecord(), false).succeeded, Is.True);
            string confirmationToken = store.ReadSlot(1).confirmationToken;

            fileSystem.FailNextFormalReadAfterReplace = true;
            fileSystem.FailRollbackReplace = true;
            TowerRunCheckpointRecord replacement = CreateValidRecord();
            replacement.runId = "replacement";
            TowerRunSlotWriteResult result = store.TryCommit(1, replacement, true, confirmationToken);

            Assert.That(result.succeeded, Is.False);
            Assert.That(result.requiresVerification, Is.True);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Test]
    public void PhysicalFileSystem_WhenNativeReplaceIsUnsupported_FailsClosed()
    {
        string root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(root);
            string source = Path.Combine(root, "source.tmp");
            string destination = Path.Combine(root, "destination.json");
            string backup = Path.Combine(root, "destination.backup.json");
            File.WriteAllText(source, "new");
            File.WriteAllText(destination, "old");
            TowerRunPhysicalFileSystem fileSystem = new TowerRunPhysicalFileSystem(
                new UnsupportedNativeFileReplacer());

            Assert.Throws<PlatformNotSupportedException>(() =>
                fileSystem.ReplaceFile(source, destination, backup));

            Assert.That(File.ReadAllText(destination), Is.EqualTo("old"));
            Assert.That(File.ReadAllText(source), Is.EqualTo("new"));
            Assert.That(File.Exists(backup), Is.False);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestCase(0)]
    [TestCase(4)]
    public void ReadSlot_RejectsNumbersOutsideTheThreeFormalSlots(int slotNumber)
    {
        TowerRunSlotStore store = new TowerRunSlotStore(CreateTemporaryRoot(), CreatePolicy());
        Assert.Throws<ArgumentOutOfRangeException>(() => store.ReadSlot(slotNumber));
    }

    private static TowerRunCompatibilityPolicy CreatePolicy()
    {
        return new TowerRunCompatibilityPolicy(
            TowerRunCheckpointRecord.CurrentSchemaVersion,
            generatorVersion: 1,
            configurationIdentity: "random-map-default-v1");
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), "RPG_Demo_TowerRunTests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    private static TowerRunCheckpointRecord CreateValidRecord()
    {
        return new TowerRunCheckpointRecord
        {
            schemaVersion = TowerRunCheckpointRecord.CurrentSchemaVersion,
            runId = "run-a",
            playerName = "玩家",
            floor = new TowerRunFloorRecord
            {
                floorNumber = 1,
                displayName = "第 1 层"
            },
            generation = new TowerRunGenerationRecord
            {
                generatorVersion = 1,
                configurationIdentity = "random-map-default-v1",
                profileId = "default",
                requestedSeed = 12345,
                attemptIndex = 0,
                staticBaselineSignature = "baseline-a"
            },
            player = new TowerRunPlayerRecord
            {
                stats = new TowerRunPlayerStatsRecord
                {
                    maxHealth = 100,
                    currentHealth = 40f,
                    maxMana = 80,
                    currentMana = 20f,
                    maxStamina = 60,
                    currentStamina = 10f
                },
                inventory =
                {
                    new TowerRunInventoryEntry { rewardId = "gold", amount = 12 }
                }
            }
        };
    }

    private sealed class FailFinalVerificationFileSystem : ITowerRunFileSystem
    {
        private readonly TowerRunPhysicalFileSystem inner = new TowerRunPhysicalFileSystem();
        private readonly HashSet<string> replacedFormalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool FailNextFormalReadAfterReplace { get; set; }
        public bool FailRollbackReplace { get; set; }

        public TowerRunFileProbeState ProbeFile(string path) => inner.ProbeFile(path);
        public string ReadAllText(string path)
        {
            if (FailNextFormalReadAfterReplace && replacedFormalPaths.Remove(path))
            {
                FailNextFormalReadAfterReplace = false;
                throw new IOException("Injected final verification failure.");
            }

            return inner.ReadAllText(path);
        }

        public void WriteAllText(string path, string contents) => inner.WriteAllText(path, contents);
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public void MoveFile(string source, string destination) => inner.MoveFile(source, destination);
        public void DeleteFile(string path) => inner.DeleteFile(path);

        public void ReplaceFile(string source, string destination, string backup)
        {
            if (FailRollbackReplace && source.EndsWith(".backup", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Injected rollback failure.");

            inner.ReplaceFile(source, destination, backup);
            replacedFormalPaths.Add(destination);
        }
    }

    private sealed class UnavailableFileSystem : ITowerRunFileSystem
    {
        public TowerRunFileProbeState ProbeFile(string path) => TowerRunFileProbeState.Unavailable;
        public string ReadAllText(string path) => throw new IOException();
        public void WriteAllText(string path, string contents) => throw new IOException();
        public void CreateDirectory(string path) => throw new IOException();
        public void ReplaceFile(string source, string destination, string backup) => throw new IOException();
        public void MoveFile(string source, string destination) => throw new IOException();
        public void DeleteFile(string path) => throw new IOException();
    }

    private sealed class UnsupportedNativeFileReplacer : ITowerRunNativeFileReplacer
    {
        public void Replace(string source, string destination, string backup)
        {
            throw new PlatformNotSupportedException();
        }
    }

}

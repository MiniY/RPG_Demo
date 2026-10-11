using System;
using System.IO;
using NUnit.Framework;

public class LegacySaveDetectorTests
{
    [Test]
    public void Detect_UsesOnlyExistenceMetadataAndNeverReadsLegacyContents()
    {
        MetadataOnlyFileProbe probe = new MetadataOnlyFileProbe
        {
            ExistsResult = true
        };
        LegacySaveDetector detector = new LegacySaveDetector(probe, "X:/isolated/save.json");

        LegacySaveDetectionResult result = detector.Detect();

        Assert.That(result.state, Is.EqualTo(LegacySaveState.Detected));
        Assert.That(probe.ExistenceChecks, Is.EqualTo(1));
    }

    [Test]
    public void Detect_WhenMetadataAccessFails_DoesNotBlockTheNewSlotSystem()
    {
        MetadataOnlyFileProbe probe = new MetadataOnlyFileProbe
        {
            Failure = new IOException("Injected metadata failure.")
        };
        LegacySaveDetector detector = new LegacySaveDetector(probe, "X:/isolated/save.json");

        LegacySaveDetectionResult result = detector.Detect();

        Assert.That(result.state, Is.EqualTo(LegacySaveState.Unavailable));
        Assert.That(result.isBlocking, Is.False);
    }

    private sealed class MetadataOnlyFileProbe : ILegacySaveFileProbe
    {
        public bool ExistsResult { get; set; }
        public Exception Failure { get; set; }
        public int ExistenceChecks { get; private set; }

        public bool FileExists(string path)
        {
            ExistenceChecks++;

            if (Failure != null)
                throw Failure;

            return ExistsResult;
        }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EcologyQueryEquivalencePlayModeTests
{
    [Test]
    public void ActualFootSweepIntervalsMatchExhaustiveOracleIncludingTangencyAndOffsets()
    {
        var native = new GameObject("OwnedQueryEquivalence");
        var body = native.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
        var circle = native.AddComponent<CircleCollider2D>(); circle.radius = .42f; circle.offset = new Vector2(.13f, .42f);
        var box = native.AddComponent<BoxCollider2D>(); box.size = new Vector2(.84f, .2f); box.offset = new Vector2(-.07f, .1f); box.enabled = false;
        MethodInfo actual = typeof(EcologyDamageBarrier).Assembly.GetType("EcologyProtectionGeometry").GetMethod("Intervals", BindingFlags.Static | BindingFlags.NonPublic);
        var expected = new List<Vector2>(); var result = new List<Vector2>(); var random = new System.Random(613580675);
        var regions = new Rect[96];
        for (int i = 0; i < regions.Length; i++) regions[i] = new Rect((i % 12) * 3f - 18f, (i / 12) * 3f - 12f, .75f + (i % 3), .5f + (i % 4));
        int compared = 0;
        try
        {
            foreach (Collider2D foot in new Collider2D[] { circle, box })
            foreach (float scale in new[] { 1f, -1f, 3.75f })
            {
                circle.enabled = foot == circle; box.enabled = foot == box; native.transform.localScale = Vector3.one * scale;
                body.position = new Vector2(3.25f, -2.5f); Physics2D.SyncTransforms();
                for (int i = 0; i < 420; i++)
                {
                    Vector2 start = i < 6 ? new Vector2(regions[0].xMin - .42f, regions[0].yMin + i * .25f) : new Vector2((float)random.NextDouble() * 50f - 25f, (float)random.NextDouble() * 40f - 20f);
                    Vector2 end = i % 5 == 0 ? start : start + new Vector2((float)random.NextDouble() * 36f - 18f, (float)random.NextDouble() * 36f - 18f);
                    bool expectedValid = EcologyProtectionGeometryExhaustiveOracle.Intervals(foot, start, end, regions, expected);
                    bool valid = (bool)actual.Invoke(null, new object[] { foot, start, end, regions, result });
                    Assert.That(valid, Is.EqualTo(expectedValid)); Assert.That(result, Is.EqualTo(expected), $"{foot.GetType().Name} scale={scale} i={i} start={start:R} end={end:R}"); compared++;
                }
            }
            native.transform.rotation = Quaternion.Euler(0, 0, 17f); Physics2D.SyncTransforms();
            Assert.That((bool)actual.Invoke(null, new object[] { box, Vector2.zero, Vector2.one, regions, result }), Is.False, "Unsupported feet remain faults.");
            Debug.Log("ECOLOGY_QUERY_EXHAUSTIVE_ORACLE actualNativeShapes=True sweepComparisons=" + compared + " rectanglesPerSweep=96 exactIntervals=True unsupportedShapeStillRejected=True");
        }
        finally { UnityEngine.Object.DestroyImmediate(native); }
    }
}
#endif

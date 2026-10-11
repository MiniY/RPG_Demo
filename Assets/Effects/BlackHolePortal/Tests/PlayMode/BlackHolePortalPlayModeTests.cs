#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BlackHolePortalPlayModeTests
{
    private const string PrefabPath = "Assets/Effects/BlackHolePortal/Prefabs/VerticalBlackHolePortal.prefab";
    private const int CaptureWidth = 1280;
    private const int CaptureHeight = 720;
    private string towerRunTestRoot;

    [SetUp]
    public void SetUpTowerRunIsolation()
    {
        towerRunTestRoot = Path.Combine(
            Path.GetTempPath(),
            "RPG_Demo-BlackHolePortalTests",
            System.Guid.NewGuid().ToString("N"));
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(towerRunTestRoot);
    }

    [TearDown]
    public void TearDownTowerRunIsolation()
    {
        TowerRunRuntimeEnvironment.ResetOverrides();

        if (Directory.Exists(towerRunTestRoot))
            Directory.Delete(towerRunTestRoot, true);
    }

    [UnityTest]
    public IEnumerator Portal_RendersInGameScene_AndManagesRuntimeState()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null, $"Missing prefab at {PrefabPath}");

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        while (!loadOperation.isDone)
            yield return null;

        yield return null;
        yield return null;
        yield return BeginFreshTowerRun();

        Camera gameCamera = Camera.main;
        Assert.That(gameCamera, Is.Not.Null, "SampleScene does not have a MainCamera.");

        Vector3 cameraPosition = gameCamera.transform.position;
        GameObject portalObject = Object.Instantiate(
            prefab,
            new Vector3(cameraPosition.x, cameraPosition.y, 0f),
            Quaternion.identity);
        portalObject.name = "Portal_PlayModeValidation";
        SetLayerRecursively(portalObject, 30);

        int originalCullingMask = gameCamera.cullingMask;
        CameraClearFlags originalClearFlags = gameCamera.clearFlags;
        Color originalBackgroundColor = gameCamera.backgroundColor;
        gameCamera.cullingMask = 1 << 30;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(0.055f, 0.085f, 0.07f, 1f);

        BlackHolePortalEffect effect = portalObject.GetComponent<BlackHolePortalEffect>();
        Assert.That(effect, Is.Not.Null);

        MeshRenderer bodyRenderer = portalObject.transform.Find("EnergyBody").GetComponent<MeshRenderer>();
        MeshRenderer coreRenderer = portalObject.transform.Find("Core").GetComponent<MeshRenderer>();
        ParticleSystem particles = portalObject.transform.Find("EdgeParticles").GetComponent<ParticleSystem>();

        Assert.That(bodyRenderer.sharedMaterial.shader.name, Is.EqualTo("RPGDemo/BlackHolePortal/Body"));
        Assert.That(coreRenderer.sharedMaterial.shader.name, Is.EqualTo("RPGDemo/BlackHolePortal/Core"));
        Assert.That(bodyRenderer.transform.localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(coreRenderer.transform.localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(effect.PortalHeight, Is.EqualTo(5.44f).Within(0.001f));
        Assert.That(effect.PortalWidth, Is.EqualTo(6.8f / 1.9f).Within(0.001f));
        Assert.That(effect.AspectRatio, Is.EqualTo(1.52f).Within(0.001f));
        Assert.That(effect.PortalHeight / effect.PortalWidth, Is.EqualTo(1.52f).Within(0.01f));
        Assert.That(
            bodyRenderer.transform.localPosition.y - effect.PortalHeight * 0.5f,
            Is.EqualTo(effect.GroundAnchorLocalY).Within(0.001f),
            "Changing the portal height moved the established ground anchor.");
        Assert.That(coreRenderer.transform.localPosition.y,
            Is.EqualTo(bodyRenderer.transform.localPosition.y).Within(0.001f));
        Assert.That(particles.transform.localPosition.y,
            Is.EqualTo(bodyRenderer.transform.localPosition.y).Within(0.001f));

        yield return new WaitForSeconds(1.0f);
        Assert.That(effect.ParticleCount, Is.GreaterThan(0), "Edge particles were not emitted.");
        Assert.That(effect.ParticleCount, Is.LessThanOrEqualTo(effect.ParticleLimit));
        ValidateParticleOutwardMotion(effect, particles);

        // Unity 2022.3 can crash in its native billboard renderer when Camera.Render is
        // forced from -nographics batch mode. Particle behavior is validated above;
        // frame captures intentionally isolate the deterministic Body/Core render path.
        particles.GetComponent<ParticleSystemRenderer>().enabled = false;

        string outputDirectory = GetOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        effect.Pause();
        Assert.That(effect.IsPlaying, Is.False);
        effect.Stop(true);
        effect.SetAnimationTime(0f);
        Object.Destroy(Capture(gameCamera, Path.Combine(outputDirectory, "Portal_GameScene_Flow_00.png")));
        effect.SetAnimationTime(0.35f);
        Object.Destroy(Capture(gameCamera, Path.Combine(outputDirectory, "Portal_GameScene_Flow_01.png")));
        effect.SetAnimationTime(0.70f);
        Object.Destroy(Capture(gameCamera, Path.Combine(outputDirectory, "Portal_GameScene_Flow_02.png")));

        effect.Play();
        yield return new WaitForSeconds(0.25f);
        Object.Destroy(Capture(gameCamera, Path.Combine(outputDirectory, "Portal_GameScene_Particles_00.png")));
        yield return new WaitForSeconds(0.25f);
        Object.Destroy(Capture(gameCamera, Path.Combine(outputDirectory, "Portal_GameScene_Particles_01.png")));

        for (int frame = 0; frame < 12; frame++)
        {
            yield return new WaitForSeconds(0.10f);
            string framePath = Path.Combine(outputDirectory, $"Portal_GameScene_Animation_{frame:00}.png");
            Object.Destroy(Capture(gameCamera, framePath));
        }

        gameCamera.cullingMask = originalCullingMask;
        gameCamera.clearFlags = originalClearFlags;
        gameCamera.backgroundColor = originalBackgroundColor;

        GameObject secondPortal = Object.Instantiate(prefab, portalObject.transform.position + Vector3.right * 4f, Quaternion.identity);
        BlackHolePortalEffect secondEffect = secondPortal.GetComponent<BlackHolePortalEffect>();
        SerializedObject secondSerializedEffect = new SerializedObject(secondEffect);
        secondSerializedEffect.FindProperty("mainDeepBlue").colorValue = new Color(0.12f, 0.005f, 0.16f, 1f);
        secondSerializedEffect.ApplyModifiedPropertiesWithoutUndo();
        secondEffect.Refresh();

        MeshRenderer secondBodyRenderer = secondPortal.transform.Find("EnergyBody").GetComponent<MeshRenderer>();
        Assert.That(secondBodyRenderer.sharedMaterial, Is.SameAs(bodyRenderer.sharedMaterial));

        MaterialPropertyBlock firstBlock = new MaterialPropertyBlock();
        MaterialPropertyBlock secondBlock = new MaterialPropertyBlock();
        bodyRenderer.GetPropertyBlock(firstBlock);
        secondBodyRenderer.GetPropertyBlock(secondBlock);
        int mainColorId = Shader.PropertyToID("_MainColor");
        Assert.That(secondBlock.GetColor(mainColorId), Is.Not.EqualTo(firstBlock.GetColor(mainColorId)),
            "Two instances unexpectedly shared their per-instance palette.");

        Object.Destroy(secondPortal);
        yield return null;

        portalObject.SetActive(false);
        Assert.That(particles.particleCount, Is.EqualTo(0), "Disabling the effect did not clear particles.");
        portalObject.SetActive(true);
        yield return new WaitForSeconds(0.12f);
        Assert.That(effect.ParticleCount, Is.GreaterThan(0), "Re-enabling the effect did not restart emission.");

        Texture2D brightCapture = CaptureAgainstBrightBackground(prefab, outputDirectory);
        Object.Destroy(brightCapture);

        Object.Destroy(portalObject);
        yield return null;
    }

    private static IEnumerator BeginFreshTowerRun()
    {
        MonoBehaviour coordinator = null;
        MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].gameObject.scene.IsValid() &&
                behaviours[i].GetType().Name == "TowerRunCoordinator")
            {
                coordinator = behaviours[i];
                break;
            }
        }

        Assert.That(coordinator, Is.Not.Null);
        System.Reflection.MethodInfo returnToStartup =
            coordinator.GetType().GetMethod("ReturnToStartupChoice");
        System.Reflection.MethodInfo beginNewRun =
            coordinator.GetType().GetMethod("BeginNewRun");
        System.Reflection.PropertyInfo state = coordinator.GetType().GetProperty("State");
        Assert.That(returnToStartup, Is.Not.Null);
        Assert.That(beginNewRun, Is.Not.Null);
        Assert.That(state, Is.Not.Null);
        returnToStartup.Invoke(coordinator, null);
        beginNewRun.Invoke(coordinator, null);
        yield return null;
        Assert.That(state.GetValue(coordinator).ToString(), Is.EqualTo("Playing"));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    private static Texture2D CaptureAgainstBrightBackground(GameObject prefab, string outputDirectory)
    {
        string beforeDirectory = Path.Combine(outputDirectory, "Before");
        string afterDirectory = Path.Combine(outputDirectory, "After");
        Directory.CreateDirectory(beforeDirectory);
        Directory.CreateDirectory(afterDirectory);

        GameObject cameraObject = new GameObject("Portal_BrightValidationCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.72f, 0.84f, 0.62f, 1f);
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.cullingMask = 1 << 30;

        GameObject portal = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        SetLayerRecursively(portal, 30);
        BlackHolePortalEffect effect = portal.GetComponent<BlackHolePortalEffect>();
        effect.Stop(true);
        effect.SetAnimationTime(0f);
        Texture2D flowStart = Capture(
            camera,
            Path.Combine(afterDirectory, "Portal_BrightBackground_FlowStart.png"));
        effect.SetAnimationTime(0.35f);
        Texture2D capture = Capture(
            camera,
            Path.Combine(afterDirectory, "Portal_BrightBackground.png"));

        ValidateRenderedFlowDirections(flowStart, capture, camera, portal.transform);
        ValidateBrightBackgroundPixels(capture, camera, portal.transform);
        Object.Destroy(flowStart);

        portal.SetActive(false);
        GameObject baselinePortal = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        SetLayerRecursively(baselinePortal, 30);
        BlackHolePortalEffect baselineEffect = baselinePortal.GetComponent<BlackHolePortalEffect>();
        SerializedObject baselineProperties = new SerializedObject(baselineEffect);
        baselineProperties.FindProperty("portalHeight").floatValue = 6.8f;
        baselineProperties.FindProperty("aspectRatio").floatValue = 1.9f;
        baselineProperties.FindProperty("groundAnchorLocalY").floatValue = -3.4f;
        baselineProperties.ApplyModifiedPropertiesWithoutUndo();
        baselineEffect.Refresh();
        baselineEffect.Stop(true);
        baselineEffect.SetAnimationTime(0.35f);
        Object.Destroy(Capture(
            camera,
            Path.Combine(beforeDirectory, "Portal_BrightBackground.png")));

        Object.Destroy(portal);
        Object.Destroy(baselinePortal);
        Object.Destroy(cameraObject);
        return capture;
    }

    private static void ValidateRenderedFlowDirections(
        Texture2D flowStart,
        Texture2D flowEnd,
        Camera camera,
        Transform portalTransform)
    {
        const int sampleCount = 180;
        float pixelsPerWorldUnit = CaptureHeight / (camera.orthographicSize * 2f);
        Transform body = portalTransform.Find("EnergyBody");
        Transform core = portalTransform.Find("Core");
        Vector3 bodyViewportPosition = camera.WorldToViewportPoint(body.position);
        Vector2 center = new Vector2(
            bodyViewportPosition.x * CaptureWidth,
            bodyViewportPosition.y * CaptureHeight);
        Vector2 bodyRadii = new Vector2(
            body.lossyScale.x * pixelsPerWorldUnit * 0.5f * 0.82f,
            body.lossyScale.y * pixelsPerWorldUnit * 0.5f * 0.82f);
        Vector2 coreRadii = new Vector2(
            core.lossyScale.x * pixelsPerWorldUnit * 0.5f * 0.62f,
            core.lossyScale.y * pixelsPerWorldUnit * 0.5f * 0.62f);

        float[] outerStart = SampleBlueProfile(flowStart, center, bodyRadii, sampleCount);
        float[] outerEnd = SampleBlueProfile(flowEnd, center, bodyRadii, sampleCount);
        float[] innerStart = SampleBlueProfile(flowStart, center, coreRadii, sampleCount);
        float[] innerEnd = SampleBlueProfile(flowEnd, center, coreRadii, sampleCount);

        int outerShift = FindBestCircularShift(outerStart, outerEnd, 18);
        int innerShift = FindBestCircularShift(innerStart, innerEnd, 18);

        Assert.That(outerShift, Is.GreaterThan(1),
            $"Rendered outer flow was not clockwise. Correlation shift: {outerShift}");
        Assert.That(innerShift, Is.LessThan(-1),
            $"Rendered core flow was not counter-clockwise. Correlation shift: {innerShift}");
        Assert.That(outerShift * innerShift, Is.LessThan(0),
            "Rendered outer and inner flow did not move in opposite directions.");

        Debug.Log(
            $"BLACK_HOLE_PORTAL_FLOW_EVIDENCE|OuterClockwiseShift={outerShift}" +
            $"|InnerCounterClockwiseShift={innerShift}|Samples={sampleCount}|DeltaTime=0.35");
    }

    private static float[] SampleBlueProfile(
        Texture2D texture,
        Vector2 center,
        Vector2 radii,
        int sampleCount)
    {
        float[] profile = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float angle = i * Mathf.PI * 2f / sampleCount;
            int x = Mathf.RoundToInt(center.x + Mathf.Cos(angle) * radii.x);
            int y = Mathf.RoundToInt(center.y + Mathf.Sin(angle) * radii.y);

            float blue = 0f;
            int samples = 0;
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    blue += texture.GetPixel(x + offsetX, y + offsetY).b;
                    samples++;
                }
            }

            profile[i] = blue / samples;
        }

        return profile;
    }

    private static int FindBestCircularShift(float[] reference, float[] current, int maximumShift)
    {
        float referenceMean = Mean(reference);
        float currentMean = Mean(current);
        int bestShift = 0;
        float bestScore = float.NegativeInfinity;

        for (int shift = -maximumShift; shift <= maximumShift; shift++)
        {
            float score = 0f;
            for (int i = 0; i < reference.Length; i++)
            {
                int referenceIndex = (i + shift + reference.Length) % reference.Length;
                score += (reference[referenceIndex] - referenceMean) * (current[i] - currentMean);
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestShift = shift;
            }
        }

        return bestShift;
    }

    private static float Mean(float[] values)
    {
        float sum = 0f;
        for (int i = 0; i < values.Length; i++)
            sum += values[i];
        return sum / values.Length;
    }

    private static void ValidateParticleOutwardMotion(
        BlackHolePortalEffect effect,
        ParticleSystem particles)
    {
        ParticleSystem.Particle[] buffer =
            new ParticleSystem.Particle[Mathf.Max(1, particles.particleCount)];
        int count = particles.GetParticles(buffer);
        Assert.That(count, Is.GreaterThan(0));

        float halfWidth = effect.PortalWidth * 0.5f;
        float halfHeight = effect.PortalHeight * 0.5f;
        int outwardCount = 0;
        int recentBoundaryCount = 0;

        for (int index = 0; index < count; index++)
        {
            ParticleSystem.Particle particle = buffer[index];
            Vector2 position = particle.position;
            Vector2 ellipseNormal = new Vector2(
                position.x / Mathf.Max(halfWidth * halfWidth, 0.0001f),
                position.y / Mathf.Max(halfHeight * halfHeight, 0.0001f));
            if (Vector2.Dot(ellipseNormal, particle.velocity) > 0f)
                outwardCount++;

            float age = particle.startLifetime - particle.remainingLifetime;
            if (age > 0.08f)
                continue;

            float ellipseRadius =
                position.x * position.x / Mathf.Max(halfWidth * halfWidth, 0.0001f) +
                position.y * position.y / Mathf.Max(halfHeight * halfHeight, 0.0001f);
            if (ellipseRadius >= 0.85f && ellipseRadius <= 1.65f)
                recentBoundaryCount++;
        }

        Assert.That(outwardCount, Is.GreaterThanOrEqualTo(Mathf.CeilToInt(count * 0.9f)),
            "Edge particles did not predominantly travel outward from the ellipse.");
        Assert.That(recentBoundaryCount, Is.GreaterThan(0),
            "No recently emitted particle was found on the resized ellipse boundary.");
    }

    private static void ValidateBrightBackgroundPixels(
        Texture2D capture,
        Camera camera,
        Transform portalTransform)
    {
        Color background = new Color(0.72f, 0.84f, 0.62f, 1f);
        Color corner = capture.GetPixel(12, 12);
        Assert.That(ColorDistance(corner, background), Is.LessThan(0.08f),
            "Transparent portal area changed the background, which suggests a rectangular artifact.");

        Vector3 bodyViewportPosition = camera.WorldToViewportPoint(
            portalTransform.Find("EnergyBody").position);
        int centerX = Mathf.RoundToInt(bodyViewportPosition.x * CaptureWidth);
        int centerY = Mathf.RoundToInt(bodyViewportPosition.y * CaptureHeight);
        Color center = capture.GetPixel(centerX, centerY);
        Assert.That(Mathf.Max(center.r, center.g, center.b), Is.LessThan(0.12f),
            "The core did not remain dark and opaque over a bright background.");

        bool foundBlueRingPixel = false;
        for (int y = centerY - 90; y <= centerY + 90 && !foundBlueRingPixel; y++)
        {
            for (int x = centerX + 78; x <= centerX + 132; x++)
            {
                Color pixel = capture.GetPixel(x, y);
                if (pixel.b > 0.24f && pixel.b > pixel.r + 0.08f)
                {
                    foundBlueRingPixel = true;
                    break;
                }
            }
        }

        Assert.That(foundBlueRingPixel, Is.True, "No blue energy band was detected around the core.");
    }

    private static Texture2D Capture(Camera camera, string path)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(
            CaptureWidth,
            CaptureHeight,
            24,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;

        camera.targetTexture = renderTexture;
        RenderTexture.active = renderTexture;
        camera.Render();

        Texture2D capture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false, false);
        capture.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
        capture.Apply(false, false);
        File.WriteAllBytes(path, capture.EncodeToPNG());

        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(renderTexture);
        return capture;
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static string GetOutputDirectory()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/BlackHolePortalValidation"));
    }

    private static float ColorDistance(Color first, Color second)
    {
        Vector3 delta = new Vector3(first.r - second.r, first.g - second.g, first.b - second.b);
        return delta.magnitude;
    }
}
#endif

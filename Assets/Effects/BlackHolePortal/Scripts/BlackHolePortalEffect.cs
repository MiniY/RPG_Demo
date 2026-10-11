using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BlackHolePortalEffect : MonoBehaviour
{
    public const float DefaultPortalHeight = 5.44f;
    public const float DefaultAspectRatio = 1.52f;
    public const float DefaultGroundAnchorLocalY = -3.4f;

    private static readonly int PortalTimeId = Shader.PropertyToID("_PortalTime");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
    private static readonly int DetailDensityId = Shader.PropertyToID("_DetailDensity");
    private static readonly int DetailStrengthId = Shader.PropertyToID("_DetailStrength");
    private static readonly int PixelGridId = Shader.PropertyToID("_PixelGrid");
    private static readonly int MainColorId = Shader.PropertyToID("_MainColor");
    private static readonly int SecondaryColorId = Shader.PropertyToID("_SecondaryColor");
    private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
    private static readonly int CoreRatioId = Shader.PropertyToID("_CoreRatio");
    private static readonly int EdgeThicknessId = Shader.PropertyToID("_EdgeThickness");
    private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
    private static readonly int DarkFlowBrightnessId = Shader.PropertyToID("_DarkFlowBrightness");

    [Header("Shape")]
    [SerializeField, Min(0.25f)] private float portalHeight = DefaultPortalHeight;
    [SerializeField, Min(1.05f)] private float aspectRatio = DefaultAspectRatio;
    [SerializeField] private float groundAnchorLocalY = DefaultGroundAnchorLocalY;
    [SerializeField] private Vector2 coreRatio = new Vector2(0.55f, 0.70f);
    [SerializeField, Range(0.02f, 0.30f)] private float edgeThickness = 0.11f;

    [Header("Flow")]
    [SerializeField, Min(0f)] private float outerFlowSpeed = 0.72f;
    [SerializeField, Min(0f)] private float innerFlowSpeed = 0.48f;
    [SerializeField] private bool reverseFlow;
    [SerializeField, Range(0f, 1f)] private float distortionStrength = 0.34f;
    [SerializeField, Range(0.25f, 3f)] private float detailDensity = 1.15f;

    [Header("Palette")]
    [SerializeField] private Color mainDeepBlue = new Color(0.004f, 0.035f, 0.16f, 1f);
    [SerializeField] private Color secondaryBlue = new Color(0.015f, 0.20f, 0.74f, 1f);
    [SerializeField] private Color highlightBlue = new Color(0.08f, 0.48f, 1f, 1f);
    [SerializeField] private Color coreBlack = new Color(0.001f, 0.003f, 0.012f, 1f);
    [SerializeField] private Color coreFlowBlue = new Color(0.02f, 0.13f, 0.38f, 1f);
    [SerializeField, Range(0f, 1f)] private float darkFlowBrightness = 0.44f;

    [Header("Pixel Style")]
    [SerializeField, Range(8f, 96f)] private float pixelsPerUnit = 48f;
    [SerializeField, Range(0f, 2f)] private float detailStrength = 1f;
    [SerializeField, Range(0f, 1f)] private float glowStrength = 0.28f;

    [Header("Particles")]
    [SerializeField, Min(0f)] private float particleEmissionRate = 48f;
    [SerializeField] private Vector2 particleLifetimeRange = new Vector2(1.0f, 1.8f);
    [SerializeField] private Vector2 particleSizeRange = new Vector2(0.08f, 0.24f);
    [SerializeField, Min(0f)] private float particleOutwardSpeed = 1.60f;
    [SerializeField] private float particleTangentialSpeed = 0.75f;
    [SerializeField, Range(1, 2000)] private int particleLimit = 260;

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int baseSortingOrder = 10;

    [Header("Runtime")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private uint randomSeed = 7291u;

    [Header("Prefab References")]
    [SerializeField] private MeshRenderer coreRenderer;
    [SerializeField] private MeshRenderer bodyRenderer;
    [SerializeField] private ParticleSystem edgeParticles;

    [System.NonSerialized] private MaterialPropertyBlock bodyProperties;
    [System.NonSerialized] private MaterialPropertyBlock coreProperties;

    private ParticleSystemRenderer particleRenderer;
    private float animationTime;
    private float emissionAccumulator;
    private uint randomState;
    private bool isPlaying;

    public float PortalWidth => portalHeight / Mathf.Max(1.05f, aspectRatio);
    public float PortalHeight => portalHeight;
    public float AspectRatio => aspectRatio;
    public float GroundAnchorLocalY => groundAnchorLocalY;
    public float VisualCenterLocalY => groundAnchorLocalY + portalHeight * 0.5f;
    public int ParticleLimit => particleLimit;
    public int ParticleCount => edgeParticles != null ? edgeParticles.particleCount : 0;
    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        CacheReferences();
        Refresh();
    }

    private void OnEnable()
    {
        CacheReferences();
        ResetRandom();
        animationTime = 0f;
        emissionAccumulator = 0f;
        isPlaying = playOnEnable;
        Refresh();

        if (Application.isPlaying && edgeParticles != null)
        {
            edgeParticles.Clear(true);
            if (isPlaying)
                edgeParticles.Play(true);
            else
                edgeParticles.Pause(true);
        }
    }

    private void OnDisable()
    {
        if (edgeParticles != null)
            edgeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        emissionAccumulator = 0f;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            ApplyShaderProperties();
            return;
        }

        if (!isPlaying)
            return;

        Advance(Time.deltaTime, false);
    }

    private void OnValidate()
    {
        portalHeight = Mathf.Max(0.25f, portalHeight);
        aspectRatio = Mathf.Max(1.05f, aspectRatio);
        coreRatio.x = Mathf.Clamp(coreRatio.x, 0.15f, 0.90f);
        coreRatio.y = Mathf.Clamp(coreRatio.y, 0.15f, 0.90f);
        particleLifetimeRange = SortPositiveRange(particleLifetimeRange, 0.05f);
        particleSizeRange = SortPositiveRange(particleSizeRange, 0.005f);
        particleLimit = Mathf.Max(1, particleLimit);

        CacheReferences();
        Refresh();
    }

    public void Play()
    {
        isPlaying = true;
        if (edgeParticles != null && Application.isPlaying)
            edgeParticles.Play(true);
    }

    public void Pause()
    {
        isPlaying = false;
        if (edgeParticles != null && Application.isPlaying)
            edgeParticles.Pause(true);
    }

    public void Stop(bool clearParticles = true)
    {
        isPlaying = false;
        animationTime = 0f;
        emissionAccumulator = 0f;
        ResetRandom();

        if (edgeParticles != null)
        {
            ParticleSystemStopBehavior behavior = clearParticles
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;
            edgeParticles.Stop(true, behavior);
        }

        ApplyShaderProperties();
    }

    public void SetFlowReversed(bool reversed)
    {
        reverseFlow = reversed;
        ApplyShaderProperties();
    }

    public void SetAnimationTime(float time)
    {
        animationTime = Mathf.Max(0f, time);
        ApplyShaderProperties();
    }

    public void Refresh()
    {
        if (bodyRenderer == null || coreRenderer == null || edgeParticles == null)
            return;

        float width = PortalWidth;
        float visualCenterY = VisualCenterLocalY;
        SetVisualLayerPosition(bodyRenderer.transform, visualCenterY);
        SetVisualLayerPosition(coreRenderer.transform, visualCenterY);
        SetVisualLayerPosition(edgeParticles.transform, visualCenterY);
        bodyRenderer.transform.localScale = new Vector3(width, portalHeight, 1f);
        coreRenderer.transform.localScale = new Vector3(
            width * coreRatio.x,
            portalHeight * coreRatio.y,
            1f);

        bodyRenderer.sortingLayerName = sortingLayerName;
        bodyRenderer.sortingOrder = baseSortingOrder + 1;
        coreRenderer.sortingLayerName = sortingLayerName;
        coreRenderer.sortingOrder = baseSortingOrder;

        particleRenderer = edgeParticles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.sortingLayerName = sortingLayerName;
            particleRenderer.sortingOrder = baseSortingOrder + 2;
        }

        ConfigureParticleSystem();
        ApplyShaderProperties();
    }

    private void Advance(float deltaTime, bool simulateParticlesManually)
    {
        if (deltaTime <= 0f)
            return;

        animationTime += deltaTime;
        EmitParticles(deltaTime);

        if (simulateParticlesManually && edgeParticles != null)
            edgeParticles.Simulate(deltaTime, true, false, false);

        ApplyShaderProperties();
    }

    private void EmitParticles(float deltaTime)
    {
        if (edgeParticles == null || particleEmissionRate <= 0f)
            return;

        int available = particleLimit - edgeParticles.particleCount;
        if (available <= 0)
        {
            emissionAccumulator = Mathf.Min(emissionAccumulator, 1f);
            return;
        }

        emissionAccumulator += particleEmissionRate * deltaTime;
        int emissionCount = Mathf.Min(Mathf.Min(Mathf.FloorToInt(emissionAccumulator), available), 32);
        if (emissionCount <= 0)
            return;

        emissionAccumulator -= emissionCount;
        for (int i = 0; i < emissionCount; i++)
            EmitParticle();
    }

    private void EmitParticle()
    {
        float angle = Next01() * Mathf.PI * 2f;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        float halfWidth = PortalWidth * 0.5f;
        float halfHeight = portalHeight * 0.5f;

        Vector2 boundaryPoint = new Vector2(halfWidth * cos, halfHeight * sin);
        Vector2 normal = new Vector2(cos / Mathf.Max(halfWidth, 0.0001f), sin / Mathf.Max(halfHeight, 0.0001f));
        normal.Normalize();

        float flowSign = reverseFlow ? -1f : 1f;
        // Positive outer flow is clockwise on screen, so use the negative CCW tangent.
        Vector2 clockwiseTangent = new Vector2(normal.y, -normal.x) * flowSign;
        float outwardVariation = Mathf.Lerp(0.70f, 1.25f, Next01());
        float tangentialVariation = Mathf.Lerp(0.65f, 1.25f, Next01());
        Vector2 velocity = normal * (particleOutwardSpeed * outwardVariation)
            + clockwiseTangent * (particleTangentialSpeed * tangentialVariation);

        Color particleColor = Color.Lerp(secondaryBlue, highlightBlue, Mathf.Pow(Next01(), 1.7f));
        particleColor.a = Mathf.Lerp(0.62f, 1f, Next01());

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = boundaryPoint + normal * Mathf.Lerp(0.015f, 0.12f, Next01()),
            velocity = velocity,
            startLifetime = Mathf.Lerp(particleLifetimeRange.x, particleLifetimeRange.y, Next01()),
            startSize = Mathf.Lerp(particleSizeRange.x, particleSizeRange.y, Next01()),
            startColor = particleColor,
            rotation = Next01() * 360f,
            angularVelocity = Mathf.Lerp(-140f, 140f, Next01()),
            randomSeed = NextUInt()
        };

        edgeParticles.Emit(emitParams, 1);
    }

    private void ApplyShaderProperties()
    {
        if (bodyRenderer == null || coreRenderer == null)
            return;

        EnsurePropertyBlocks();

        float flowSign = reverseFlow ? -1f : 1f;
        Vector4 pixelGrid = new Vector4(
            Mathf.Max(8f, PortalWidth * pixelsPerUnit),
            Mathf.Max(8f, portalHeight * pixelsPerUnit),
            0f,
            0f);

        bodyRenderer.GetPropertyBlock(bodyProperties);
        bodyProperties.SetFloat(PortalTimeId, animationTime);
        bodyProperties.SetFloat(FlowSpeedId, outerFlowSpeed * flowSign);
        bodyProperties.SetFloat(DistortionId, distortionStrength);
        bodyProperties.SetFloat(DetailDensityId, detailDensity);
        bodyProperties.SetFloat(DetailStrengthId, detailStrength);
        bodyProperties.SetVector(PixelGridId, pixelGrid);
        bodyProperties.SetColor(MainColorId, mainDeepBlue);
        bodyProperties.SetColor(SecondaryColorId, secondaryBlue);
        bodyProperties.SetColor(HighlightColorId, highlightBlue);
        bodyProperties.SetVector(CoreRatioId, coreRatio);
        bodyProperties.SetFloat(EdgeThicknessId, edgeThickness);
        bodyProperties.SetFloat(GlowStrengthId, glowStrength);
        bodyRenderer.SetPropertyBlock(bodyProperties);

        coreRenderer.GetPropertyBlock(coreProperties);
        coreProperties.SetFloat(PortalTimeId, animationTime);
        coreProperties.SetFloat(FlowSpeedId, -innerFlowSpeed * flowSign);
        coreProperties.SetFloat(DistortionId, distortionStrength * 0.72f);
        coreProperties.SetFloat(DetailDensityId, detailDensity * 0.88f);
        coreProperties.SetFloat(DetailStrengthId, detailStrength);
        coreProperties.SetVector(PixelGridId, new Vector4(
            Mathf.Max(8f, PortalWidth * coreRatio.x * pixelsPerUnit),
            Mathf.Max(8f, portalHeight * coreRatio.y * pixelsPerUnit),
            0f,
            0f));
        coreProperties.SetColor(MainColorId, coreBlack);
        coreProperties.SetColor(SecondaryColorId, coreFlowBlue);
        coreProperties.SetFloat(DarkFlowBrightnessId, darkFlowBrightness);
        coreRenderer.SetPropertyBlock(coreProperties);
    }

    private void ConfigureParticleSystem()
    {
        ParticleSystem.MainModule main = edgeParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = particleLimit;
        main.startSpeed = 0f;
        main.startLifetime = particleLifetimeRange.y;
        main.startSize = particleSizeRange.y;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = edgeParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = edgeParticles.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = edgeParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            alphaKeys = new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.78f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            },
            colorKeys = new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            }
        });

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = edgeParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.75f),
            new Keyframe(0.18f, 1f),
            new Keyframe(1f, 0.18f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.LimitVelocityOverLifetimeModule velocityLimit = edgeParticles.limitVelocityOverLifetime;
        velocityLimit.enabled = true;
        velocityLimit.limit = new ParticleSystem.MinMaxCurve(Mathf.Max(0.1f, particleOutwardSpeed * 1.35f));
        velocityLimit.dampen = 0.08f;
    }

    private void CacheReferences()
    {
        if (edgeParticles != null && particleRenderer == null)
            particleRenderer = edgeParticles.GetComponent<ParticleSystemRenderer>();
    }

    private void EnsurePropertyBlocks()
    {
        if (bodyProperties == null)
            bodyProperties = new MaterialPropertyBlock();
        if (coreProperties == null)
            coreProperties = new MaterialPropertyBlock();
    }

    private void ResetRandom()
    {
        randomState = randomSeed == 0u ? 0xA341316Cu : randomSeed;
    }

    private uint NextUInt()
    {
        uint value = randomState;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        randomState = value == 0u ? 0xA341316Cu : value;
        return randomState;
    }

    private float Next01()
    {
        return (NextUInt() & 0x00FFFFFFu) / 16777216f;
    }

    private static Vector2 SortPositiveRange(Vector2 range, float minimum)
    {
        float min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return new Vector2(min, max);
    }

    private static void SetVisualLayerPosition(Transform layer, float localY)
    {
        Vector3 localPosition = layer.localPosition;
        localPosition.x = 0f;
        localPosition.y = localY;
        layer.localPosition = localPosition;
    }

#if UNITY_EDITOR
    public void EditorAssignReferences(
        MeshRenderer newBodyRenderer,
        MeshRenderer newCoreRenderer,
        ParticleSystem newEdgeParticles)
    {
        bodyRenderer = newBodyRenderer;
        coreRenderer = newCoreRenderer;
        edgeParticles = newEdgeParticles;
        particleRenderer = newEdgeParticles != null
            ? newEdgeParticles.GetComponent<ParticleSystemRenderer>()
            : null;
        Refresh();
    }

    public void EditorSimulate(float duration, float step = 1f / 60f)
    {
        if (edgeParticles == null || duration <= 0f)
            return;

        edgeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ResetRandom();
        emissionAccumulator = 0f;

        float remaining = duration;
        float safeStep = Mathf.Clamp(step, 1f / 240f, 0.1f);
        while (remaining > 0f)
        {
            float delta = Mathf.Min(safeStep, remaining);
            Advance(delta, true);
            remaining -= delta;
        }
    }
#endif
}

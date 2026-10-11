using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BlackHolePortalAssetBuilder
{
    public const string RootPath = "Assets/Effects/BlackHolePortal";
    public const string PrefabPath = RootPath + "/Prefabs/VerticalBlackHolePortal.prefab";
    public const string PreviewScenePath = RootPath + "/Preview/BlackHolePortalPreview.unity";

    private const string MeshPath = RootPath + "/Generated/BlackHolePortalQuad.asset";
    private const string BodyMaterialPath = RootPath + "/Materials/BlackHolePortalBody.mat";
    private const string CoreMaterialPath = RootPath + "/Materials/BlackHolePortalCore.mat";
    private const string ParticleMaterialPath = RootPath + "/Materials/BlackHolePortalParticle.mat";
    private const string BackdropMaterialPath = RootPath + "/Preview/BlackHolePortalPreviewBackdrop.mat";

    [MenuItem("Tools/Black Hole Portal/Build or Refresh Assets")]
    public static void BuildAssets()
    {
        EnsureFolder(RootPath + "/Generated");
        EnsureFolder(RootPath + "/Materials");
        EnsureFolder(RootPath + "/Prefabs");
        EnsureFolder(RootPath + "/Preview");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Mesh quadMesh = CreateOrUpdateQuadMesh();
        Material bodyMaterial = CreateOrUpdateMaterial(BodyMaterialPath, "RPGDemo/BlackHolePortal/Body");
        Material coreMaterial = CreateOrUpdateMaterial(CoreMaterialPath, "RPGDemo/BlackHolePortal/Core");
        Material particleMaterial = CreateOrUpdateMaterial(ParticleMaterialPath, "RPGDemo/BlackHolePortal/Particle");
        particleMaterial.SetFloat("_Intensity", 1.3f);
        EditorUtility.SetDirty(particleMaterial);
        Material backdropMaterial = CreateOrUpdateMaterial(BackdropMaterialPath, "RPGDemo/BlackHolePortal/PreviewBackdrop");

        CreateOrUpdatePrefab(quadMesh, bodyMaterial, coreMaterial, particleMaterial);
        CreateOrUpdatePreviewScene(quadMesh, backdropMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateShaders();

        Debug.Log($"Black hole portal assets built successfully. Prefab: {PrefabPath}");
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            BuildAssets();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static Mesh CreateOrUpdateQuadMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "BlackHolePortalQuad" };
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }

        mesh.Clear();
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Material CreateOrUpdateMaterial(string path, string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
            throw new InvalidOperationException($"Required shader was not found: {shaderName}");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateOrUpdatePrefab(
        Mesh quadMesh,
        Material bodyMaterial,
        Material coreMaterial,
        Material particleMaterial)
    {
        GameObject root = new GameObject("VerticalBlackHolePortal");
        try
        {
            BlackHolePortalEffect effect = root.AddComponent<BlackHolePortalEffect>();

            MeshRenderer coreRenderer = CreateMeshLayer(
                root.transform,
                "Core",
                quadMesh,
                coreMaterial,
                0f);

            MeshRenderer bodyRenderer = CreateMeshLayer(
                root.transform,
                "EnergyBody",
                quadMesh,
                bodyMaterial,
                -0.01f);

            GameObject particleObject = new GameObject("EdgeParticles");
            particleObject.transform.SetParent(root.transform, false);
            particleObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
            ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = particleMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleRenderer.minParticleSize = 0f;
            particleRenderer.maxParticleSize = 0.08f;

            effect.EditorAssignReferences(bodyRenderer, coreRenderer, particleSystem);
            effect.Refresh();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException($"Could not save portal prefab at {PrefabPath}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static MeshRenderer CreateMeshLayer(
        Transform parent,
        string name,
        Mesh mesh,
        Material material,
        float localZ)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(parent, false);
        layer.transform.localPosition = new Vector3(0f, 0f, localZ);

        MeshFilter meshFilter = layer.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = layer.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return meshRenderer;
    }

    private static void CreateOrUpdatePreviewScene(Mesh quadMesh, Material backdropMaterial)
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            throw new OperationCanceledException("Portal preview scene generation was cancelled to preserve unsaved scenes.");

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        Scene previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        try
        {
            GameObject cameraObject = new GameObject("PreviewCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 4.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.085f, 0.07f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;

            GameObject backdrop = new GameObject("PixelBackdrop");
            backdrop.transform.position = new Vector3(0f, 0f, 1f);
            backdrop.transform.localScale = new Vector3(17f, 10f, 1f);
            MeshFilter backdropFilter = backdrop.AddComponent<MeshFilter>();
            backdropFilter.sharedMesh = quadMesh;
            MeshRenderer backdropRenderer = backdrop.AddComponent<MeshRenderer>();
            backdropRenderer.sharedMaterial = backdropMaterial;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Portal prefab was not found at {PrefabPath}");

            GameObject portal = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject;
            if (portal == null)
                throw new InvalidOperationException("Could not instantiate portal in preview scene.");

            portal.transform.position = Vector3.zero;

            if (!EditorSceneManager.SaveScene(previewScene, PreviewScenePath))
                throw new InvalidOperationException($"Could not save preview scene at {PreviewScenePath}");
        }
        finally
        {
            bool canRestore = previousSetup.Length > 0;
            for (int i = 0; i < previousSetup.Length; i++)
                canRestore &= !string.IsNullOrEmpty(previousSetup[i].path);

            if (canRestore)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    private static void ValidateShaders()
    {
        string[] shaderNames =
        {
            "RPGDemo/BlackHolePortal/Body",
            "RPGDemo/BlackHolePortal/Core",
            "RPGDemo/BlackHolePortal/Particle",
            "RPGDemo/BlackHolePortal/PreviewBackdrop"
        };

        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Shader import failed: {shaderName}");

            if (ShaderUtil.ShaderHasError(shader))
            {
                ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
                string details = string.Join("\n", Array.ConvertAll(messages, message => message.message));
                throw new InvalidOperationException($"Shader contains compile errors: {shaderName}\n{details}");
            }
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}

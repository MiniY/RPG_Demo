// 精灵表批量切片工具（Tiny Swords 素材包专用，但通用）
// 用法：菜单 Tools/Tiny Swords/精灵表切片工具
// 对每个 PNG 手动输入帧尺寸（宽x高），点击"开始切割"按网格切片。
// 尺寸填 0 或勾选"单张" = 不切，保持单张 Sprite。
// 自动检测：基于"帧边界透明密度"推断帧尺寸（真实帧边界处像素几乎全透明）。
// 版本兼容：Unity 2022.2+ 使用 ISpriteEditorDataProvider，旧版使用 TextureImporter.spritesheet。
// 撤销：每次切割前自动快照原始导入设置到 Library/SpriteSheetSlicerBackup.json，
//       [撤销上次切割] 一键恢复到切割前状态。

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
#if UNITY_2022_2_OR_NEWER
using UnityEditor.U2D.Sprites;
#endif

public class SpriteSheetSlicerWindow : EditorWindow
{
    private class Entry
    {
        public string path;
        public int frameW; // 手动输入的帧宽
        public int frameH; // 手动输入的帧高
        public bool single; // 作为单张（不切片）
    }

    // ---------- 快照数据结构（用于撤销） ----------
    [Serializable]
    private class SnapshotMeta
    {
        public string name;
        public float x, y, w, h;
        public int alignment;
        public float pivotX, pivotY;

        public SnapshotMeta() { }

        public SnapshotMeta(SpriteMetaData m)
        {
            name = m.name;
            x = m.rect.x; y = m.rect.y; w = m.rect.width; h = m.rect.height;
            alignment = m.alignment;
            pivotX = m.pivot.x; pivotY = m.pivot.y;
        }

        public SpriteMetaData ToMetaData()
        {
            return new SpriteMetaData
            {
                name = name,
                rect = new Rect(x, y, w, h),
                alignment = alignment,
                pivot = new Vector2(pivotX, pivotY)
            };
        }
    }

    [Serializable]
    private class SnapshotEntry
    {
        public string path;
        public int textureType;
        public int spriteImportMode;
        public float ppu;
        public SnapshotMeta[] metas;
    }

    [Serializable]
    private class SnapshotRoot
    {
        public List<SnapshotEntry> entries = new List<SnapshotEntry>();
    }

    private readonly List<Entry> entries = new List<Entry>();
    private Vector2 scroll;
    private bool foldTinySwords = true;
    private bool foldFreePack = true;
    private int ppu = 64;

    private const string Root1 = "Assets/Resources/TinySwords";
    private const string Root2 = "Assets/Resources/TinySwordsFreePack";

    // 备份文件放 Library（不污染 Assets 资源库，也不进版本控制）
    private static string BackupPath
    {
        get
        {
            string lib = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library"));
            return Path.Combine(lib, "SpriteSheetSlicerBackup.json");
        }
    }

    [MenuItem("Tools/Tiny Swords/精灵表切片工具")]
    public static void OpenWindow()
    {
        var w = GetWindow<SpriteSheetSlicerWindow>("精灵表切片工具");
        w.minSize = new Vector2(560, 420);
        w.Refresh();
    }

    private void OnEnable() => Refresh();

    // ---------- 扫描资源 ----------
    private void Refresh()
    {
        entries.Clear();
        ScanFolder(Root1);
        ScanFolder(Root2);
        Repaint();
    }

    private void ScanFolder(string root)
    {
        if (!AssetDatabase.IsValidFolder(root)) return;
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;
            entries.Add(new Entry
            {
                path = path,
                frameW = 0,
                frameH = 0
            });
        }
    }

    // ---------- 自动检测帧尺寸（边界透明密度） ----------
    private static int DetectFrameSize(Texture2D tex)
    {
        int w = tex.width, h = tex.height;
        if (!tex.isReadable) return 0;

        Color32[] px = tex.GetPixels32();
        int best = 0;
        float bestScore = 0.6f; // 阈值：边界透明度占比必须 > 0.6 才算网格
        int[] candidates = { 16, 24, 32, 48, 64, 96, 128, 192, 256, 320, 384 };

        foreach (int f in candidates)
        {
            if (w % f != 0 || h % f != 0) continue;
            int cols = w / f, rows = h / f;
            if (cols <= 1 && rows <= 1) continue; // 单张图跳过

            // 统计所有帧边界线（x=k*f 与 y=k*f）上透明像素占比
            long total = 0, transparent = 0;
            for (int k = 1; k < cols; k++)
            {
                int x = k * f;
                for (int y = 0; y < h; y++)
                {
                    total++;
                    if (px[y * w + x].a == 0) transparent++;
                }
            }
            for (int k = 1; k < rows; k++)
            {
                int y = k * f;
                for (int x = 0; x < w; x++)
                {
                    total++;
                    if (px[y * w + x].a == 0) transparent++;
                }
            }
            float score = total == 0 ? 0f : (float)transparent / total;
            if (score > bestScore)
            {
                bestScore = score;
                best = f;
            }
        }
        return best;
    }

    private void DetectAll()
    {
        int ok = 0, fail = 0;
        foreach (var e in entries)
        {
            var importer = AssetImporter.GetAtPath(e.path) as TextureImporter;
            if (importer == null) continue;

            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(e.path);
            if (tex != null)
            {
                int f = DetectFrameSize(tex);
                if (f > 0)
                {
                    e.frameW = f;
                    e.frameH = f;
                    e.single = false;
                    ok++;
                }
                else
                {
                    e.single = true; // 检测不到网格 → 单张
                    fail++;
                }
            }
            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }
        EditorUtility.DisplayDialog("自动检测完成", string.Format("检测到网格帧尺寸：{0} 张；判定为单张：{1} 张。\n可手动修改任意行的帧宽/帧高后开始切割。", ok, fail), "OK");
        Repaint();
    }

    // ---------- 批量设置 ----------
    private void SetAll(int w, int h, bool single)
    {
        foreach (var e in entries)
        {
            e.frameW = w;
            e.frameH = h;
            e.single = single;
        }
        Repaint();
    }

    // ---------- 快照：读取当前导入设置（用于撤销） ----------
    private static SnapshotEntry Capture(TextureImporter importer)
    {
        var snap = new SnapshotEntry();
        snap.path = importer.assetPath;
        snap.textureType = (int)importer.textureType;
        snap.spriteImportMode = (int)importer.spriteImportMode;
        snap.ppu = importer.spritePixelsPerUnit;
        snap.metas = ReadSpriteMetas(importer);
        return snap;
    }

    private static SnapshotMeta[] ReadSpriteMetas(TextureImporter importer)
    {
        if (importer.spriteImportMode != SpriteImportMode.Multiple) return new SnapshotMeta[0];
#if UNITY_2022_2_OR_NEWER
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var rects = provider.GetSpriteRects();
        var list = new SnapshotMeta[rects.Length];
        for (int i = 0; i < rects.Length; i++)
        {
            list[i] = new SnapshotMeta
            {
                name = rects[i].name,
                x = rects[i].rect.x,
                y = rects[i].rect.y,
                w = rects[i].rect.width,
                h = rects[i].rect.height,
                alignment = (int)rects[i].alignment,
                pivotX = rects[i].pivot.x,
                pivotY = rects[i].pivot.y
            };
        }
        return list;
#else
        var md = importer.spritesheet;
        if (md == null || md.Length == 0) return new SnapshotMeta[0];
        var list = new SnapshotMeta[md.Length];
        for (int i = 0; i < md.Length; i++) list[i] = new SnapshotMeta(md[i]);
        return list;
#endif
    }

    // ---------- 写入 Sprite 元数据（版本兼容封装） ----------
    // metas 为 null 表示"单张模式"（清空切片）
    private static void ApplySpriteData(TextureImporter importer, SpriteImportMode mode, SpriteMetaData[] metas)
    {
#if UNITY_2022_2_OR_NEWER
        importer.spriteImportMode = mode;
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        if (metas != null && metas.Length > 0)
        {
            var rects = new SpriteRect[metas.Length];
            for (int i = 0; i < metas.Length; i++)
            {
                rects[i] = new SpriteRect
                {
                    name = metas[i].name,
                    rect = metas[i].rect,
                    alignment = (SpriteAlignment)metas[i].alignment,
                    pivot = metas[i].pivot
                };
            }
            provider.SetSpriteRects(rects);
        }
        provider.Apply();
#else
        importer.spriteImportMode = mode;
        importer.spritesheet = metas;
#endif
    }

    // ---------- 执行切割 ----------
    private void SliceAll()
    {
        var snapshot = new SnapshotRoot();
        int sliced = 0, single = 0, skipped = 0;
        var errors = new List<string>();

        foreach (var e in entries)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(e.path);
            var importer = AssetImporter.GetAtPath(e.path) as TextureImporter;
            if (tex == null || importer == null) { skipped++; continue; }

            bool isSingleMode = e.single || e.frameW <= 0 || e.frameH <= 0;

            // 快照原始状态（仅当本次会实际修改时才快照）
            if (!isSingleMode || importer.spriteImportMode != SpriteImportMode.Single)
            {
                snapshot.entries.Add(Capture(importer));
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = ppu;

            if (isSingleMode)
            {
                // 单张模式
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    ApplySpriteData(importer, SpriteImportMode.Single, null);
                    importer.SaveAndReimport();
                }
                single++;
                continue;
            }

            int w = tex.width, h = tex.height;
            int fw = e.frameW, fh = e.frameH;
            if (w % fw != 0 || h % fh != 0)
            {
                errors.Add(string.Format("{0}: {1}x{2} 无法被 {3}x{4} 整除，已跳过", Path.GetFileName(e.path), w, h, fw, fh));
                skipped++;
                continue;
            }

            int cols = w / fw, rows = h / fh;
            var metas = new SpriteMetaData[cols * rows];
            string baseName = Path.GetFileNameWithoutExtension(e.path);
            int idx = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // 纹理坐标系 y 轴朝上：顶部第 0 行对应 y = height - fh
                    metas[idx] = new SpriteMetaData
                    {
                        name = string.Format("{0}_{1}_{2}", baseName, r, c),
                        rect = new Rect(c * fw, h - (r + 1) * fh, fw, fh),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
                    idx++;
                }
            }

            ApplySpriteData(importer, SpriteImportMode.Multiple, metas);
            importer.SaveAndReimport();
            sliced++;
        }

        // 保存快照（供撤销），并清理失效的旧备份
        if (snapshot.entries.Count > 0)
        {
            string json = JsonUtility.ToJson(snapshot, true);
            try
            {
                File.WriteAllText(BackupPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[切片工具] 快照保存失败，撤销功能不可用: " + ex.Message);
            }
        }
        else if (File.Exists(BackupPath))
        {
            File.Delete(BackupPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = string.Format("切割完成：网格切片 {0} 张，单张 {1} 张，跳过 {2} 张。", sliced, single, skipped);
        if (errors.Count > 0)
            msg += "\n\n无法整除被跳过：\n" + string.Join("\n", errors.GetRange(0, Mathf.Min(errors.Count, 15)));
        Debug.Log(msg);
        EditorUtility.DisplayDialog("切片完成", msg, "OK");
    }

    // ---------- 撤销上次切割 ----------
    private void UndoLastSlice()
    {
        if (!File.Exists(BackupPath))
        {
            EditorUtility.DisplayDialog("撤销", "没有找到可撤销的备份（还没有执行过切割，或备份已被清理）。", "OK");
            return;
        }

        SnapshotRoot snapshot;
        try
        {
            snapshot = JsonUtility.FromJson<SnapshotRoot>(File.ReadAllText(BackupPath));
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("撤销失败", "备份文件损坏，无法解析：\n" + ex.Message, "OK");
            return;
        }
        if (snapshot == null || snapshot.entries == null || snapshot.entries.Count == 0)
        {
            EditorUtility.DisplayDialog("撤销", "备份为空，无法撤销。", "OK");
            return;
        }

        int restored = 0, failed = 0;
        foreach (var e in snapshot.entries)
        {
            var importer = AssetImporter.GetAtPath(e.path) as TextureImporter;
            if (importer == null) { failed++; continue; }

            try
            {
                importer.textureType = (TextureImporterType)e.textureType;
                importer.spritePixelsPerUnit = e.ppu;

                // 仅当原状态是 Sprite 类型时才处理 sprite 数据（Default 类型无需切片数据）
                if (e.textureType == (int)TextureImporterType.Sprite)
                {
                    if (e.spriteImportMode == (int)SpriteImportMode.Multiple && e.metas != null && e.metas.Length > 0)
                    {
                        var metas = new SpriteMetaData[e.metas.Length];
                        for (int i = 0; i < e.metas.Length; i++) metas[i] = e.metas[i].ToMetaData();
                        ApplySpriteData(importer, SpriteImportMode.Multiple, metas);
                    }
                    else
                    {
                        ApplySpriteData(importer, SpriteImportMode.Single, null);
                    }
                }
                importer.SaveAndReimport();
                restored++;
            }
            catch (Exception ex)
            {
                failed++;
                Debug.LogWarning(string.Format("[切片工具] 撤销失败: {0} - {1}", e.path, ex.Message));
            }
        }

        File.Delete(BackupPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("撤销完成",
            string.Format("已恢复 {0} 张图片到切割前状态。{1}", restored,
                failed > 0 ? string.Format("\n失败 {0} 张（见 Console 日志）。", failed) : ""), "OK");
        Repaint();
    }

    // ---------- UI ----------
    private void OnGUI()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("刷新列表", GUILayout.Width(100))) Refresh();
            if (GUILayout.Button("自动检测尺寸", GUILayout.Width(115))) DetectAll();
            if (GUILayout.Button("全部设为 192", GUILayout.Width(105))) SetAll(192, 192, false);
            if (GUILayout.Button("全部设为 128", GUILayout.Width(105))) SetAll(128, 128, false);
            if (GUILayout.Button("全部设为 64", GUILayout.Width(95))) SetAll(64, 64, false);
            if (GUILayout.Button("全部单张", GUILayout.Width(90))) SetAll(0, 0, true);
        }
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("PPU (Sprite 每单位像素)", GUILayout.Width(160));
            ppu = EditorGUILayout.IntField(ppu, GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            bool hasBackup = File.Exists(BackupPath);
            GUI.enabled = hasBackup;
            if (GUILayout.Button("撤销上次切割", GUILayout.Width(120)))
            {
                if (EditorUtility.DisplayDialog("确认撤销", "将恢复所有图片到上次切割前的状态，是否继续？", "撤销", "取消"))
                    UndoLastSlice();
            }
            GUI.enabled = true;
            if (GUILayout.Button("开始切割", GUILayout.Width(110)))
            {
                if (EditorUtility.DisplayDialog("确认", "将按当前每行设置的帧尺寸切割所有图片，是否继续？", "切割", "取消"))
                    SliceAll();
            }
        }
        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawList(Root1, ref foldTinySwords);
        DrawList(Root2, ref foldFreePack);
        EditorGUILayout.EndScrollView();
    }

    private void DrawList(string root, ref bool fold)
    {
        var list = entries.FindAll(e => e.path.StartsWith(root));
        if (list.Count == 0) return;
        fold = EditorGUILayout.Foldout(fold, string.Format("{0}  ({1} 张)", root, list.Count));
        if (!fold) return;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("文件", EditorStyles.boldLabel, GUILayout.MinWidth(260));
            EditorGUILayout.LabelField("帧宽", EditorStyles.boldLabel, GUILayout.Width(52));
            EditorGUILayout.LabelField("帧高", EditorStyles.boldLabel, GUILayout.Width(52));
            EditorGUILayout.LabelField("单张", EditorStyles.boldLabel, GUILayout.Width(44));
        }

        foreach (var e in list)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(e.path);
            string size = tex != null ? string.Format("  [{0}x{1}]", tex.width, tex.height) : "";
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(Path.GetFileName(e.path) + size, GUILayout.MinWidth(260));
                int nw = EditorGUILayout.IntField(e.frameW, GUILayout.Width(52));
                int nh = EditorGUILayout.IntField(e.frameH, GUILayout.Width(52));
                bool ns = EditorGUILayout.Toggle(e.single, GUILayout.Width(44));
                if (nw != e.frameW || nh != e.frameH || ns != e.single)
                {
                    e.frameW = Mathf.Max(0, nw);
                    e.frameH = Mathf.Max(0, nh);
                    e.single = ns;
                    if (ns) { e.frameW = 0; e.frameH = 0; }
                }
            }
        }
    }
}
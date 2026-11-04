using UnityEngine;

/// <summary>将稀疏图集的已核实区域拼接为面板；不修改共用纹理或导入器。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ShopPatchGraphic : UnityEngine.UI.MaskableGraphic
{
    [SerializeField] private Texture2D sourceTexture;
    [SerializeField] private Rect[] sourcePatches = new Rect[9];
    [SerializeField] private Vector2 capSize = new Vector2(64, 64);
    [SerializeField] private float pixelScale = 1f;
    [SerializeField] private bool horizontalOnly;

    public override Texture mainTexture => sourceTexture != null ? sourceTexture : Texture2D.whiteTexture;

    public void Configure(Texture2D texture, Rect[] patches, Vector2 caps, float scale, bool ribbon = false)
    {
        sourceTexture = texture;
        sourcePatches = patches;
        capSize = caps;
        pixelScale = scale;
        horizontalOnly = ribbon;
        SetAllDirty();
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper helper)
    {
        helper.Clear();
        if (sourceTexture == null || sourcePatches == null || sourcePatches.Length != (horizontalOnly ? 3 : 9))
            return;

        Rect area = GetPixelAdjustedRect();
        float capX = Mathf.Min(capSize.x, area.width / 2);
        float capY = Mathf.Min(capSize.y, area.height / 2);
        float[] xs = { area.xMin, area.xMin + capX, area.xMax - capX, area.xMax };
        float[] ys = horizontalOnly
            ? new[] { area.yMin, area.yMax }
            : new[] { area.yMin, area.yMin + capY, area.yMax - capY, area.yMax };

        for (int row = 0; row < ys.Length - 1; row++)
        for (int column = 0; column < 3; column++)
        {
            Rect source = sourcePatches[row * 3 + column];
            Rect target = Rect.MinMaxRect(xs[column], ys[row], xs[column + 1], ys[row + 1]);
            if (target.width <= 0 || target.height <= 0 || source.width <= 0 || source.height <= 0)
                continue;
            // 仅延展方向平铺；角块保持完整，末块按实际剩余长度裁切 UV。
            float stepX = column == 1 ? source.width * pixelScale : target.width;
            float stepY = !horizontalOnly && row == 1 ? source.height * pixelScale : target.height;
            stepX = Mathf.Max(1, stepX);
            stepY = Mathf.Max(1, stepY);
            for (float y = target.yMin; y < target.yMax - .01f; y += stepY)
            for (float x = target.xMin; x < target.xMax - .01f; x += stepX)
            {
                float width = Mathf.Min(stepX, target.xMax - x);
                float height = Mathf.Min(stepY, target.yMax - y);
                Rect uv = new Rect(source.x / sourceTexture.width, source.y / sourceTexture.height,
                    source.width / sourceTexture.width * width / stepX,
                    source.height / sourceTexture.height * height / stepY);
                AddQuad(helper, new Rect(x, y, width, height), uv);
            }
        }
    }

    private void AddQuad(UnityEngine.UI.VertexHelper helper, Rect target, Rect uv)
    {
        int index = helper.currentVertCount;
        helper.AddVert(new Vector3(target.xMin, target.yMin), color, new Vector2(uv.xMin, uv.yMin));
        helper.AddVert(new Vector3(target.xMin, target.yMax), color, new Vector2(uv.xMin, uv.yMax));
        helper.AddVert(new Vector3(target.xMax, target.yMax), color, new Vector2(uv.xMax, uv.yMax));
        helper.AddVert(new Vector3(target.xMax, target.yMin), color, new Vector2(uv.xMax, uv.yMin));
        helper.AddTriangle(index, index + 1, index + 2);
        helper.AddTriangle(index + 2, index + 3, index);
    }
}

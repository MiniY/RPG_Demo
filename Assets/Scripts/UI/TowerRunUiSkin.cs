using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-owned sprite references for the settings overlay built at runtime.
/// Keeping references on the Canvas lets the Player retain imported sprites without Resources copies.
/// </summary>
[DisallowMultipleComponent]
public sealed class TowerRunUiSkin : MonoBehaviour
{
    [SerializeField] private Sprite woodFrame;
    [SerializeField] private Sprite carvedPaper;
    [SerializeField] private Sprite carvedCard;
    [SerializeField] private Sprite titleBanner;
    [SerializeField] private Sprite blueRibbon;
    [SerializeField] private Sprite blueButton;
    [SerializeField] private Sprite redButton;
    [SerializeField] private Sprite disabledButton;

    public Sprite WoodFrame => woodFrame;
    public Sprite CarvedPaper => carvedPaper;
    public Sprite CarvedCard => carvedCard;
    public Sprite TitleBanner => titleBanner;
    public Sprite BlueRibbon => blueRibbon;
    public Sprite BlueButton => blueButton;
    public Sprite RedButton => redButton;
    public Sprite DisabledButton => disabledButton;

    public static readonly Color Ink = new Color32(0x30, 0x23, 0x1B, 0xFF);
    public static readonly Color MutedInk = new Color32(0x4A, 0x36, 0x29, 0xFF);
    public static readonly Color LightInk = new Color32(0xFF, 0xF3, 0xDB, 0xFF);
    public static readonly Color TextShadow = new Color32(0x12, 0x0C, 0x08, 0x59);

    public static void ApplySprite(Image image, Sprite sprite, Color fallback, bool sliced = true)
    {
        image.sprite = sprite;
        image.type = sprite != null && sliced && sprite.border != Vector4.zero
            ? Image.Type.Sliced
            : Image.Type.Simple;
        image.color = sprite != null ? Color.white : fallback;
    }

    public void StyleButton(Button button, Sprite sprite = null)
    {
        Image image = button.GetComponent<Image>();
        ApplySprite(image, sprite != null ? sprite : blueButton, new Color(0.21f, 0.44f, 0.59f));

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
        colors.pressedColor = new Color(0.74f, 0.85f, 0.9f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.64f, 0.64f, 0.64f, 0.85f);
        button.colors = colors;
    }
}

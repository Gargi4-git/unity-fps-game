using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>A simple horizontal bar made of a background + fill.</summary>
public class UIBar
{
    public Image background;
    public RectTransform root;
    public RectTransform fill;
    public Image fillImage;

    public void Set(float value01)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
    }
}

/// <summary>
/// Builds uGUI elements from code so the HUD works without any
/// manual Canvas setup in the editor.
/// </summary>
public static class UIFactory
{
    private static Font font;
    private static Sprite vignetteSprite;

    public static Font GetFont()
    {
        if (font != null)
            return font;

#if UNITY_2022_2_OR_NEWER
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif

        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 16);

        return font;
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rt = CreateRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static Text CreateText(
        string name, Transform parent, string content, int size,
        TextAnchor align, Color color, bool shadow = true)
    {
        RectTransform rt = CreateRect(name, parent);
        Text t = rt.gameObject.AddComponent<Text>();

        t.font = GetFont();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;

        if (shadow)
        {
            Shadow s = rt.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.85f);
            s.effectDistance = new Vector2(2f, -2f);
        }

        return t;
    }

    public static UIBar CreateBar(
        Transform parent, string name, Vector2 anchor, Vector2 pivot,
        Vector2 pos, Vector2 size, Color bgColor, Color fillColor, float padding)
    {
        UIBar bar = new UIBar();

        bar.background = CreateImage(name, parent, bgColor);
        bar.root = bar.background.rectTransform;
        Anchor(bar.root, anchor, pivot, pos, size);

        bar.fillImage = CreateImage("Fill", bar.root, fillColor);
        bar.fill = bar.fillImage.rectTransform;
        bar.fill.anchorMin = Vector2.zero;
        bar.fill.anchorMax = Vector2.one;
        bar.fill.offsetMin = new Vector2(padding, padding);
        bar.fill.offsetMax = new Vector2(-padding, -padding);

        return bar;
    }

    public static Button CreateButton(
        Transform parent, string label, Vector2 anchoredPos, Vector2 size, UnityAction onClick)
    {
        Image img = CreateImage(label + " Button", parent, Color.white);
        img.raycastTarget = true;

        Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, size);

        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;

        // Image is white, so the tint colours ARE the visible colours.
        ColorBlock cb = button.colors;
        cb.normalColor = new Color(0.13f, 0.16f, 0.21f, 0.95f);
        cb.highlightedColor = new Color(0.22f, 0.45f, 0.65f, 1f);
        cb.pressedColor = new Color(0.15f, 0.32f, 0.5f, 1f);
        cb.selectedColor = cb.normalColor;
        cb.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        cb.fadeDuration = 0.08f;
        button.colors = cb;

        Text text = CreateText("Label", img.rectTransform, label, 34,
            TextAnchor.MiddleCenter, Color.white);
        Stretch(text.rectTransform);

        if (onClick != null)
            button.onClick.AddListener(onClick);

        return button;
    }

    public static Slider CreateSlider(
        Transform parent, Vector2 anchoredPos, float width,
        float min, float max, float value, Color accent, UnityAction<float> onChanged)
    {
        RectTransform root = CreateRect("Slider", parent);
        Anchor(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(width, 30f));

        Image bg = CreateImage("Background", root, new Color(1f, 1f, 1f, 0.18f));
        bg.raycastTarget = true;
        bg.rectTransform.anchorMin = new Vector2(0f, 0.35f);
        bg.rectTransform.anchorMax = new Vector2(1f, 0.65f);
        bg.rectTransform.offsetMin = Vector2.zero;
        bg.rectTransform.offsetMax = Vector2.zero;

        RectTransform fillArea = CreateRect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0f, 0.35f);
        fillArea.anchorMax = new Vector2(1f, 0.65f);
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        Image fill = CreateImage("Fill", fillArea, accent);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        RectTransform handleArea = CreateRect("Handle Slide Area", root);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);

        Image handle = CreateImage("Handle", handleArea, Color.white);
        handle.raycastTarget = true;
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(20f, 0f);

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.SetValueWithoutNotify(value);

        if (onChanged != null)
            slider.onValueChanged.AddListener(onChanged);

        return slider;
    }

    /// <summary>Soft red-edged radial gradient used for the damage vignette.</summary>
    public static Sprite GetVignetteSprite()
    {
        if (vignetteSprite != null)
            return vignetteSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x / (size - 1f)) * 2f - 1f;
                float dy = (y / (size - 1f)) * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1.25f, d));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        vignetteSprite = Sprite.Create(
            tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);

        return vignetteSprite;
    }
}

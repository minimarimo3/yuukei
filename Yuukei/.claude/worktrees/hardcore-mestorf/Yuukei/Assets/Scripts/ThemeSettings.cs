using System;
using UnityEngine;

[Serializable]
public class ThemeSettings
{
    public DialogueWindowSettings dialogueWindow;
    public ButtonSettings button;
    public ProgressBarSettings progressBar;
    public InputFieldSettings inputField;
    public PopupSettingsCollection popups;
}

[Serializable]
public class PaddingDef
{
    public float left;
    public float right;
    public float top;
    public float bottom;
    
    // 9スライス生成用のVector4変換プロパティ
    public Vector4 ToVector4() => new Vector4(left, bottom, right, top);
}

[Serializable]
public class DialogueWindowSettings
{
    public string backgroundImage;
    public PaddingDef backgroundBorder; // 9スライス用
    public string tailImage;
    public PaddingDef textPadding;      // 背景画像に対するテキスト領域の余白
    public string fontColorHtml;        // 例: "#333333"
    public float fontSize;
}

[Serializable]
public class ButtonSettings
{
    public string normalImage;
    public string hoverImage;
    public string pressedImage;
    public PaddingDef border;
    public PaddingDef textPadding;
    public string fontColorHtml;
    public float fontSize;
}

[Serializable]
public class ProgressBarSettings
{
    public string backgroundImage;
    public PaddingDef backgroundBorder;
    public string fillImage;
    public PaddingDef fillBorder;
    public int fillMethod; // Unityの Image.FillMethod のint値 (0: Horizontal, 1: Vertical など)
    public int fillOrigin; // Unityの Image.FillOrigin のint値 (0: Left, 1: Right など)
}

[Serializable]
public class InputFieldSettings
{
    public string backgroundImage;
    public PaddingDef backgroundBorder;
    public PaddingDef textPadding;
    public string fontColorHtml;
    public float fontSize;
    public string caretColorHtml;
}

[Serializable]
public class PopupSettingsCollection
{
    public PopupThemeSettings general;
    public PopupThemeSettings info;
    public PopupThemeSettings warning;
    public PopupThemeSettings error;
    public PopupThemeSettings glitch;
}

[Serializable]
public class PopupThemeSettings
{
    public string backgroundImage;
    public string backgroundDrawMode; // "Sliced" または "Simple"
    public PaddingDef backgroundBorder;
    public PaddingDef contentPadding;
    public string iconImage;
    public string fontColorHtml;
    public CloseButtonSettings closeButton;
}

[Serializable]
public class CloseButtonSettings
{
    public string normalImage;
    public string hoverImage;
    public string pressedImage;
    public Vector2Def offset;
    public SizeDef size;
}

[Serializable]
public class Vector2Def { public float x; public float y; }

[Serializable]
public class SizeDef { public float width; public float height; }

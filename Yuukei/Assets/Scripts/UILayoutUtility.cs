using UnityEngine;

public static class UILayoutUtility
{
    /// <summary>
    /// RectTransformにパディングを適用します。
    /// 前提: 対象のRectTransformのAnchorがStretch-Stretch (Min(0,0), Max(1,1)) になっていること。
    /// </summary>
    public static void ApplyPadding(RectTransform targetRect, PaddingDef padding)
    {
        if (targetRect == null || padding == null) return;

        // AnchorをStretchに強制
        targetRect.anchorMin = Vector2.zero;
        targetRect.anchorMax = Vector2.one;

        // offsetMinは (Left, Bottom)
        targetRect.offsetMin = new Vector2(padding.left, padding.bottom);
        // offsetMaxは (-Right, -Top) ※内側に向かうためマイナスにする必要があります
        targetRect.offsetMax = new Vector2(-padding.right, -padding.top);
    }
}
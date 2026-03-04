using UnityEngine;

public static class SpriteFactory
{
    /// <summary>
    /// Texture2Dから9スライス対応のSpriteを生成します。
    /// </summary>
    public static Sprite Create9SliceSprite(Texture2D texture, PaddingDef borderDef)
    {
        if (texture == null) return null;

        Rect rect = new Rect(0, 0, texture.width, texture.height);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        Vector4 border = borderDef != null ? borderDef.ToVector4() : Vector4.zero;

        // UI用途のため、SpriteMeshType.FullRect を指定するのがベストプラクティスです
        return Sprite.Create(texture, rect, pivot, 100f, 0, SpriteMeshType.FullRect, border);
    }
}
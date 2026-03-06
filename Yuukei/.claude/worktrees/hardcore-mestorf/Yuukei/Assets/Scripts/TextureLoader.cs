using System.IO;
using UnityEngine;

public static class TextureLoader
{
    public static Texture2D LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"画像ファイルが見つかりません: {filePath}");
            return null;
        }

        byte[] fileData = File.ReadAllBytes(filePath);
        // ミップマップ生成を無効化してUI用のテクスチャを生成
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(fileData);
        
        return tex;
    }
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Siluet putih dari tekstur sprite (ukuran & tata letak SAMA persis dengan tekstur aslinya).
/// Outline digambar dengan sprite asli (mesh, pivot, flip identik) tapi teksturnya diganti siluet ini,
/// lalu digeser 1 pixel ke 4 arah di belakang sprite -> garis tepi selalu pas, tidak bisa bergeser.
/// Di-cache per tekstur. Tidak butuh "Read/Write" di import (dibaca lewat RenderTexture kalau perlu).
/// </summary>
public static class SpriteOutline
{
    private static readonly Dictionary<Texture2D, Texture2D> cache = new Dictionary<Texture2D, Texture2D>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnPlay()
    {
        cache.Clear();
    }

    public static Texture2D Silhouette(Texture2D texture)
    {
        if (texture == null) return null;
        if (cache.TryGetValue(texture, out Texture2D cached) && cached != null) return cached;

        Color32[] pixels = ReadAll(texture);
        if (pixels == null) return null;
        for (int i = 0; i < pixels.Length; i++)
        {
            byte a = pixels[i].a;
            pixels[i] = new Color32(255, 255, 255, a >= 128 ? (byte)255 : (byte)0);
        }

        Texture2D white = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false)
        {
            name = texture.name + " Silhouette",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        white.SetPixels32(pixels);
        white.Apply();
        cache[texture] = white;
        return white;
    }

    private static Color32[] ReadAll(Texture2D texture)
    {
        if (texture.isReadable) return texture.GetPixels32();

        RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(texture, rt);
        RenderTexture.active = rt;
        Texture2D read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        read.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] pixels = read.GetPixels32();
        Object.Destroy(read);
        return pixels;
    }
}

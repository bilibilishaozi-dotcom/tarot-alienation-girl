using UnityEngine;

// 运行时纯色方块。尺寸、锚点和像素密度与原来各处手写的 4×4 方块一致。
public static class RuntimeSprite
{
    private static Sprite pixel;

    public static Sprite Pixel
    {
        get
        {
            if (pixel != null)
                return pixel;

            Texture2D texture = new Texture2D(4, 4);
            texture.hideFlags = HideFlags.HideAndDontSave;
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            pixel = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            pixel.hideFlags = HideFlags.HideAndDontSave;
            return pixel;
        }
    }
}

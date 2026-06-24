using UnityEngine;

// Runtime soft-circle sprites used by thrown flame VFX.
public static class FlameVfxSprites
{
    private static Sprite coreSprite;
    private static Sprite emberSprite;
    private static Material additiveMaterial;
    private static Material alphaMaterial;

    public static Sprite Core => coreSprite ??= CreateSprite(48, 0.9f);
    public static Sprite Ember => emberSprite ??= CreateSprite(24, 0.75f);

    public static Material AlphaMaterial => alphaMaterial ??= new Material(Shader.Find("Sprites/Default"));

    public static Material AdditiveMaterial
    {
        get
        {
            if (additiveMaterial != null)
            {
                return additiveMaterial;
            }

            additiveMaterial = new Material(Shader.Find("Sprites/Default"));
            additiveMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            additiveMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            return additiveMaterial;
        }
    }

    private static Sprite CreateSprite(int size, float falloffPower)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        float center = (size - 1) * 0.5f;
        float maxRadius = center;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / maxRadius);
                float alpha = Mathf.Pow(t, falloffPower);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}

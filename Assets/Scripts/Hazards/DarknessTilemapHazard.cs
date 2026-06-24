using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
public class DarknessTilemapHazard : MonoBehaviour
{
    private const string MaskObjectName = "Generated Darkness Mask";
    private static readonly Vector2[] CoverageSampleOffsets =
    {
        new Vector2(0.25f, 0.25f),
        new Vector2(0.75f, 0.25f),
        new Vector2(0.25f, 0.75f),
        new Vector2(0.75f, 0.75f),
    };

    [Header("Detection")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private PlayerDeathHandler playerDeathHandler;
    [SerializeField] private Collider2D playerCollider;
    [Tooltip("Fraction of the player's collider bounds that must be inside darkness before it kills.")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float minimumOverlapFraction = 0.22f;

    [Header("Black Bloom Overlay")]
    [SerializeField] private bool buildDarknessMask = true;
    [SerializeField] private float glowRadius = 1.5f;
    [SerializeField] private float texturePixelsPerUnit = 72f;
    [SerializeField] private int maxTextureSize = 1536;
    [SerializeField] private float coreAlpha = 1f;
    [SerializeField] private float glowAlpha = 1f;

    private readonly HashSet<Vector3Int> darknessCells = new HashSet<Vector3Int>();
    private bool maskDirty;
    private bool hasBuiltMask;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        CheckPlayerOverlap();
    }

    private void LateUpdate()
    {
        if (!buildDarknessMask || darknessCells.Count == 0 || (!maskDirty && hasBuiltMask))
        {
            return;
        }

        RebuildDarknessMask();
    }

    private void Reset()
    {
        tilemap = GetComponent<Tilemap>();
    }

    private void ResolveReferences()
    {
        if (tilemap == null)
        {
            tilemap = GetComponent<Tilemap>();
        }

        if (playerDeathHandler == null)
        {
            playerDeathHandler = FindAnyObjectByType<PlayerDeathHandler>();
        }

        if (playerCollider == null && playerDeathHandler != null)
        {
            playerCollider = playerDeathHandler.GetComponent<Collider2D>();
        }
    }

    public void RegisterDarknessCell(Vector3 worldPosition)
    {
        if (tilemap == null)
        {
            tilemap = GetComponent<Tilemap>();
        }

        if (tilemap == null)
        {
            return;
        }

        Vector3Int cell = tilemap.WorldToCell(worldPosition);
        if (darknessCells.Add(cell))
        {
            maskDirty = true;
        }
    }

    public void UnregisterDarknessCell(Vector3Int cell)
    {
        if (darknessCells.Remove(cell))
        {
            maskDirty = true;
        }
    }

    private void CheckPlayerOverlap()
    {
        if (tilemap == null || playerDeathHandler == null || playerCollider == null)
        {
            ResolveReferences();
        }

        if (tilemap == null || playerDeathHandler == null || playerCollider == null)
        {
            return;
        }

        if (darknessCells.Count == 0)
        {
            return;
        }

        if (GetDarknessOverlapFraction(playerCollider.bounds) >= minimumOverlapFraction)
        {
            playerDeathHandler.Kill();
        }
    }

    private float GetDarknessOverlapFraction(Bounds playerBounds)
    {
        float playerArea = Mathf.Max(0.0001f, playerBounds.size.x * playerBounds.size.y);
        Vector3Int minCell = tilemap.WorldToCell(playerBounds.min);
        Vector3Int maxCell = tilemap.WorldToCell(playerBounds.max);
        float darknessArea = 0f;

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, minCell.z);
                if (!darknessCells.Contains(cell))
                {
                    continue;
                }

                Bounds cellBounds = GetCellWorldBounds(cell);
                darknessArea += CalculateOverlapArea(playerBounds, cellBounds);
            }
        }

        return darknessArea / playerArea;
    }

    private Bounds GetCellWorldBounds(Vector3Int cell)
    {
        Vector3 cellMin = tilemap.CellToWorld(cell);
        Vector3 cellMax = tilemap.CellToWorld(new Vector3Int(cell.x + 1, cell.y + 1, cell.z));
        Vector3 min = Vector3.Min(cellMin, cellMax);
        Vector3 max = Vector3.Max(cellMin, cellMax);
        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private Bounds GetCellLocalBounds(Vector3Int cell)
    {
        Vector3 cellMin = tilemap.CellToLocal(cell);
        Vector3 cellMax = tilemap.CellToLocal(new Vector3Int(cell.x + 1, cell.y + 1, cell.z));
        Vector3 min = Vector3.Min(cellMin, cellMax);
        Vector3 max = Vector3.Max(cellMin, cellMax);
        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private static float CalculateOverlapArea(Bounds a, Bounds b)
    {
        float minX = Mathf.Max(a.min.x, b.min.x);
        float maxX = Mathf.Min(a.max.x, b.max.x);
        float minY = Mathf.Max(a.min.y, b.min.y);
        float maxY = Mathf.Min(a.max.y, b.max.y);

        if (maxX <= minX || maxY <= minY)
        {
            return 0f;
        }

        return (maxX - minX) * (maxY - minY);
    }

    [ContextMenu("Rebuild Darkness Mask")]
    private void RebuildDarknessMask()
    {
        if (tilemap == null)
        {
            tilemap = GetComponent<Tilemap>();
        }

        if (tilemap == null)
        {
            return;
        }

        ClearDarknessMask();

        if (darknessCells.Count == 0)
        {
            return;
        }

        Bounds maskBounds = default;
        bool hasBounds = false;
        foreach (Vector3Int cell in darknessCells)
        {
            Bounds cellBounds = GetCellLocalBounds(cell);
            if (!hasBounds)
            {
                maskBounds = cellBounds;
                hasBounds = true;
                continue;
            }

            maskBounds.Encapsulate(cellBounds);
        }

        float radius = Mathf.Max(0.01f, glowRadius);
        maskBounds.Expand(radius * 2f);

        float pixelsPerUnit = Mathf.Clamp(texturePixelsPerUnit, 8f, 96f);
        int width = Mathf.Max(1, Mathf.CeilToInt(maskBounds.size.x * pixelsPerUnit));
        int height = Mathf.Max(1, Mathf.CeilToInt(maskBounds.size.y * pixelsPerUnit));
        int maxSize = Mathf.Clamp(maxTextureSize, 64, 1536);
        if (width > maxSize || height > maxSize)
        {
            float scale = Mathf.Min(maxSize / (float)width, maxSize / (float)height);
            pixelsPerUnit *= scale;
            width = Mathf.Max(1, Mathf.CeilToInt(maskBounds.size.x * pixelsPerUnit));
            height = Mathf.Max(1, Mathf.CeilToInt(maskBounds.size.y * pixelsPerUnit));
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        Color[] pixels = BuildMaskPixels(width, height, maskBounds, pixelsPerUnit, radius);

        texture.SetPixels(pixels);
        texture.Apply();

        Sprite maskSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            Vector2.zero,
            pixelsPerUnit);
        maskSprite.hideFlags = HideFlags.HideAndDontSave;

        GameObject maskObject = new GameObject(MaskObjectName);
        SpriteRenderer maskRenderer = maskObject.AddComponent<SpriteRenderer>();
        maskRenderer.sprite = maskSprite;

        TilemapRenderer sourceRenderer = GetComponent<TilemapRenderer>();
        if (sourceRenderer != null)
        {
            maskRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            maskRenderer.sortingOrder = sourceRenderer.sortingOrder + 5;
            maskRenderer.maskInteraction = sourceRenderer.maskInteraction;
        }

        maskObject.transform.SetParent(transform, false);
        maskObject.transform.localPosition = new Vector3(maskBounds.min.x, maskBounds.min.y, 0f);
        maskObject.transform.localRotation = Quaternion.identity;
        maskObject.transform.localScale = Vector3.one;

        maskDirty = false;
        hasBuiltMask = true;
    }

    private Color[] BuildMaskPixels(int width, int height, Bounds maskBounds, float pixelsPerUnit, float radius)
    {
        Color[] pixels = new Color[width * height];
        float radiusPixels = radius * pixelsPerUnit;
        int glowRadiusPixels = Mathf.CeilToInt(radiusPixels);
        float clampedCoreAlpha = Mathf.Clamp01(coreAlpha);
        float clampedGlowAlpha = Mathf.Clamp01(glowAlpha);

        foreach (Vector3Int cell in darknessCells)
        {
            Bounds cellBounds = GetCellLocalBounds(cell);
            int minX = Mathf.Max(0, Mathf.FloorToInt((cellBounds.min.x - maskBounds.min.x) * pixelsPerUnit));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt((cellBounds.max.x - maskBounds.min.x) * pixelsPerUnit) - 1);
            int minY = Mathf.Max(0, Mathf.FloorToInt((cellBounds.min.y - maskBounds.min.y) * pixelsPerUnit));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt((cellBounds.max.y - maskBounds.min.y) * pixelsPerUnit) - 1);

            for (int y = minY; y <= maxY; y++)
            {
                int row = y * width;
                float sampleY = maskBounds.min.y + (y + 0.5f) / pixelsPerUnit;
                for (int x = minX; x <= maxX; x++)
                {
                    float sampleX = maskBounds.min.x + (x + 0.5f) / pixelsPerUnit;
                    float coverage = CalculateDarknessCoverage(new Vector2(sampleX, sampleY), pixelsPerUnit);
                    float alpha = Mathf.Lerp(0f, clampedCoreAlpha, coverage);
                    int index = row + x;
                    if (alpha > pixels[index].a)
                    {
                        pixels[index] = new Color(0f, 0f, 0f, alpha);
                    }
                }
            }

            int glowMinX = Mathf.Max(0, minX - glowRadiusPixels);
            int glowMaxX = Mathf.Min(width - 1, maxX + glowRadiusPixels);
            int glowMinY = Mathf.Max(0, minY - glowRadiusPixels);
            int glowMaxY = Mathf.Min(height - 1, maxY + glowRadiusPixels);

            for (int y = glowMinY; y <= glowMaxY; y++)
            {
                int row = y * width;
                float sampleY = maskBounds.min.y + (y + 0.5f) / pixelsPerUnit;
                for (int x = glowMinX; x <= glowMaxX; x++)
                {
                    if (x >= minX && x <= maxX && y >= minY && y <= maxY)
                    {
                        continue;
                    }

                    float sampleX = maskBounds.min.x + (x + 0.5f) / pixelsPerUnit;
                    Vector2 sample = new Vector2(sampleX, sampleY);
                    float distance = DistanceToBounds(sample, cellBounds);
                    float edgeAlpha = CalculateCubicFalloff(distance, radius) * clampedGlowAlpha;
                    float coverage = CalculateDarknessCoverage(sample, pixelsPerUnit);
                    float alpha = Mathf.Lerp(edgeAlpha, clampedCoreAlpha, coverage);
                    int index = row + x;
                    if (alpha > pixels[index].a)
                    {
                        pixels[index] = new Color(0f, 0f, 0f, alpha);
                    }
                }
            }
        }

        return pixels;
    }

    private float CalculateDarknessCoverage(Vector2 localPosition, float pixelsPerUnit)
    {
        int coveredSamples = 0;
        float invPixelsPerUnit = 1f / pixelsPerUnit;
        for (int i = 0; i < CoverageSampleOffsets.Length; i++)
        {
            Vector2 offset = CoverageSampleOffsets[i];
            Vector2 sample = localPosition + new Vector2(
                (offset.x - 0.5f) * invPixelsPerUnit,
                (offset.y - 0.5f) * invPixelsPerUnit);
            if (darknessCells.Contains(tilemap.LocalToCell(sample)))
            {
                coveredSamples++;
            }
        }

        return coveredSamples / (float)CoverageSampleOffsets.Length;
    }

    private static float DistanceToBounds(Vector2 localPosition, Bounds bounds)
    {
        float dx = Mathf.Max(bounds.min.x - localPosition.x, 0f, localPosition.x - bounds.max.x);
        float dy = Mathf.Max(bounds.min.y - localPosition.y, 0f, localPosition.y - bounds.max.y);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private float CalculateCubicFalloff(float distance, float radius)
    {
        float t = Mathf.Clamp01(distance / radius);
        float falloff = 1f - Mathf.Pow(t, 3);
        return Mathf.Clamp01(glowAlpha) * falloff;
    }

    private void ClearDarknessMask()
    {
        Transform existing = transform.Find(MaskObjectName);
        if (existing == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(existing.gameObject);
        }
        else
        {
            DestroyImmediate(existing.gameObject);
        }
    }
}

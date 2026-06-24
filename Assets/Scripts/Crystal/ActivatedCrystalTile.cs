using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "ActivatedCrystalTile", menuName = "Tiles/Activated Crystal Tile")]
public class ActivatedCrystalTile : Tile
{
    [SerializeField] private Color glowColor = new Color(0.756f, 0.25f, 0.023f);
    [SerializeField] private Color particleColor = new Color(1f, 0.369f, 0.078f);

    public Color GlowColor => glowColor;
    public Color ParticleColor => particleColor;
}

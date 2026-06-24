using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "CrystalTile", menuName = "Tiles/Crystal Tile")]
public class CrystalTile : Tile, IFlameReactiveTile
{
    [SerializeField] private ActivatedCrystalTile activatedTile = null;
    [SerializeField] private Color glowColor = new Color(0.694f, 0.086f, 0.577f);
    [SerializeField] private Color particleColor = new Color(0.859f, 0.219f, 0.733f);
    [SerializeField] private int activationBurstCount = 120;

    public ActivatedCrystalTile ActivatedTile =>
        activatedTile != null ? activatedTile : TileLibrary.Get("activated_crystal") as ActivatedCrystalTile;
    public TileBase ActivatedTileBase => activatedTile != null ? activatedTile : TileLibrary.Get("activated_crystal");
    public Color GlowColor => glowColor;
    public Color ParticleColor => particleColor;
    public int ActivationBurstCount => activationBurstCount;

    public void OnFlameHit(Tilemap tilemap, Vector3Int cell, FlameProjectile projectile)
    {
        CrystalActivationHandler.RequestActivation(tilemap, cell, this);
    }
}

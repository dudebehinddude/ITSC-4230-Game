using UnityEngine;

// Legacy trigger refuel zone. Prefer painted OilTiles (see OilTileRefill).
[RequireComponent(typeof(Collider2D))]
public class FuelSource : MonoBehaviour
{
    [SerializeField] private FuelKind fuelKind = FuelKind.Orange;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryRefill(other);
    private void OnTriggerStay2D(Collider2D other) => TryRefill(other);

    private void TryRefill(Collider2D other)
    {
        var lantern = other.GetComponentInParent<PlayerLantern>();
        if (lantern != null)
        {
            lantern.Refill(fuelKind);
        }
    }
}

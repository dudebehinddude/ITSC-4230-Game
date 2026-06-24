using UnityEngine;

// Implement on colliders or tile behaviours that should react to thrown flame.
public interface IFlameProjectileTarget
{
    void OnFlameProjectileHit(FlameProjectile projectile, Collision2D collision);
}

namespace Quantum.Asteroids
{
  /// <summary>
  /// The <c>AsteroidsCollisionsSystem</c> class handles collision events in the Asteroids game,
  /// responding to projectile hits on ships and asteroids, as well as asteroid collisions with ships.
  /// </summary>
  public unsafe class AsteroidsCollisionsSystem : SystemSignalsOnly, ISignalOnCollisionEnter2D
  {
    /// <summary>
    /// Called when a 2D collision occurs. Determines the type of entities involved
    /// in the collision and delegates to the appropriate handler method.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="info">Information about the collision.</param>
    public void OnCollisionEnter2D(Frame f, CollisionInfo2D info)
    {
      if (f.Unsafe.TryGetPointer<AsteroidsProjectile>(info.Entity, out var projectile))
      {
        if (f.Has<AsteroidsShip>(info.Other))
        {
          HandleProjectileHitShip(f, info, projectile);
        }
        else if (f.TryGet<AsteroidsAsteroid>(info.Other, out var asteroid))
        {
          HandleProjectileHitAsteroid(f, info, projectile, asteroid);
        }
      }
      else if (f.Unsafe.TryGetPointer<AsteroidsAsteroid>(info.Entity, out var asteroid))
      {
        HandleAsteroidHitShip(f, info, asteroid);
      }
    }

    /// <summary>
    /// Handles the scenario where a projectile hits a ship. Increases the score of the ship that fired the projectile
    /// and signals the destruction of the hit ship.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="info">Information about the collision.</param>
    /// <param name="projectile">Pointer to the projectile involved in the collision.</param>
    private void HandleProjectileHitShip(Frame f, CollisionInfo2D info, AsteroidsProjectile* projectile)
    {
      if (projectile->Owner == info.Other)
      {
        info.IgnoreCollision = true;
        return;
      }
      
      if (f.Unsafe.TryGetPointer<AsteroidsShip>(projectile->Owner, out var ship)) {
        ship->Score++;
      }
      
      f.Signals.AsteroidsShipDestroyed(info.Other);
      f.Destroy(info.Entity);
    }
    
    /// <summary>
    /// Handles the scenario where a projectile hits an asteroid. Increases the score of the ship that fired the projectile,
    /// and potentially spawns new child asteroids.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="info">Information about the collision.</param>
    /// <param name="projectile">Pointer to the projectile involved in the collision.</param>
    /// <param name="asteroid">The asteroid involved in the collision.</param>
    private void HandleProjectileHitAsteroid(Frame f, CollisionInfo2D info, AsteroidsProjectile* projectile, AsteroidsAsteroid asteroid)
    {
      if (f.Unsafe.TryGetPointer<AsteroidsShip>(projectile->Owner, out var ship)) {
        ship->Score++;
      }
      
      if (asteroid.ChildAsteroid != null)
      {
        f.Signals.AsteroidsSpawnAsteroid(asteroid.ChildAsteroid, info.Other);
        f.Signals.AsteroidsSpawnAsteroid(asteroid.ChildAsteroid, info.Other);
      }

      f.Destroy(info.Entity);
      f.Destroy(info.Other);
    }

    /// <summary>
    /// Handles the scenario where an asteroid hits a ship. Signals the destruction of the ship.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="info">Information about the collision.</param>
    /// <param name="asteroid">Pointer to the asteroid involved in the collision.</param>
    private void HandleAsteroidHitShip(Frame f, CollisionInfo2D info, AsteroidsAsteroid* asteroid)
    {
      if (f.Has<AsteroidsShip>(info.Other))
      {
        f.Signals.AsteroidsShipDestroyed(info.Other);
      }
    }
  }
}
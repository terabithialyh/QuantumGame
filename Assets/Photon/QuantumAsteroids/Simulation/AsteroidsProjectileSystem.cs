namespace Quantum.Asteroids
{
  using Photon.Deterministic;

  /// <summary>
  /// The <c>AsteroidsProjectileSystem</c> class manages the lifecycle of projectiles,
  /// including updating their time-to-live (TTL) and handling projectile shooting signals.
  /// </summary>
  public unsafe class AsteroidsProjectileSystem : SystemMainThreadFilter<AsteroidsProjectileSystem.Filter>, ISignalAsteroidsShipShoot
  {
    /// <summary>
    /// The <c>Filter</c> struct represents the components required for the system's operations,
    /// including an entity reference and a pointer to its projectile component.
    /// </summary>
    public struct Filter
    {
      /// <summary>
      /// The reference to the entity being processed.
      /// </summary>
      public EntityRef Entity;
      
      /// <summary>
      /// Pointer to the entity's projectile component.
      /// </summary>
      public AsteroidsProjectile* Projectile;
    }

    /// <summary>
    /// Updates TTL of projectiles and destroying them if TTL reaches zero.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="filter">The filter containing the entity and its projectile component.</param>
    public override void Update(Frame f, ref Filter filter)
    {
      filter.Projectile->TTL -= f.DeltaTime;
      if (filter.Projectile->TTL <= 0)
      {
        f.Destroy(filter.Entity);
      }
    }

    /// <summary>
    /// Handles the shooting of a projectile by a ship. This method creates a new projectile,
    /// sets its initial position and velocity, and assigns it to the shooting ship.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="owner">The reference to the entity (ship) that is shooting the projectile.</param>
    public void AsteroidsShipShoot(Frame f, EntityRef owner)
    {
      AsteroidsGameConfig config = f.FindAsset(f.RuntimeConfig.GameConfig);
      EntityRef projectileEntity = f.Create(config.ProjectilePrototype);
      Transform2D* projectileTransform = f.Unsafe.GetPointer<Transform2D>(projectileEntity);
      Transform2D* ownerTransform = f.Unsafe.GetPointer<Transform2D>(owner);

      var relativeOffset = FPVector2.Up * config.ShotOffset;
      projectileTransform->Rotation = ownerTransform->Rotation;
      projectileTransform->Position = ownerTransform->TransformPoint(relativeOffset);
      
      AsteroidsProjectile* projectile = f.Unsafe.GetPointer<AsteroidsProjectile>(projectileEntity);
      projectile->TTL = config.ProjectileTTL;
      projectile->Owner = owner;

      PhysicsBody2D* body = f.Unsafe.GetPointer<PhysicsBody2D>(projectileEntity);
      body->Velocity = ownerTransform->Up * config.ProjectileInitialSpeed;
    }
  }
}
namespace Quantum.Asteroids
{
  using Photon.Deterministic;

  /// <summary>
  /// The <c>AsteroidsShipSystem</c> class manages the behavior of player-controlled ships,
  /// including movement, firing, and handling destruction and respawn.
  /// </summary>
  public unsafe class AsteroidsShipSystem : SystemMainThreadFilter<AsteroidsShipSystem.Filter>, ISignalAsteroidsShipDestroyed, ISignalAsteroidsSpawnShip
  {
    /// <summary>
    /// The <c>Filter</c> struct represents the components required for the system's operations,
    /// including an entity reference, transform, physics body, ship component, and player link component.
    /// </summary>
    public struct Filter
    {
      /// <summary>
      /// The reference to the entity being processed.
      /// </summary>
      public EntityRef Entity;
      
      /// <summary>
      /// Pointer to the entity's transform component.
      /// </summary>
      public Transform2D* Transform;
      
      /// <summary>
      /// Pointer to the entity's physics body component.
      /// </summary>
      public PhysicsBody2D* Body;
      
      /// <summary>
      /// Pointer to the entity's ship component.
      /// </summary>
      public AsteroidsShip* AsteroidsShip;
      
      /// <summary>
      /// Pointer to the entity's player link component.
      /// </summary>
      public AsteroidsPlayerLink* PlayerLink;
    }

    /// <summary>
    /// Updates the ship movement and firing, as well as respawning if needed.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="filter">The filter containing the entity and its components.</param>
    public override void Update(Frame f, ref Filter filter)
    {
      // The AsteroidsShipRespawn component marks that a ship is dead.
      if (f.Unsafe.TryGetPointer<AsteroidsShipRespawn>(filter.Entity, out var shipRespawn))
      {
        shipRespawn->RespawnTimer -= f.DeltaTime;
        if (shipRespawn->RespawnTimer <= 0)
        {
          f.Signals.AsteroidsSpawnShip(filter.Entity);
        }
        return;
      }

      AsteroidsGameConfig config = f.FindAsset(f.RuntimeConfig.GameConfig);
      Input* input = f.GetPlayerInput(filter.PlayerLink->PlayerRef);

      UpdateShipMovement(f, ref filter, input, config);

      UpdateShipFire(f, ref filter, input, config);
    }

    /// <summary>
    /// Updates the firing state of the ship, handling ammunition and firing intervals.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="filter">The filter containing the entity and its components.</param>
    /// <param name="input">The input state of the player.</param>
    /// <param name="config">The game configuration settings.</param>
    private void UpdateShipFire(Frame f, ref Filter filter, Input* input, AsteroidsGameConfig config)
    {
      if (input->Fire && filter.AsteroidsShip->AmmoCount >= 1 && filter.AsteroidsShip->FireInterval <= 0)
      {
        filter.AsteroidsShip->FireInterval = config.FireInterval;
        filter.AsteroidsShip->AmmoCount -= 1;
        f.Signals.AsteroidsShipShoot(filter.Entity);
      }
      else
      {
        filter.AsteroidsShip->FireInterval -= f.DeltaTime;
      }

      if (filter.AsteroidsShip->AmmoCount < config.MaxAmmo)
      {
        filter.AsteroidsShip->AmmoCount += f.DeltaTime / config.LoadAmmoTime;
      }
    }
    
    /// <summary>
    /// Updates the movement state of the ship, handling thrust and rotation based on player input.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="filter">The filter containing the entity and its components.</param>
    /// <param name="input">The input state of the player.</param>
    /// <param name="config">The game configuration settings.</param>
    private void UpdateShipMovement(Frame f, ref Filter filter, Input* input, AsteroidsGameConfig config)
    {
      if (input->Up)
      {
        filter.Body->AddForce(filter.Transform->Up * config.ShipAceleration);
      }

      FP turnSpeed = config.ShipTurnSpeed;
      if (input->Left)
      {
        filter.Body->AddTorque(turnSpeed);
      }

      if (input->Right)
      {
        filter.Body->AddTorque(-turnSpeed);
      }

      filter.Body->AngularVelocity = FPMath.Clamp(filter.Body->AngularVelocity, -turnSpeed, turnSpeed);
    }

    /// <summary>
    /// Spawns a new ship for the player, setting its initial position and resetting its state.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="shipEntity">The reference to the ship entity to be spawned.</param>
    public void AsteroidsSpawnShip(Frame f, EntityRef shipEntity)
    {
      f.Remove<AsteroidsShipRespawn>(shipEntity);
      
      AsteroidsGameConfig config = f.FindAsset(f.RuntimeConfig.GameConfig);
      Transform2D* transform = f.Unsafe.GetPointer<Transform2D>(shipEntity);
      transform->Position = AsteroidsUtils.GetRandomEdgePointOnCircle(f, config.ShipSpawnDistanceToCenter);
      transform->Teleport(f, transform);

      f.Unsafe.GetPointer<PhysicsBody2D>(shipEntity)->Velocity = default;
      f.Unsafe.GetPointer<PhysicsBody2D>(shipEntity)->AngularVelocity = default;
      f.Unsafe.GetPointer<AsteroidsShip>(shipEntity)->AmmoCount = config.MaxAmmo;
      f.Unsafe.GetPointer<PhysicsCollider2D>(shipEntity)->Enabled = true;
    }

    /// <summary>
    /// Handles the destruction of a ship, disabling its collider and initiating a respawn timer.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="shipEntity">The reference to the ship entity being destroyed.</param>
    public void AsteroidsShipDestroyed(Frame f, EntityRef shipEntity)
    {
      if (f.Has<AsteroidsShipRespawn>(shipEntity))
      {
        return;
      }

      f.Add<AsteroidsShipRespawn>(shipEntity, out var shipRespawn);
      shipRespawn->RespawnTimer = 2;

      f.Unsafe.GetPointer<AsteroidsShip>(shipEntity)->Score--;
      f.Unsafe.GetPointer<PhysicsCollider2D>(shipEntity)->Enabled = false;
    }
  }
}
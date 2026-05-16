namespace Quantum.Asteroids 
{
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The <c>AsteroidsGameConfig</c> class holds the configuration settings for the Asteroids game.
  /// These settings include parameters for ships, projectiles, asteroids, and the game map.
  /// </summary>
  public class AsteroidsGameConfig: AssetObject
  {
    [Header("Ship Configuration")]
    [Tooltip("The speed that the ship turns with added as torque")]
    public FP ShipTurnSpeed = 5;
    [Tooltip("The speed that the ship accelerates using add force")]
    public FP ShipAceleration = 10;
    [Tooltip("The time required to reload one projectile")]
    public FP LoadAmmoTime = FP._0_50;
    [Tooltip("The amount of accumulated ammo by ship")]
    public FP MaxAmmo = 10;
    [Tooltip("Time interval between ship shots")]
    public FP FireInterval = 1;
    [Tooltip("Distance to the center of the map. This value is the radius in a random circular location where the ship is spawned")]
    public FP ShipSpawnDistanceToCenter = 15;
    
    [Header("Projectile configuration")]
    [Tooltip("Prototype reference to spawn ship projectiles")]
    public AssetRef<EntityPrototype> ProjectilePrototype;
    [Tooltip("Speed applied to the projectile when spawned")]
    public FP ProjectileInitialSpeed = 15;
    [Tooltip("Time until destroy the projectile")]
    public FP ProjectileTTL = 1;
    [Tooltip("Displacement of the projectile spawn position related to the ship position")]
    public FP ShotOffset = 1;
    
    [Header("Asteroids configuration")]
    [Tooltip("Prototype reference to spawn asteroids")]
    public AssetRef<EntityPrototype> AsteroidPrototype;
    [Tooltip("Speed applied to the asteroid when spawned")]
    public FP AsteroidInitialSpeed = 3;
    [Tooltip("Minimum torque applied to the asteroid when spawned")]
    public FP AsteroidInitialTorqueMin = 7;
    [Tooltip("Maximum torque applied to the asteroid when spawned")]
    public FP AsteroidInitialTorqueMax = 20;
    [Tooltip("Distance to the center of the map. This value is the radius in a random circular location where the asteroid is spawned")]
    public FP AsteroidSpawnDistanceToCenter = 20;
    [Tooltip("Amount of asteroids spawned in level 1. In each level, the number os asteroids spawned is increased by one")]
    public int InitialAsteroidsCount = 5;

    [Header("Map configuration")]
    [Tooltip("Total size of the map. This is used to calculate when an entity is outside de gameplay area and then wrap it to the other side")]
    public FPVector2 GameMapSize = new FPVector2(25, 25);

    private FPVector2 _mapExtends;

    /// <summary>
    /// Gets the half-size of the map, used for boundary calculations.
    /// </summary>
    private FPVector2 MapExtends => _mapExtends;

    /// <summary>
    /// Called when the asset is loaded. Initializes the map extents based on the game map size.
    /// </summary>
    /// <param name="resourceManager">The resource manager used to load assets.</param>
    /// <param name="allocator">The memory allocator used during loading.</param>
    public override void Loaded(IResourceManager resourceManager, Native.Allocator allocator)
    {
      base.Loaded(resourceManager, allocator);

      _mapExtends = GameMapSize / 2;
    }

    /// <summary>
    /// Checks if a position is out of bounds and provides a warped position.
    /// When the entity leaves the bounds, it will emerge on the other side.
    /// </summary>
    /// <param name="position">The position to check.</param>
    /// <param name="newPosition">The new position after wrapping around the map bounds.</param>
    /// <returns><c>true</c> if the position was out of bounds and was warped; otherwise, <c>false</c>.</returns>
    public bool IsOutOfBounds(FPVector2 position, out FPVector2 newPosition)
    {
      newPosition = position;

      if (position.X >= -MapExtends.X && position.X <= MapExtends.X &&
          position.Y >= -MapExtends.Y && position.Y <= MapExtends.Y)
      {
        // position is inside map bounds
        return false;
      }

      // warp x position
      if (position.X < -MapExtends.X)
      {
        newPosition.X = MapExtends.X;
      }
      else if (position.X > MapExtends.X)
      {
        newPosition.X = -MapExtends.X;
      }

      // warp y position
      if (position.Y < -MapExtends.Y)
      {
        newPosition.Y = MapExtends.Y;
      }
      else if (position.Y > MapExtends.Y)
      {
        newPosition.Y = -MapExtends.Y;
      }

      return true;
    }
  }
}
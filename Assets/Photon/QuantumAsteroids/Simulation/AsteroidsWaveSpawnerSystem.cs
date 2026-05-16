namespace Quantum.Asteroids
{

  /// <summary>
  /// The <c>AsteroidsWaveSpawnerSystem</c> class is responsible for spawning waves of asteroids
  /// and managing the asteroid spawning logic.
  /// </summary>
  public unsafe class AsteroidsWaveSpawnerSystem : SystemSignalsOnly,
    ISignalOnComponentRemoved<AsteroidsAsteroid>, ISignalAsteroidsSpawnAsteroid
  {
    /// <summary>
    /// Initializes the wave spawner system by setting the initial wave count and spawning the first wave of asteroids.
    /// </summary>
    /// <param name="f">The game frame.</param>
    public override void OnInit(Frame f)
    {
      f.Global->AsteroidsWaveCount = 0;
      SpawnAsteroidWave(f);
    }

    /// <summary>
    /// Handles the removal of an asteroid component. If there are no asteroids remaining,
    /// spawns a new wave of asteroids.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="entity">The entity from which the asteroid component was removed.</param>
    /// <param name="component">The removed asteroid component.</param>
    public void OnRemoved(Frame f, EntityRef entity, AsteroidsAsteroid* component)
    {
      if (f.ComponentCount<AsteroidsAsteroid>(false) <= 1)
      {
        SpawnAsteroidWave(f);
      }
    }

    /// <summary>
    /// Spawns a wave of asteroids based on the current wave count.
    /// </summary>
    /// <param name="f">The game frame.</param>
    private void SpawnAsteroidWave(Frame f)
    {
      AsteroidsGameConfig config = f.FindAsset(f.RuntimeConfig.GameConfig);
      for (int i = 0; i < f.Global->AsteroidsWaveCount + config.InitialAsteroidsCount; i++)
      {
        f.Signals.AsteroidsSpawnAsteroid(config.AsteroidPrototype, EntityRef.None);
      }

      f.Global->AsteroidsWaveCount++;
    }

    /// <summary>
    /// Spawns an asteroid entity at a random position on the edge of the game area or near a specified parent entity.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="childPrototype">The prototype of the asteroid entity to spawn.</param>
    /// <param name="parent">The parent entity near which to spawn the asteroid (or EntityRef.None for random edge spawn).</param>
    public void AsteroidsSpawnAsteroid(Frame f, AssetRef<EntityPrototype> childPrototype, EntityRef parent)
    {
      AsteroidsGameConfig config = f.FindAsset(f.RuntimeConfig.GameConfig);
      EntityRef asteroid = f.Create(childPrototype);
      Transform2D* asteroidTransform = f.Unsafe.GetPointer<Transform2D>(asteroid);

      if (parent == EntityRef.None)
      {
        asteroidTransform->Position = AsteroidsUtils.GetRandomEdgePointOnCircle(f, config.AsteroidSpawnDistanceToCenter);
      }
      else
      {
        asteroidTransform->Position = f.Get<Transform2D>(parent).Position;
      }

      asteroidTransform->Rotation = AsteroidsUtils.GetRandomRotation(f);
      if (f.Unsafe.TryGetPointer<PhysicsBody2D>(asteroid, out var body))
      {
        body->Velocity = asteroidTransform->Up * config.AsteroidInitialSpeed;
        body->AddTorque(f.RNG->Next(config.AsteroidInitialTorqueMin, config.AsteroidInitialTorqueMax));
      }
    }
  }
}
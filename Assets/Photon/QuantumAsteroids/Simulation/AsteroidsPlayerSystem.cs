namespace Quantum.Asteroids {

  /// <summary>
  /// The <c>AsteroidsPlayerSystem</c> class handles the addition of new players to the game,
  /// including creating and spawning their ship entities.
  /// </summary>
  public unsafe class AsteroidsPlayerSystem : SystemSignalsOnly, ISignalOnPlayerAdded, ISignalOnPlayerDisconnected {
    /// <summary>
    /// Called when a new player is added to the game. This method creates a ship entity for the player,
    /// sets up the player link component, and triggers the ship spawn signal.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="player">The reference to the player being added.</param>
    /// <param name="firstTime">Indicates if this is the first time the player is added.</param>
    public void OnPlayerAdded(Frame f, PlayerRef player, bool firstTime) {
      RuntimePlayer data = f.GetPlayerData(player);

      // Create a ship entity from the provided prototype or the default prototype from the RuntimeConfig
      var playerAvatarAssetRef = data.PlayerAvatar.IsValid ? data.PlayerAvatar : f.RuntimeConfig.DefaultPlayerAvatar;
      EntityPrototype shipPrototypeAsset = f.FindAsset(playerAvatarAssetRef);
      EntityRef shipEntity = f.Create(shipPrototypeAsset);

      // Set player link component to mark this entity as player controller
      f.Set(shipEntity, new AsteroidsPlayerLink { PlayerRef = player });

      // Spawn the ship
      f.Signals.AsteroidsSpawnShip(shipEntity);
    }

    /// <summary>
    /// Called when a new player is disconnected. This callback handles the ship destruction.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="player">The reference to the player being desconnected.</param>
    public void OnPlayerDisconnected(Frame f, PlayerRef player) {
      foreach (var pair in f.GetComponentIterator<AsteroidsPlayerLink>())
      {
        if (pair.Component.PlayerRef == player) {
          f.Destroy(pair.Entity);
        }
      }
    }
  }
}
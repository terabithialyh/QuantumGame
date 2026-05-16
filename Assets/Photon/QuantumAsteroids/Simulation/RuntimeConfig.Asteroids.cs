namespace Quantum
{
  /// <summary>
  /// Partial class <c>RuntimeConfig</c> contains configuration settings for the runtime behavior of the game.
  /// </summary>
  public partial class RuntimeConfig
  {
    /// <summary>
    /// Reference to the game configuration asset, specifying various settings for the Asteroids game.
    /// </summary>
    public AssetRef<Asteroids.AsteroidsGameConfig> GameConfig;
    /// <summary>
    /// Reference to the default player avatar prototype to be used when creating player entities.
    /// </summary>
    public AssetRef<EntityPrototype> DefaultPlayerAvatar;
  }
}
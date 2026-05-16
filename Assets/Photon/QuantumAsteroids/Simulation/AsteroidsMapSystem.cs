namespace Quantum.Asteroids
{
  using Photon.Deterministic;

  /// <summary>
  /// The <c>AsteroidsMapSystem</c> class handles the logic for wrapping entities around the map boundaries
  /// in the Asteroids game. It ensures that entities reappear on the opposite side when they move out of bounds.
  /// </summary>
  public unsafe class AsteroidsMapSystem : SystemMainThreadFilter<AsteroidsMapSystem.Filter>
  {
    /// <summary>
    /// The <c>Filter</c> struct represents the components required for the system's operations,
    /// including an entity reference and its transform.
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
    }

    /// <summary>
    /// Updates the state of the system by checking if entities are out of bounds and wrapping them if necessary.
    /// </summary>
    /// <param name="f">The game frame.</param>
    /// <param name="filter">The filter containing the entity and its transform.</param>
    public override void Update(Frame f, ref Filter filter)
    {
      AsteroidsGameConfig config = f.FindAsset(f.RuntimeConfig.GameConfig);

      if (config.IsOutOfBounds(filter.Transform->Position, out FPVector2 newPosition))
      {
        filter.Transform->Teleport(f, newPosition);
      }
    }
  }
}
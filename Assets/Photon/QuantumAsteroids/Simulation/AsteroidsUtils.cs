namespace Quantum.Asteroids
{
  using Photon.Deterministic;

  /// <summary>
  /// The <c>AsteroidsUtils</c> static class provides utility functions for common tasks in the Asteroids game,
  /// such as generating random rotations and positions on a circle's edge.
  /// </summary>
  public static unsafe class AsteroidsUtils
  {
    /// <summary>
    /// Generates a random rotation angle between 0 and 360 degrees.
    /// </summary>
    /// <param name="f">The game frame, which provides access to the random number generator.</param>
    /// <returns>A random rotation angle in degrees.</returns>
    public static FP GetRandomRotation(Frame f)
    {
      return f.RNG->Next(0, 360);
    }

    /// <summary>
    /// Calculates a random point on the edge of a circle with the specified radius.
    /// </summary>
    /// <param name="f">The game frame, which provides access to the random number generator.</param>
    /// <param name="radius">The radius of the circle.</param>
    /// <returns>A random point on the edge of the circle.</returns>
    public static FPVector2 GetRandomEdgePointOnCircle(Frame f, FP radius)
    {
      return FPVector2.Rotate(FPVector2.Up * radius , f.RNG->Next() * FP.PiTimes2);
    }
  }
}

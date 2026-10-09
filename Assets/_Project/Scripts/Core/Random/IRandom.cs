namespace Pathfinder.Core.Random
{
    /// <summary>
    /// Deterministic source of random bits. Implementations must produce the same sequence
    /// for the same seed on every platform and scripting backend (GDD §14.3).
    /// </summary>
    /// <remarks>
    /// The interface is deliberately minimal: every derived value (ranges, floats, picks,
    /// shuffles) is built in <see cref="RandomExtensions"/> on top of <see cref="NextUInt"/>,
    /// so swapping the generator can never change how those values are derived.
    /// </remarks>
    public interface IRandom
    {
        /// <summary>Returns the next uniformly distributed 32-bit value.</summary>
        uint NextUInt();
    }
}

namespace Pathfinder.Game.Enemies
{
    /// <summary>How far a burrowing enemy has sunk into the ground: 0 on the surface, 1 fully underground. For visuals.</summary>
    public interface ISinkDepth
    {
        float SinkDepth { get; }
    }
}

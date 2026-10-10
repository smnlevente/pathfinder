namespace Pathfinder.Game.Enemies
{
    /// <summary>A boss made of several separate enemies, such as the Binary Wardens. The HUD shows a bar for each part.</summary>
    public interface IMultiPartBoss
    {
        int PartCount { get; }

        /// <summary>The part's enemy, or null once it is gone.</summary>
        Enemy Part(int index);

        string PartName(int index);

        /// <summary>Seconds until a downed part comes back; 0 while it is up.</summary>
        float PartDowntime(int index);
    }
}

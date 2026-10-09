namespace Pathfinder.Core.Combat
{
    /// <summary>Which side an entity or projectile fights for. Projectiles only hit the opposing team.</summary>
    public enum Team
    {
        Player,
        Enemy,
    }

    public static class TeamExtensions
    {
        public static Team Opponent(this Team team) => team == Team.Player ? Team.Enemy : Team.Player;
    }
}

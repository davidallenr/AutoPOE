using ExileCore.Shared.Enums;

namespace AutoPOE.Logic.Combat
{
    /// <summary>
    /// Defines targeting priority for specific monster types or names
    /// </summary>
    public class TargetPriority
    {
        /// <summary>
        /// Whether this priority rule is active
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Monster rarity to target (White, Magic, Rare, Unique)
        /// </summary>
        public MonsterRarity TargetRarity { get; set; } = MonsterRarity.Rare;

        /// <summary>
        /// Specific monster names to prioritize (e.g., "Kosis", "Omniphobia")
        /// Empty means any monster of the specified rarity
        /// </summary>
        public List<string> SpecificNames { get; set; } = new List<string>();

        /// <summary>
        /// Priority level (1 = highest priority, higher numbers = lower priority)
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Minimum health percentage required (0-100)
        /// Useful for ignoring nearly-dead targets
        /// </summary>
        public float MinHealthPercent { get; set; } = 0f;

        /// <summary>
        /// Maximum distance from player (in grid units)
        /// </summary>
        public float MaxDistance { get; set; } = 100f;
    }
}

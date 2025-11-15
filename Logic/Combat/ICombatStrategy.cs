using System.Numerics;
using static AutoPOE.Settings.Skill;

namespace AutoPOE.Logic.Combat
{
    /// <summary>
    /// Defines combat behavior strategies for different build types
    /// </summary>
    public interface ICombatStrategy
    {
        /// <summary>
        /// Name of the strategy (e.g., "Standard", "Cast on Crit", "Channelling")
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Selects the best target to attack based on strategy-specific logic
        /// </summary>
        /// <param name="monsters">List of available hostile monsters</param>
        /// <returns>Grid position of selected target, or null if no valid target</returns>
        Task<Vector2?> SelectTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters);

        /// <summary>
        /// Whether this strategy should kite/maintain distance from enemies
        /// </summary>
        bool ShouldKite { get; }

        /// <summary>
        /// Preferred distance to maintain from enemies (in grid units)
        /// </summary>
        int KiteDistance { get; }

        /// <summary>
        /// Additional context about why this target was selected (for debugging)
        /// </summary>
        string LastTargetReason { get; }

        /// <summary>
        /// Gets the recommended skill for the current combat situation
        /// </summary>
        /// <param name="targetEntity">The target entity (can be null)</param>
        /// <param name="playerHealth">Player health percentage</param>
        /// <param name="enemyCount">Number of enemies nearby</param>
        /// <returns>Recommended skill or null</returns>
        Task<Settings.Skill?> GetRecommendedSkill(ExileCore.PoEMemory.MemoryObjects.Entity? targetEntity, float playerHealth, int enemyCount);

        /// <summary>
        /// Determines if defensive skills should be prioritized
        /// </summary>
        /// <param name="playerHealth">Player health percentage</param>
        /// <param name="enemyCount">Number of enemies nearby</param>
        /// <returns>True if defensive priority should be used</returns>
        bool ShouldPrioritizeDefensive(float playerHealth, int enemyCount);

        /// <summary>
        /// Gets the preferred target priority for this strategy
        /// </summary>
        /// <returns>Target priority enum</returns>
        TargetPrioritySort GetTargetPriority();

        /// <summary>
        /// Gets the maximum combat range for this strategy
        /// </summary>
        /// <returns>Maximum range in units</returns>
        int GetMaxCombatRange();

        /// <summary>
        /// Indicates whether the strategy is currently locked onto a high-priority target (e.g., boss)
        /// When true, repositioning logic should be disabled
        /// </summary>
        /// <returns>True if locked onto a priority target</returns>
        bool IsLockedOnPriorityTarget();
    }
}

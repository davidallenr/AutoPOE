using System.Numerics;

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
    }
}

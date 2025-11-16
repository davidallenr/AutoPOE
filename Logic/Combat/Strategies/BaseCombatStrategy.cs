using ExileCore.Shared.Enums;
using System.Numerics;
using static AutoPOE.Settings.Skill;

namespace AutoPOE.Logic.Combat.Strategies
{
    /// <summary>
    /// Base class for combat strategies with shared logic
    /// </summary>
    public abstract class BaseCombatStrategy : ICombatStrategy
    {
        public abstract string Name { get; }
        public abstract bool ShouldKite { get; }
        public abstract int KiteDistance { get; }
        public abstract string LastTargetReason { get; protected set; }

        /// <summary>
        /// Selects the best target - implemented by derived strategies
        /// </summary>
        public abstract Task<Vector2?> SelectTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters);

        /// <summary>
        /// Gets the recommended skill with common defensive/buff logic
        /// </summary>
        public virtual Task<Settings.Skill?> GetRecommendedSkill(
            ExileCore.PoEMemory.MemoryObjects.Entity? targetEntity, 
            float playerHealth, 
            int enemyCount)
        {
            // Priority 1: Defensive skills if needed
            if (ShouldPrioritizeDefensive(playerHealth, enemyCount))
            {
                var defensiveSkill = Core.Settings.GetSkillsByRole(SkillRoleSort.Defensive).FirstOrDefault();
                if (defensiveSkill != null)
                {
                    return Task.FromResult<Settings.Skill?>(defensiveSkill);
                }
            }

            // Priority 2: Buff maintenance
            var buffSkill = GetBuffSkillIfNeeded(playerHealth);
            if (buffSkill != null)
            {
                return Task.FromResult<Settings.Skill?>(buffSkill);
            }

            // Priority 3: Damage skills - delegated to derived strategy
            return GetDamageSkill(targetEntity, enemyCount);
        }

        /// <summary>
        /// Gets buff skill if needed - can be overridden for strategy-specific conditions
        /// </summary>
        protected virtual Settings.Skill? GetBuffSkillIfNeeded(float playerHealth)
        {
            if (Core.Settings.Combat.MaintainBuffs.Value)
            {
                return Core.Settings.GetSkillsByRole(SkillRoleSort.Buff).FirstOrDefault();
            }
            return null;
        }

        /// <summary>
        /// Gets damage skill for current situation - must be implemented by derived strategies
        /// </summary>
        protected abstract Task<Settings.Skill?> GetDamageSkill(
            ExileCore.PoEMemory.MemoryObjects.Entity? targetEntity, 
            int enemyCount);

        /// <summary>
        /// Determines if defensive skills should be prioritized - implemented by derived strategies
        /// </summary>
        public abstract bool ShouldPrioritizeDefensive(float playerHealth, int enemyCount);

        /// <summary>
        /// Gets the preferred target priority (shared implementation)
        /// </summary>
        public virtual TargetPrioritySort GetTargetPriority()
        {
            return Core.Settings.Combat.FocusFire.Value ?
                TargetPrioritySort.HighestThreat :
                TargetPrioritySort.MostEnemies;
        }

        /// <summary>
        /// Gets the maximum combat range - implemented by derived strategies
        /// </summary>
        public abstract int GetMaxCombatRange();

        /// <summary>
        /// Indicates if locked onto a priority target - implemented by derived strategies
        /// </summary>
        public abstract bool IsLockedOnPriorityTarget();

        /// <summary>
        /// Shared helper: Filters monsters to valid combat targets within range
        /// </summary>
        protected List<ExileCore.PoEMemory.MemoryObjects.Entity> FilterValidMonsters(
            List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters,
            Vector2 playerPos,
            int maxRange)
        {
            return monsters
                .Where(m => m.IsHostile && m.IsTargetable && m.IsAlive)
                .Where(m => Vector2.Distance(m.GridPosNum, playerPos) <= maxRange)
                .ToList();
        }
    }
}

using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System.Numerics;
using static AutoPOE.Settings.Skill;

namespace AutoPOE.Logic.Combat.Strategies
{
    /// <summary>
    /// Standard combat strategy - targets highest rarity monsters within range
    /// </summary>
    public class StandardCombatStrategy : ICombatStrategy
    {
        public string Name => "Standard";
        public bool ShouldKite => false; // Standard strategy doesn't kite
        public int KiteDistance => 0;
        public string LastTargetReason { get; private set; } = "No target selected yet";

        public Task<Vector2?> SelectTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters)
        {
            // Filter to valid hostile monsters within combat distance
            var maxRange = GetMaxCombatRange();
            var validMonsters = monsters
                .Where(m => m.IsHostile
                    && m.IsTargetable
                    && m.IsAlive
                    && m.GridPosNum.Distance(Core.GameController.Player.GridPosNum) < maxRange)
                .ToList();

            if (!validMonsters.Any())
            {
                LastTargetReason = "No valid monsters in range";
                return Task.FromResult<Vector2?>(null);
            }

            // Select highest rarity monster (Unique > Rare > Magic > White)
            var bestTarget = validMonsters
                .OrderByDescending(m => Navigation.Map.GetMonsterRarityWeight(m.Rarity))
                .ThenBy(m => m.GridPosNum.Distance(Core.GameController.Player.GridPosNum)) // Prefer closer if same rarity
                .FirstOrDefault();

            if (bestTarget == null)
            {
                LastTargetReason = "No valid target found";
                return Task.FromResult<Vector2?>(null);
            }

            // Set reason for debugging
            var distance = bestTarget.GridPosNum.Distance(Core.GameController.Player.GridPosNum);
            LastTargetReason = $"{bestTarget.Rarity} monster at {distance:F1} units";

            if (!string.IsNullOrEmpty(bestTarget.RenderName))
            {
                LastTargetReason += $" ({bestTarget.RenderName})";
            }

            return Task.FromResult<Vector2?>(bestTarget.GridPosNum);
        }

        public Task<Settings.Skill?> GetRecommendedSkill(ExileCore.PoEMemory.MemoryObjects.Entity? targetEntity, float playerHealth, int enemyCount)
        {
            // Check if defensive skills are needed first
            if (ShouldPrioritizeDefensive(playerHealth, enemyCount))
            {
                var defensiveSkill = Core.Settings.GetSkillsByRole(SkillRoleSort.Defensive).FirstOrDefault();
                if (defensiveSkill != null)
                {
                    return Task.FromResult<Settings.Skill?>(defensiveSkill);
                }
            }

            // Maintain buffs if needed
            if (Core.Settings.Combat.MaintainBuffs.Value)
            {
                var buffSkill = Core.Settings.GetSkillsByRole(SkillRoleSort.Buff).FirstOrDefault();
                if (buffSkill != null)
                {
                    return Task.FromResult<Settings.Skill?>(buffSkill);
                }
            }

            // Select appropriate damage skill based on target and enemy count
            var targetPriority = GetTargetPriority();
            var preferredRole = enemyCount > 3 ? SkillRoleSort.AreaDamage : SkillRoleSort.SingleTarget;

            // If we have a specific target, we can be more selective
            if (targetEntity != null)
            {
                // Use single target for rares/uniques, area for groups
                preferredRole = (targetEntity.Rarity == MonsterRarity.Rare || targetEntity.Rarity == MonsterRarity.Unique)
                    ? SkillRoleSort.SingleTarget
                    : SkillRoleSort.AreaDamage;
            }

            var skill = Core.Settings.GetBestSkillForTarget(targetPriority, preferredRole);
            return Task.FromResult(skill);
        }

        public bool ShouldPrioritizeDefensive(float playerHealth, int enemyCount)
        {
            var threshold = Core.Settings.Combat.DefensiveThreshold.Value;
            return playerHealth < threshold || (enemyCount > 5 && playerHealth < 75);
        }

        public TargetPrioritySort GetTargetPriority()
        {
            return Core.Settings.Combat.FocusFire.Value ? TargetPrioritySort.HighestThreat : TargetPrioritySort.MostEnemies;
        }

        public int GetMaxCombatRange()
        {
            return Core.Settings.CombatDistance.Value;
        }

        public bool IsLockedOnPriorityTarget()
        {
            // Standard strategy doesn't have priority target locking
            return false;
        }
    }
}

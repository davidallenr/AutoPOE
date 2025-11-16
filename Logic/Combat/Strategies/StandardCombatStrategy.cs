using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System.Numerics;
using static AutoPOE.Settings.Skill;

namespace AutoPOE.Logic.Combat.Strategies
{
    /// <summary>
    /// Standard combat strategy - targets highest rarity monsters within range
    /// </summary>
    public class StandardCombatStrategy : BaseCombatStrategy
    {
        public override string Name => "Standard";
        public override bool ShouldKite => false; // Standard strategy doesn't kite
        public override int KiteDistance => 0;
        public override string LastTargetReason { get; protected set; } = "No target selected yet";

        public override Task<Vector2?> SelectTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters)
        {
            // Filter to valid hostile monsters within combat distance
            var maxRange = GetMaxCombatRange();
            var playerPos = Core.GameController.Player.GridPosNum;
            var validMonsters = FilterValidMonsters(monsters, playerPos, maxRange);

            if (!validMonsters.Any())
            {
                LastTargetReason = "No valid monsters in range";
                return Task.FromResult<Vector2?>(null);
            }

            // Select highest rarity monster (Unique > Rare > Magic > White)
            var bestTarget = validMonsters
                .OrderByDescending(m => Navigation.Map.GetMonsterRarityWeight(m.Rarity))
                .ThenBy(m => m.GridPosNum.Distance(playerPos)) // Prefer closer if same rarity
                .FirstOrDefault();

            if (bestTarget == null)
            {
                LastTargetReason = "No valid target found";
                return Task.FromResult<Vector2?>(null);
            }

            // Set reason for debugging
            var distance = bestTarget.GridPosNum.Distance(playerPos);
            LastTargetReason = $"{bestTarget.Rarity} monster at {distance:F1} units";

            if (!string.IsNullOrEmpty(bestTarget.RenderName))
            {
                LastTargetReason += $" ({bestTarget.RenderName})";
            }

            return Task.FromResult<Vector2?>(bestTarget.GridPosNum);
        }

        protected override Task<Settings.Skill?> GetDamageSkill(
            ExileCore.PoEMemory.MemoryObjects.Entity? targetEntity, 
            int enemyCount)
        {
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

        public override bool ShouldPrioritizeDefensive(float playerHealth, int enemyCount)
        {
            var threshold = Core.Settings.Combat.DefensiveThreshold.Value;
            return playerHealth < threshold || (enemyCount > 5 && playerHealth < 75);
        }

        public override int GetMaxCombatRange()
        {
            return Core.Settings.CombatDistance.Value;
        }

        public override bool IsLockedOnPriorityTarget()
        {
            // Standard strategy doesn't have priority target locking
            return false;
        }
    }
}

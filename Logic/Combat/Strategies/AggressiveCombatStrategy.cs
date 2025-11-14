using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System.Numerics;
using static AutoPOE.Settings.Skill;

namespace AutoPOE.Logic.Combat.Strategies
{
    /// <summary>
    /// Aggressive combat strategy - prioritizes high damage output and targets groups
    /// </summary>
    public class AggressiveCombatStrategy : ICombatStrategy
    {
        public string Name => "Aggressive";
        public bool ShouldKite => false; // Aggressive builds stay in close combat
        public int KiteDistance => 0;
        public string LastTargetReason { get; private set; } = "No target selected yet";

        private Vector2? _lastTargetPosition;
        private DateTime _lastTargetTime = DateTime.MinValue;
        private const float TARGET_STABILITY_DURATION = 0.8f; // Reduced duration for more responsive targeting

        public Task<Vector2?> SelectTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters)
        {
            var maxRange = GetMaxCombatRange();
            var playerPos = Core.GameController.Player.GridPosNum;
            var validMonsters = monsters
                .Where(m => m.IsHostile
                    && m.IsTargetable
                    && m.IsAlive
                    && m.GridPosNum.Distance(playerPos) < maxRange)
                .ToList();

            if (!validMonsters.Any())
            {
                LastTargetReason = "No valid monsters in range";
                _lastTargetPosition = null;
                _lastTargetTime = DateTime.MinValue; // Reset timing when no monsters
                return Task.FromResult<Vector2?>(null);
            }

            // If we have a recent target position and there are still enemies near it, stay focused
            if (_lastTargetPosition.HasValue &&
                DateTime.Now < _lastTargetTime.AddSeconds(TARGET_STABILITY_DURATION))
            {
                var nearCurrentTarget = validMonsters
                    .Where(m => m.GridPosNum.Distance(_lastTargetPosition.Value) < 20)
                    .ToList();

                if (nearCurrentTarget.Any())
                {
                    // Find best target in current area (prefer rare/unique, then closest)
                    var stableTarget = nearCurrentTarget
                        .OrderByDescending(m => Navigation.Map.GetMonsterRarityWeight(m.Rarity))
                        .ThenBy(m => m.GridPosNum.Distance(_lastTargetPosition.Value))
                        .FirstOrDefault();

                    if (stableTarget != null)
                    {
                        var distance = stableTarget.GridPosNum.Distance(playerPos);
                        LastTargetReason = $"Focused on current area ({stableTarget.Rarity}) at {distance:F1} units";
                        return Task.FromResult<Vector2?>(stableTarget.GridPosNum);
                    }
                }
                else
                {
                    // No monsters near last target, reset timing to find new area faster
                    _lastTargetTime = DateTime.MinValue;
                }
            }

            // Prioritize closer targets to avoid getting stuck at map edges
            var closeTargets = validMonsters.Where(m => m.GridPosNum.Distance(playerPos) < maxRange * 0.7f).ToList();
            var targetsToConsider = closeTargets.Any() ? closeTargets : validMonsters;

            // If we've been targeting the same area for too long without success, try closer monsters only
            if (_lastTargetPosition.HasValue &&
                DateTime.Now > _lastTargetTime.AddSeconds(TARGET_STABILITY_DURATION * 2) &&
                closeTargets.Any())
            {
                targetsToConsider = closeTargets;
                LastTargetReason = "Switching to closer targets due to timeout";
                _lastTargetTime = DateTime.MinValue; // Reset timing
            }

            // Find new target area - center of largest group
            var bestTarget = targetsToConsider
                .Select(m => new
                {
                    Monster = m,
                    NearbyCount = targetsToConsider.Count(other => other.GridPosNum.Distance(m.GridPosNum) < 20)
                })
                .OrderByDescending(x => x.NearbyCount) // Prefer groups
                .ThenByDescending(x => Navigation.Map.GetMonsterRarityWeight(x.Monster.Rarity)) // Then rarity
                .ThenBy(x => x.Monster.GridPosNum.Distance(playerPos)) // Then distance
                .FirstOrDefault();

            if (bestTarget == null)
            {
                LastTargetReason = "No valid target found";
                _lastTargetPosition = null;
                return Task.FromResult<Vector2?>(null);
            }

            // Update target tracking
            _lastTargetPosition = bestTarget.Monster.GridPosNum;
            _lastTargetTime = DateTime.Now;

            var targetDistance = bestTarget.Monster.GridPosNum.Distance(playerPos);
            LastTargetReason = $"New group: {bestTarget.NearbyCount} monsters ({bestTarget.Monster.Rarity}) at {targetDistance:F1} units";

            return Task.FromResult<Vector2?>(bestTarget.Monster.GridPosNum);
        }

        public Task<Settings.Skill?> GetRecommendedSkill(ExileCore.PoEMemory.MemoryObjects.Entity? targetEntity, float playerHealth, int enemyCount)
        {
            // Only prioritize defensive if health is critically low (more aggressive threshold)
            if (ShouldPrioritizeDefensive(playerHealth, enemyCount))
            {
                var defensiveSkill = Core.Settings.GetSkillsByRole(SkillRoleSort.Defensive).FirstOrDefault();
                if (defensiveSkill != null)
                {
                    return Task.FromResult<Settings.Skill?>(defensiveSkill);
                }
            }

            // Aggressive builds prioritize damage over buffs, but still maintain critical buffs
            if (Core.Settings.Combat.MaintainBuffs.Value && playerHealth > 70)
            {
                var buffSkill = Core.Settings.GetSkillsByRole(SkillRoleSort.Buff).FirstOrDefault();
                if (buffSkill != null)
                {
                    return Task.FromResult<Settings.Skill?>(buffSkill);
                }
            }

            // Always prefer area damage for aggressive clearing
            var preferredRole = enemyCount > 1 ? SkillRoleSort.AreaDamage : SkillRoleSort.PrimaryDamage;
            var targetPriority = TargetPrioritySort.MostEnemies; // Always target groups

            var skill = Core.Settings.GetBestSkillForTarget(targetPriority, preferredRole);
            return Task.FromResult(skill);
        }

        public bool ShouldPrioritizeDefensive(float playerHealth, int enemyCount)
        {
            // More aggressive threshold - only go defensive when critically low
            return playerHealth < 25 || (enemyCount > 8 && playerHealth < 40);
        }

        public TargetPrioritySort GetTargetPriority()
        {
            return TargetPrioritySort.MostEnemies; // Always prioritize groups
        }

        public int GetMaxCombatRange()
        {
            // Use base range for aggressive builds to avoid targeting unreachable monsters
            return Math.Min(Core.Settings.CombatDistance.Value, 45);
        }
    }
}
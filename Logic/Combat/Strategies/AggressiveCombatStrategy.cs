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
        private const float TARGET_STABILITY_DURATION = 1.5f; // Longer duration for target stability
        private DateTime _lastTargetTimeout = DateTime.MinValue;
        private const float TARGET_TIMEOUT = 5.0f; // Reset if stuck on unreachable target

        public Task<Vector2?> SelectTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters)
        {
            var maxRange = GetMaxCombatRange();
            var playerPos = Core.GameController.Player.GridPosNum;

            // Debug: Check why monsters are being filtered out
            var hostileMonsters = monsters.Where(m => m.IsHostile).ToList();
            var targetableMonsters = hostileMonsters.Where(m => m.IsTargetable).ToList();
            var aliveMonsters = targetableMonsters.Where(m => m.IsAlive).ToList();
            var validMonsters = aliveMonsters
                .Where(m => m.GridPosNum.Distance(playerPos) <= maxRange)
                .ToList();

            if (!validMonsters.Any())
            {
                // Provide detailed debug info about why no monsters are valid
                var closestDistance = aliveMonsters.Any() ?
                    aliveMonsters.Min(m => m.GridPosNum.Distance(playerPos)) : -1;

                LastTargetReason = $"No valid monsters in range. Max: {maxRange}, Total: {monsters.Count}, Hostile: {hostileMonsters.Count}, Targetable: {targetableMonsters.Count}, Alive: {aliveMonsters.Count}, Closest: {closestDistance:F1}";
                _lastTargetPosition = null;
                _lastTargetTime = DateTime.MinValue; // Reset timing when no monsters
                return Task.FromResult<Vector2?>(null);
            }

            // If we have a recent target position and there are still enemies near it, stay focused
            if (_lastTargetPosition.HasValue &&
                DateTime.Now < _lastTargetTime.AddSeconds(TARGET_STABILITY_DURATION))
            {
                // Check for targets near our last position
                var nearCurrentTarget = validMonsters
                    .Where(m => m.GridPosNum.Distance(_lastTargetPosition.Value) < 30) // Increased area for stability
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

                        // Update position to track moving enemies (especially for unique)
                        if (stableTarget.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique)
                        {
                            _lastTargetPosition = stableTarget.GridPosNum;
                        }

                        return Task.FromResult<Vector2?>(stableTarget.GridPosNum);
                    }
                }
                else
                {
                    // No monsters near last target, but check if we should stick with a unique enemy
                    var uniqueTarget = validMonsters.FirstOrDefault(m => m.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique);
                    if (uniqueTarget != null)
                    {
                        // Extend stability for unique enemies
                        _lastTargetPosition = uniqueTarget.GridPosNum;
                        _lastTargetTime = DateTime.Now;
                        var distance = uniqueTarget.GridPosNum.Distance(playerPos);
                        LastTargetReason = $"Following unique enemy at {distance:F1} units";
                        return Task.FromResult<Vector2?>(uniqueTarget.GridPosNum);
                    }

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

            // Find new target area - use Focus Fire setting to determine priority
            var bestTarget = targetsToConsider
                .Select(m => new
                {
                    Monster = m,
                    NearbyCount = targetsToConsider.Count(other => other.GridPosNum.Distance(m.GridPosNum) < 20),
                    RarityWeight = Navigation.Map.GetMonsterRarityWeight(m.Rarity),
                    Distance = m.GridPosNum.Distance(playerPos)
                })
                .OrderByDescending(x => Core.Settings.Combat.FocusFire.Value ? x.RarityWeight : x.NearbyCount) // Focus Fire: rarity first, else groups first
                .ThenByDescending(x => Core.Settings.Combat.FocusFire.Value ? x.NearbyCount : x.RarityWeight) // Secondary priority
                .ThenBy(x => x.Distance) // Always prefer closer targets as tie-breaker
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
            var focusMode = Core.Settings.Combat.FocusFire.Value ? "Focus Fire" : "Group Clear";
            LastTargetReason = $"{focusMode}: {bestTarget.Monster.Rarity} ({bestTarget.NearbyCount} nearby) at {targetDistance:F1} units";

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

            // Adjust strategy based on Focus Fire and enemy count
            var preferredRole = enemyCount > 1 ? SkillRoleSort.AreaDamage : SkillRoleSort.PrimaryDamage;
            var targetPriority = Core.Settings.Combat.FocusFire.Value ?
                TargetPrioritySort.HighestThreat :  // Focus Fire: target highest threat (rare/unique) 
                TargetPrioritySort.MostEnemies;     // Group Clear: target center of groups

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
            return Core.Settings.Combat.FocusFire.Value ?
                TargetPrioritySort.HighestThreat :  // Focus Fire: prioritize rare/unique monsters
                TargetPrioritySort.MostEnemies;     // Group Clear: prioritize groups
        }

        public int GetMaxCombatRange()
        {
            // Aggressive builds need longer range to find more targets and groups
            // This ensures we can see enemies that are visible on screen
            return Math.Min(Core.Settings.CombatDistance.Value + 15, 60);
        }
    }
}
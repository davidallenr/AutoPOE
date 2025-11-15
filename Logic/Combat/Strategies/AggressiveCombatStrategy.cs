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
        private const float TARGET_STABILITY_DURATION = 1.5f; // Standard duration for target stability
        private const float UNIQUE_TARGET_STABILITY_DURATION = 5.0f; // Longer duration for unique enemies (bosses)
        private DateTime _lastTargetTimeout = DateTime.MinValue;
        private const float TARGET_TIMEOUT = 5.0f; // Reset if stuck on unreachable target
        private string? _lastTargetName; // Track specific target names for bosses

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

            // Special handling for known boss names - Kosis priority over Omniphobia
            var bossTargets = validMonsters
                .Where(m => m.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique && IsSimulacrumBoss(m))
                .OrderByDescending(m => m.RenderName?.Contains("Kosis") == true ? 2 : 1) // Kosis gets priority
                .ToList();

            // If we have boss targets, prioritize them absolutely
            if (bossTargets.Any())
            {
                var boss = bossTargets.First();
                var distance = boss.GridPosNum.Distance(playerPos);

                // Only update position if boss moved significantly (prevents constant repositioning)
                const float BOSS_REPOSITION_THRESHOLD = 25f;
                bool shouldUpdatePosition = !_lastTargetPosition.HasValue ||
                                           _lastTargetPosition.Value.Distance(boss.GridPosNum) > BOSS_REPOSITION_THRESHOLD;

                if (shouldUpdatePosition)
                {
                    _lastTargetPosition = boss.GridPosNum;
                    _lastTargetTime = DateTime.Now;
                    _lastTargetName = boss.RenderName;
                    LastTargetReason = $"BOSS PRIORITY: {boss.RenderName} at {distance:F1} units (repositioning)";
                }
                else
                {
                    // Keep same target position to avoid cast interruption
                    LastTargetReason = $"BOSS PRIORITY: {boss.RenderName} at {distance:F1} units (stable position)";
                }

                return Task.FromResult<Vector2?>(_lastTargetPosition);
            }

            // Check if we were targeting a boss and should continue
            if (_lastTargetName != null && IsSimulacrumBoss(_lastTargetName))
            {
                var continueBoss = validMonsters.FirstOrDefault(m => IsSimulacrumBoss(m));

                if (continueBoss != null)
                {
                    var distance = continueBoss.GridPosNum.Distance(playerPos);

                    // Only update position if boss moved significantly
                    const float BOSS_REPOSITION_THRESHOLD = 25f;
                    bool shouldUpdatePosition = !_lastTargetPosition.HasValue ||
                                               _lastTargetPosition.Value.Distance(continueBoss.GridPosNum) > BOSS_REPOSITION_THRESHOLD;

                    if (shouldUpdatePosition)
                    {
                        _lastTargetPosition = continueBoss.GridPosNum;
                        _lastTargetTime = DateTime.Now;
                        LastTargetReason = $"CONTINUING BOSS: {continueBoss.RenderName} at {distance:F1} units (repositioning)";
                    }
                    else
                    {
                        LastTargetReason = $"CONTINUING BOSS: {continueBoss.RenderName} at {distance:F1} units (stable)";
                    }

                    return Task.FromResult<Vector2?>(_lastTargetPosition);
                }
                else
                {
                    // Boss is dead, clear tracking
                    _lastTargetName = null;
                    _lastTargetPosition = null;
                    _lastTargetTime = DateTime.MinValue;
                }
            }
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
            // Use longer stability duration for unique enemies (bosses)
            var isTargetingBoss = _lastTargetName != null && IsSimulacrumBoss(_lastTargetName);
            var stabilityDuration = isTargetingBoss ? UNIQUE_TARGET_STABILITY_DURATION : TARGET_STABILITY_DURATION; if (_lastTargetPosition.HasValue &&
                DateTime.Now < _lastTargetTime.AddSeconds(stabilityDuration))
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
                            _lastTargetName = stableTarget.RenderName; // Track boss names
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
                        // Extend stability for unique enemies (especially bosses)
                        _lastTargetPosition = uniqueTarget.GridPosNum;
                        _lastTargetTime = DateTime.Now;
                        _lastTargetName = uniqueTarget.RenderName;
                        var distance = uniqueTarget.GridPosNum.Distance(playerPos);
                        LastTargetReason = $"Following unique enemy: {uniqueTarget.RenderName} at {distance:F1} units";
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
            _lastTargetName = bestTarget.Monster.RenderName;

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

            // Special skill selection for bosses and unique enemies
            bool isTargetingBoss = false;
            if (targetEntity?.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique)
            {
                isTargetingBoss = IsSimulacrumBoss(targetEntity);
            }

            // For bosses, ALWAYS prefer SingleTarget skills regardless of enemy count
            SkillRoleSort preferredRole;
            if (isTargetingBoss)
            {
                preferredRole = SkillRoleSort.SingleTarget; // Force single target for bosses
            }
            else if (targetEntity?.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique)
            {
                preferredRole = SkillRoleSort.SingleTarget; // All unique enemies get single target
            }
            else
            {
                // Regular targeting logic - adjust based on enemy count
                preferredRole = enemyCount > 1 ? SkillRoleSort.AreaDamage : SkillRoleSort.PrimaryDamage;
            }

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

        /// <summary>
        /// Checks if an entity or name is a known Simulacrum boss
        /// </summary>
        private bool IsSimulacrumBoss(ExileCore.PoEMemory.MemoryObjects.Entity? entity)
        {
            if (entity?.RenderName == null) return false;
            return IsSimulacrumBoss(entity.RenderName);
        }

        /// <summary>
        /// Checks if a name string indicates a Simulacrum boss
        /// </summary>
        private bool IsSimulacrumBoss(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            return name.Contains("Kosis") ||
                   name.Contains("Omniphobia") ||
                   name.Contains("Delirium Boss");
        }
    }
}
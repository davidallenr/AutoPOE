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

        // Targeting state
        private Vector2? _lastTargetPosition;
        private DateTime _lastTargetTime = DateTime.MinValue;
        private string? _lastTargetName;

        // Constants
        private const float TARGET_STABILITY_DURATION = 1.5f;
        private const float UNIQUE_TARGET_STABILITY_DURATION = 5.0f;
        private const float BOSS_REPOSITION_THRESHOLD = 25f;
        private const float TARGET_TIMEOUT = 5.0f; // Currently unused but kept for future use

        public Task<Vector2?> SelectTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters)
        {
            var maxRange = GetMaxCombatRange();
            var playerPos = Core.GameController.Player.GridPosNum;

            // Filter to valid combat targets
            var validMonsters = FilterValidMonsters(monsters, playerPos, maxRange);

            // Priority 1: Simulacrum bosses (Kosis > Omniphobia)
            var bossTarget = SelectBossTarget(validMonsters, playerPos);
            if (bossTarget.HasValue)
                return Task.FromResult<Vector2?>(bossTarget);

            // Priority 2: Continue tracking previously targeted boss
            var continuedBossTarget = ContinueBossTracking(validMonsters, playerPos);
            if (continuedBossTarget.HasValue)
                return Task.FromResult<Vector2?>(continuedBossTarget);

            // No valid monsters found
            if (!validMonsters.Any())
            {
                ResetTargetTracking();
                LastTargetReason = BuildNoTargetsDebugMessage(monsters, playerPos, maxRange);
                return Task.FromResult<Vector2?>(null);
            }

            // Priority 3: Maintain stable targeting on current area
            var stableTarget = SelectStableTarget(validMonsters, playerPos);
            if (stableTarget.HasValue)
                return Task.FromResult<Vector2?>(stableTarget);

            // Priority 4: Find new optimal target
            return Task.FromResult(SelectNewTarget(validMonsters, playerPos, maxRange));
        }

        /// <summary>
        /// Filters monsters to valid combat targets within range
        /// </summary>
        private List<ExileCore.PoEMemory.MemoryObjects.Entity> FilterValidMonsters(
            List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters,
            Vector2 playerPos,
            int maxRange)
        {
            return monsters
                .Where(m => m.IsHostile && m.IsTargetable && m.IsAlive)
                .Where(m => m.GridPosNum.Distance(playerPos) <= maxRange)
                .ToList();
        }

        /// <summary>
        /// Attempts to select a Simulacrum boss target (Kosis priority)
        /// </summary>
        private Vector2? SelectBossTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> validMonsters, Vector2 playerPos)
        {
            var bossTargets = validMonsters
                .Where(m => m.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique && IsSimulacrumBoss(m))
                .OrderByDescending(m => m.RenderName?.Contains("Kosis") == true ? 2 : 1)
                .ToList();

            if (!bossTargets.Any())
                return null;

            var boss = bossTargets.First();
            var distance = boss.GridPosNum.Distance(playerPos);

            // Only reposition if boss moved significantly (prevents cast interruption)
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
                LastTargetReason = $"BOSS PRIORITY: {boss.RenderName} at {distance:F1} units (stable position)";
            }

            return _lastTargetPosition;
        }

        /// <summary>
        /// Continues tracking a previously targeted boss if still alive
        /// </summary>
        private Vector2? ContinueBossTracking(List<ExileCore.PoEMemory.MemoryObjects.Entity> validMonsters, Vector2 playerPos)
        {
            if (_lastTargetName == null || !IsSimulacrumBoss(_lastTargetName))
                return null;

            var continueBoss = validMonsters.FirstOrDefault(m => IsSimulacrumBoss(m));

            if (continueBoss != null)
            {
                var distance = continueBoss.GridPosNum.Distance(playerPos);

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

                return _lastTargetPosition;
            }
            else
            {
                // Boss is dead, clear tracking
                ResetTargetTracking();
                return null;
            }
        }

        /// <summary>
        /// Maintains stable targeting on current area to avoid constant repositioning
        /// </summary>
        private Vector2? SelectStableTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> validMonsters, Vector2 playerPos)
        {
            var isTargetingBoss = _lastTargetName != null && IsSimulacrumBoss(_lastTargetName);
            var stabilityDuration = isTargetingBoss ? UNIQUE_TARGET_STABILITY_DURATION : TARGET_STABILITY_DURATION;

            if (!_lastTargetPosition.HasValue || DateTime.Now >= _lastTargetTime.AddSeconds(stabilityDuration))
                return null;

            // Check for targets near our last position
            var nearCurrentTarget = validMonsters
                .Where(m => m.GridPosNum.Distance(_lastTargetPosition.Value) < 30)
                .ToList();

            if (nearCurrentTarget.Any())
            {
                var stableTarget = nearCurrentTarget
                    .OrderByDescending(m => Navigation.Map.GetMonsterRarityWeight(m.Rarity))
                    .ThenBy(m => m.GridPosNum.Distance(_lastTargetPosition.Value))
                    .FirstOrDefault();

                if (stableTarget != null)
                {
                    var distance = stableTarget.GridPosNum.Distance(playerPos);
                    LastTargetReason = $"Focused on current area ({stableTarget.Rarity}) at {distance:F1} units";

                    // Track unique enemies
                    if (stableTarget.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique)
                    {
                        _lastTargetPosition = stableTarget.GridPosNum;
                        _lastTargetName = stableTarget.RenderName;
                    }

                    return stableTarget.GridPosNum;
                }
            }

            // Try to stick with any unique enemy
            var uniqueTarget = validMonsters.FirstOrDefault(m => m.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique);
            if (uniqueTarget != null)
            {
                _lastTargetPosition = uniqueTarget.GridPosNum;
                _lastTargetTime = DateTime.Now;
                _lastTargetName = uniqueTarget.RenderName;
                var distance = uniqueTarget.GridPosNum.Distance(playerPos);
                LastTargetReason = $"Following unique enemy: {uniqueTarget.RenderName} at {distance:F1} units";
                return uniqueTarget.GridPosNum;
            }

            // No valid stable targets, reset timing
            _lastTargetTime = DateTime.MinValue;
            return null;
        }

        /// <summary>
        /// Selects a new optimal target based on Focus Fire setting and proximity
        /// </summary>
        private Vector2? SelectNewTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> validMonsters, Vector2 playerPos, int maxRange)
        {
            // Prefer closer targets to avoid map edge issues
            var closeTargets = validMonsters.Where(m => m.GridPosNum.Distance(playerPos) < maxRange * 0.7f).ToList();
            var targetsToConsider = closeTargets.Any() ? closeTargets : validMonsters;

            // Timeout handling: switch to closer targets if stuck
            if (_lastTargetPosition.HasValue &&
                DateTime.Now > _lastTargetTime.AddSeconds(TARGET_STABILITY_DURATION * 2) &&
                closeTargets.Any())
            {
                targetsToConsider = closeTargets;
                LastTargetReason = "Switching to closer targets due to timeout";
                _lastTargetTime = DateTime.MinValue;
            }

            // Find best target using Focus Fire setting
            var bestTarget = targetsToConsider
                .Select(m => new
                {
                    Monster = m,
                    NearbyCount = targetsToConsider.Count(other => other.GridPosNum.Distance(m.GridPosNum) < 20),
                    RarityWeight = Navigation.Map.GetMonsterRarityWeight(m.Rarity),
                    Distance = m.GridPosNum.Distance(playerPos)
                })
                .OrderByDescending(x => Core.Settings.Combat.FocusFire.Value ? x.RarityWeight : x.NearbyCount)
                .ThenByDescending(x => Core.Settings.Combat.FocusFire.Value ? x.NearbyCount : x.RarityWeight)
                .ThenBy(x => x.Distance)
                .FirstOrDefault();

            if (bestTarget == null)
            {
                LastTargetReason = "No valid target found";
                _lastTargetPosition = null;
                return null;
            }

            // Update tracking
            _lastTargetPosition = bestTarget.Monster.GridPosNum;
            _lastTargetTime = DateTime.Now;
            _lastTargetName = bestTarget.Monster.RenderName;

            var focusMode = Core.Settings.Combat.FocusFire.Value ? "Focus Fire" : "Group Clear";
            LastTargetReason = $"{focusMode}: {bestTarget.Monster.Rarity} ({bestTarget.NearbyCount} nearby) at {bestTarget.Distance:F1} units";

            return _lastTargetPosition;
        }

        /// <summary>
        /// Resets all target tracking state
        /// </summary>
        private void ResetTargetTracking()
        {
            _lastTargetName = null;
            _lastTargetPosition = null;
            _lastTargetTime = DateTime.MinValue;
        }

        /// <summary>
        /// Builds detailed debug message when no targets are found
        /// </summary>
        private string BuildNoTargetsDebugMessage(List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters, Vector2 playerPos, int maxRange)
        {
            var hostileMonsters = monsters.Where(m => m.IsHostile).ToList();
            var targetableMonsters = hostileMonsters.Where(m => m.IsTargetable).ToList();
            var aliveMonsters = targetableMonsters.Where(m => m.IsAlive).ToList();
            var closestDistance = aliveMonsters.Any() ? aliveMonsters.Min(m => m.GridPosNum.Distance(playerPos)) : -1;

            return $"No valid monsters in range. Max: {maxRange}, Total: {monsters.Count}, " +
                   $"Hostile: {hostileMonsters.Count}, Targetable: {targetableMonsters.Count}, " +
                   $"Alive: {aliveMonsters.Count}, Closest: {closestDistance:F1}";
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
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
        private bool _isBossLocked; // Indicates we're hard-locked onto a boss

        // Constants
        private const float TARGET_STABILITY_DURATION = 1.5f;
        private const float UNIQUE_TARGET_STABILITY_DURATION = 5.0f;
        private const float BOSS_REPOSITION_THRESHOLD = 15f; // Reduced to stick closer to boss
        private const float BOSS_LOCK_TIMEOUT = 30.0f; // Maximum time to maintain boss lock without seeing boss
        private const float BOSS_LOCK_MAX_DISTANCE = 150f; // Maximum distance before breaking boss lock

        public Task<Vector2?> SelectTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters)
        {
            var maxRange = GetMaxCombatRange();
            var playerPos = Core.GameController.Player.GridPosNum;

            // Check if we should break boss lock due to timeout or distance
            if (_isBossLocked && _lastTargetPosition.HasValue)
            {
                var timeSinceLastSeen = (DateTime.Now - _lastTargetTime).TotalSeconds;
                var distanceFromBoss = playerPos.Distance(_lastTargetPosition.Value);

                if (timeSinceLastSeen > BOSS_LOCK_TIMEOUT || distanceFromBoss > BOSS_LOCK_MAX_DISTANCE)
                {
                    // Break boss lock
                    ResetTargetTracking();
                    LastTargetReason = $"Boss lock broken: timeout={timeSinceLastSeen:F1}s, distance={distanceFromBoss:F1}";
                }
            }

            // Filter to valid combat targets
            var validMonsters = FilterValidMonsters(monsters, playerPos, maxRange);

            // Priority 1: Check for higher-priority boss (e.g., Kosis when locked on Omniphobia)
            if (_isBossLocked && _lastTargetName != null)
            {
                var currentBossPriority = GameConstants.SimulacrumBosses.GetBossPriority(_lastTargetName);
                var higherPriorityBoss = validMonsters
                    .Where(m => m.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique && IsSimulacrumBoss(m))
                    .Where(m => GameConstants.SimulacrumBosses.GetBossPriority(m.RenderName) > currentBossPriority)
                    .OrderByDescending(m => GameConstants.SimulacrumBosses.GetBossPriority(m.RenderName))
                    .FirstOrDefault();

                if (higherPriorityBoss != null)
                {
                    // Switch to higher priority boss
                    var distance = higherPriorityBoss.GridPosNum.Distance(playerPos);
                    _isBossLocked = true;
                    _lastTargetPosition = higherPriorityBoss.GridPosNum;
                    _lastTargetTime = DateTime.Now;
                    _lastTargetName = higherPriorityBoss.RenderName;
                    LastTargetReason = $"⚡ BOSS SWITCH: {higherPriorityBoss.RenderName} at {distance:F1} units - HIGHER PRIORITY";
                    return Task.FromResult<Vector2?>(_lastTargetPosition);
                }
            }

            // Priority 2: Maintain current boss lock
            if (_isBossLocked)
            {
                var lockedBossTarget = MaintainBossLock(monsters, validMonsters, playerPos);
                if (lockedBossTarget.HasValue)
                    return Task.FromResult<Vector2?>(lockedBossTarget);
            }

            // Priority 3: Find new Simulacrum boss to lock onto
            var bossTarget = SelectBossTarget(validMonsters, playerPos);
            if (bossTarget.HasValue)
                return Task.FromResult<Vector2?>(bossTarget);

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
        /// Maintains boss lock, searching in expanded range if necessary
        /// </summary>
        private Vector2? MaintainBossLock(
            List<ExileCore.PoEMemory.MemoryObjects.Entity> allMonsters,
            List<ExileCore.PoEMemory.MemoryObjects.Entity> validMonsters,
            Vector2 playerPos)
        {
            // First check in valid range
            var boss = validMonsters.FirstOrDefault(m =>
                m.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique &&
                IsSimulacrumBoss(m));

            // If not in valid range, check in extended range (boss might be just outside combat range)
            if (boss == null)
            {
                var extendedRange = GetMaxCombatRange() * 1.5f;
                boss = allMonsters
                    .Where(m => m.IsAlive && m.IsHostile && m.IsTargetable)
                    .Where(m => m.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique)
                    .Where(m => IsSimulacrumBoss(m))
                    .Where(m => m.GridPosNum.Distance(playerPos) < extendedRange)
                    .FirstOrDefault();
            }

            if (boss != null)
            {
                var distance = boss.GridPosNum.Distance(playerPos);

                // Always update to boss's actual position (follow him closely)
                _lastTargetPosition = boss.GridPosNum;
                _lastTargetTime = DateTime.Now;
                _lastTargetName = boss.RenderName;

                LastTargetReason = $"🔒 BOSS LOCKED: {boss.RenderName} at {distance:F1} units";
                return boss.GridPosNum;
            }
            else
            {
                // Boss not found anywhere, maintain last known position for a while
                var timeSinceLastSeen = (DateTime.Now - _lastTargetTime).TotalSeconds;

                if (timeSinceLastSeen < 3.0f) // Give 3 seconds grace period
                {
                    LastTargetReason = $"🔒 BOSS LOCKED (last seen {timeSinceLastSeen:F1}s ago): {_lastTargetName}";
                    return _lastTargetPosition;
                }
                else
                {
                    // Boss likely dead, release lock
                    ResetTargetTracking();
                    LastTargetReason = "Boss lock released - boss not found";
                    return null;
                }
            }
        }

        /// <summary>
        /// Attempts to select a Simulacrum boss target (Kosis priority)
        /// </summary>
        private Vector2? SelectBossTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> validMonsters, Vector2 playerPos)
        {
            var bossTargets = validMonsters
                .Where(m => m.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique && IsSimulacrumBoss(m))
                .OrderByDescending(m => GameConstants.SimulacrumBosses.GetBossPriority(m.RenderName))
                .ToList();

            if (!bossTargets.Any())
                return null;

            var boss = bossTargets.First();
            var distance = boss.GridPosNum.Distance(playerPos);

            // Engage boss lock!
            _isBossLocked = true;
            _lastTargetPosition = boss.GridPosNum;
            _lastTargetTime = DateTime.Now;
            _lastTargetName = boss.RenderName;
            LastTargetReason = $"🎯 BOSS DETECTED: {boss.RenderName} at {distance:F1} units - LOCKING ON";

            return _lastTargetPosition;
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
            _isBossLocked = false;
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
            bool isTargetingBoss = _isBossLocked; // Check boss lock flag first

            if (!isTargetingBoss && targetEntity?.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique)
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

        public bool IsLockedOnPriorityTarget()
        {
            return _isBossLocked;
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
            return GameConstants.SimulacrumBosses.IsBoss(name);
        }
    }
}
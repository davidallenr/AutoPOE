using AutoPOE.Logic.Combat;
using AutoPOE.Logic.Combat.Strategies;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System.Numerics;
using static AutoPOE.Settings.Skill;
using static System.ComponentModel.Design.ObjectSelectorEditor;

namespace AutoPOE.Logic.Actions
{
    public class CombatAction : IAction
    {
        private Random _random = new Random();
        private Navigation.Path? _currentPath;
        private (Vector2 Position, float Weight) _bestFightPos;
        private const float RepositionThreshold = 1.25f;
        private ICombatStrategy _combatStrategy;
        private Vector2? _lastTarget;
        private string _loadedStrategyName = "";

        public CombatAction()
        {
            // Initialize with default strategy
            LoadStrategy();
        }

        private void LoadStrategy()
        {
            // Load strategy based on settings
            var strategyName = Core.Settings.Combat.Strategy.Value;

            // Only reload if strategy changed
            if (_loadedStrategyName == strategyName && _combatStrategy != null)
                return;

            _combatStrategy = strategyName switch
            {
                "Aggressive" => new AggressiveCombatStrategy(),
                _ => new StandardCombatStrategy() // Default to Standard for any other value
            };

            _loadedStrategyName = strategyName;
            Core.Plugin.LogMessage($"Loaded combat strategy: {_combatStrategy.Name}");
        }

        /// <summary>
        /// Executes the current path if it exists and is not finished, otherwise clears it.
        /// </summary>
        private async Task ExecuteOrClearPath()
        {
            if (_currentPath != null && !_currentPath.IsFinished)
            {
                await _currentPath.FollowPath();
            }
            else
            {
                _currentPath = null;
            }
        }

        /// <summary>
        /// Attempts to create a path to the target position if no active path exists.
        /// </summary>
        /// <param name="playerPos">Current player position</param>
        /// <param name="targetPos">Target position to path to</param>
        /// <param name="maxDistance">Optional maximum distance check before creating path</param>
        private void TryCreatePathToTarget(Vector2 playerPos, Vector2 targetPos, float? maxDistance = null)
        {
            if (_currentPath == null || _currentPath.IsFinished)
            {
                var newPath = Core.Map.FindPath(playerPos, targetPos);
                if (newPath != null)
                {
                    // Only set path if within max distance (if specified)
                    if (!maxDistance.HasValue || targetPos.Distance(playerPos) < maxDistance.Value)
                    {
                        _currentPath = newPath;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts tactical repositioning based on fight weight analysis.
        /// </summary>
        /// <param name="playerPos">Current player position</param>
        private void TryTacticalRepositioning(Vector2 playerPos)
        {
            var currentWeight = Core.Map.GetPositionFightWeight(playerPos);
            _bestFightPos = Core.Map.FindBestFightingPosition();

            if (_currentPath == null && _bestFightPos.Weight > currentWeight * RepositionThreshold)
            {
                var newPath = Core.Map.FindPath(playerPos, _bestFightPos.Position);
                if (newPath != null && _bestFightPos.Position.Distance(playerPos) < 200)
                {
                    _currentPath = newPath;
                }
            }
        }

        /// <summary>
        /// Gets valid hostile monsters within specified range of player.
        /// </summary>
        /// <param name="playerPos">Player position</param>
        /// <param name="maxRange">Maximum search range</param>
        private List<ExileCore.PoEMemory.MemoryObjects.Entity> GetValidMonstersInRange(Vector2 playerPos, float maxRange)
        {
            return Core.GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                .Where(m => m.IsAlive
                    && m.IsTargetable
                    && m.IsHostile
                    && m.GridPosNum.Distance(playerPos) < maxRange)
                .ToList();
        }

        public async Task<ActionResultType> Tick()
        {
            await DetonateMines();
            await CastTargetSelfSpells();
            await CastTargetMonsterSpells();

            var playerPos = Core.GameController.Player.GridPosNum;

            // Check if we're locked on a priority target (e.g., boss)
            LoadStrategy();
            bool isLockedOnPriorityTarget = _combatStrategy.IsLockedOnPriorityTarget();

            // When locked on priority target, only reposition toward the target
            if (isLockedOnPriorityTarget && _lastTarget.HasValue)
            {
                var distanceToTarget = playerPos.Distance(_lastTarget.Value);
                const float BOSS_ENGAGE_DISTANCE = 30f; // Distance to maintain from boss

                // If boss is too far, move closer
                if (distanceToTarget > BOSS_ENGAGE_DISTANCE)
                {
                    TryCreatePathToTarget(playerPos, _lastTarget.Value);
                    await ExecuteOrClearPath();
                }
                else
                {
                    // Close enough to boss, stop moving
                    _currentPath = null;
                }
            }
            // Normal repositioning when not locked on priority target
            else if (!isLockedOnPriorityTarget)
            {
                // If we have a last target that's far away, prioritize moving toward it
                // This helps close distance to scattered remaining monsters
                if (_lastTarget.HasValue && _lastTarget.Value != Vector2.Zero)
                {
                    var distanceToTarget = playerPos.Distance(_lastTarget.Value);
                    var combatRange = _combatStrategy.GetMaxCombatRange();

                    // If target is beyond combat range, move toward it
                    if (distanceToTarget > combatRange)
                    {
                        TryCreatePathToTarget(playerPos, _lastTarget.Value, maxDistance: 300);
                        await ExecuteOrClearPath();
                    }
                    // Close enough - use normal repositioning for tactical advantage
                    else
                    {
                        TryTacticalRepositioning(playerPos);
                        await ExecuteOrClearPath();
                    }
                }
                // No target - use normal repositioning
                else
                {
                    TryTacticalRepositioning(playerPos);
                    await ExecuteOrClearPath();
                }
            }
            // When locked but no target position yet, clear paths
            else
            {
                _currentPath = null;
            }

            if (DateTime.Now > SimulacrumState.LastMovedAt.AddSeconds(2))
            {
                var nextPos = Core.GameController.Player.GridPosNum + new Vector2(_random.Next(-50, 50), _random.Next(-50, 50));
                await Controls.UseKeyAtGridPos(nextPos, Core.Settings.GetNextMovementSkill());
                return ActionResultType.Exception;
            }

            return ActionResultType.Running;
        }

        private async Task<bool> CastTargetSelfSpells()
        {
            var skill = Core.Settings.GetNextCombatSkill(Settings.Skill.CastTypeSort.TargetSelf);
            if (skill == null) return false;
            await Controls.UseKeyAtGridPos(Core.GameController.Player.GridPosNum, skill.Hotkey.Value);
            return true;
        }

        private async Task<bool> DetonateMines()
        {
            if (!Core.GameController.Player.Buffs.Any(buff => buff.Name == GameConstants.BuffNames.MineManaReservation))
                return false;
            if (!Core.Settings.ShouldDetonateMines) return false;
            await Controls.UseKeyAtGridPos(Core.GameController.Player.GridPosNum, Core.Settings.DetonateMinesKey.Value);
            await Task.Delay(Core.Settings.ActionFrequency);
            return true;
        }

        private async Task<bool> CastTargetMonsterSpells()
        {
            // Ensure strategy is loaded
            LoadStrategy();

            var playerPos = Core.GameController.Player.GridPosNum;
            var maxRange = _combatStrategy.GetMaxCombatRange();

            // Get monsters within strategy's actual range
            var monsters = GetValidMonstersInRange(playerPos, maxRange);

            // If no monsters found in normal range but MonstersRemaining > 0, expand search
            // This helps find scattered remaining enemies
            if (monsters.Count == 0)
            {
                var monstersRemaining = Core.GameController.IngameState.Data.ServerData.MonstersRemaining;
                if (monstersRemaining > 0)
                {
                    var expandedRange = maxRange * 2.5f; // Search up to 2.5x normal range
                    monsters = GetValidMonstersInRange(playerPos, expandedRange);
                }
            }

            // Use strategy to select target
            var targetPos = await _combatStrategy.SelectTarget(monsters);
            _lastTarget = targetPos;

            if (targetPos == null)
                return false;

            // Find the actual target entity
            var bestTarget = monsters
                .FirstOrDefault(m => m.GridPosNum == targetPos.Value);

            // Get player health for strategy decisions
            var playerComponent = Core.GameController.Player.GetComponent<Life>();
            var playerHealth = playerComponent != null ? (float)playerComponent.HPPercentage : 100f;

            // Count nearby enemies
            var nearbyEnemies = monsters.Count(m => m.GridPosNum.Distance(Core.GameController.Player.GridPosNum) < 30);

            // Get recommended skill from strategy
            var recommendedSkill = await _combatStrategy.GetRecommendedSkill(bestTarget, playerHealth, nearbyEnemies);

            if (recommendedSkill == null)
            {
                // Fallback to old system if strategy doesn't recommend anything
                var availableSkills = Core.Settings.GetAvailableSkillsByPriority()
                    .Where(s => s.CastType.Value != CastTypeSort.DoNotUse.ToString() &&
                               s.CastType.Value != CastTypeSort.TargetSelf.ToString())
                    .ToList();

                recommendedSkill = availableSkills.FirstOrDefault();
            }

            if (recommendedSkill == null)
                return false;

            // Use the skill
            recommendedSkill.NextCast = DateTime.Now.AddMilliseconds(recommendedSkill.MinimumDelay.Value);
            await Controls.UseKeyAtGridPos(targetPos.Value, recommendedSkill.Hotkey.Value);

            return true;
        }


        public void Render()
        {
            _currentPath?.Render();
            if (_bestFightPos.Position != Vector2.Zero)
                Core.Graphics.DrawCircle(Controls.GetScreenClampedGridPos(_bestFightPos.Position), 15, SharpDX.Color.Yellow, 3);

            // Draw target indicator
            if (_lastTarget.HasValue)
            {
                Core.Graphics.DrawCircle(Controls.GetScreenClampedGridPos(_lastTarget.Value), 10, SharpDX.Color.Red, 3);
            }
        }

        /// <summary>
        /// Gets the current combat strategy for debugging
        /// </summary>
        public ICombatStrategy CurrentStrategy => _combatStrategy;

        /// <summary>
        /// Gets the last targeted position for debugging
        /// </summary>
        public Vector2? LastTarget => _lastTarget;
    }
}

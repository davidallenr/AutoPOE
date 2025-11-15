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
                    if (_currentPath == null || _currentPath.IsFinished)
                    {
                        var newPath = Core.Map.FindPath(playerPos, _lastTarget.Value);
                        if (newPath != null)
                        {
                            _currentPath = newPath;
                        }
                    }

                    if (_currentPath != null && !_currentPath.IsFinished)
                    {
                        await _currentPath.FollowPath();
                    }
                    else
                    {
                        _currentPath = null;
                    }
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
                var currentWeight = Core.Map.GetPositionFightWeight(playerPos);
                _bestFightPos = Core.Map.FindBestFightingPosition();

                // Always try to generate a new path if none exists
                if (_currentPath == null && _bestFightPos.Weight > currentWeight * RepositionThreshold)
                {
                    var newPath = Core.Map.FindPath(playerPos, _bestFightPos.Position);
                    // Only set the path if it was successfully created and the target isn't too far
                    if (newPath != null && _bestFightPos.Position.Distance(playerPos) < 200)
                    {
                        _currentPath = newPath;
                    }
                }

                if (_currentPath != null && !_currentPath.IsFinished)
                {
                    // Validate path isn't leading to an unreachable area
                    var nextTarget = _currentPath.Next;
                    if (nextTarget.HasValue && nextTarget.Value.Distance(playerPos) > 300)
                    {
                        // Path target is too far, abandon it
                        _currentPath = null;
                    }
                    else
                    {
                        await _currentPath.FollowPath();
                    }
                }
                else
                {
                    _currentPath = null;
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

            // Get monsters within strategy's actual range (consistent with sequence logic)
            var monsters = Core.GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                .Where(m => m.IsAlive
                    && m.IsTargetable
                    && m.IsHostile
                    && m.GridPosNum.Distance(playerPos) < maxRange)
                .ToList();

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

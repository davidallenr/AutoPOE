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
    public class CombatAction:IAction
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
                "Cast on Crit" => new StandardCombatStrategy(), // TODO: Implement CastOnCritStrategy
                _ => new StandardCombatStrategy()
            };
            
            _loadedStrategyName = strategyName;
            Core.Plugin.LogMessage($"Loaded combat strategy: {_combatStrategy.Name}");
        }

        public async Task<ActionResultType> Tick()
        {
            await DetonateMines();
            await CastTargetSelfSpells();
            await CastTargetMercenarySpells();
            await CastTargetMonsterSpells();

            var playerPos = Core.GameController.Player.GridPosNum;
            var currentWeight = Core.Map.GetPositionFightWeight(playerPos);
            _bestFightPos = Core.Map.FindBestFightingPosition();

            // Always try to generate a new path if none exists
            if (_currentPath == null && _bestFightPos.Weight > currentWeight * RepositionThreshold)            
                _currentPath = Core.Map.FindPath(playerPos, _bestFightPos.Position);            

            if (_currentPath != null && !_currentPath.IsFinished)            
                await _currentPath.FollowPath();            
            else
                _currentPath = null;

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
            if (!Core.GameController.Player.Buffs.Any(buff => buff.Name == "mine_mana_reservation"))
                return false;
            if (!Core.Settings.ShouldDetonateMines) return false;
            await Controls.UseKeyAtGridPos(Core.GameController.Player.GridPosNum, Core.Settings.DetonateMinesKey.Value);
            await Task.Delay(Core.Settings.ActionFrequency);
            return true;
        }

        private async Task<bool> CastTargetMercenarySpells()
        {
            var merc = Core.GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                .FirstOrDefault(m => m.IsAlive && m.IsTargetable && !m.IsHostile && m.GridPosNum.Distance(Core.GameController.Player.GridPosNum) <= Core.Settings.ViewDistance);
            if (merc == null) return false;

            var skill = Core.Settings.GetNextCombatSkill(Settings.Skill.CastTypeSort.TargetMercenary);
            if (skill == null) return false;

            await Controls.UseKeyAtGridPos(merc.GridPosNum, skill.Hotkey.Value);
            return true;
        }

        private async Task<bool> CastTargetMonsterSpells()
        {
            // Get all valid monsters
            var monsters = Core.GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                .Where(m => m.IsAlive && m.IsTargetable)
                .ToList();

            // Use strategy to select target
            var targetPos = await _combatStrategy.SelectTarget(monsters);
            _lastTarget = targetPos;

            if (targetPos == null)
                return false;

            // Find the actual target entity for rarity-based skill selection
            var bestTarget = monsters
                .FirstOrDefault(m => m.GridPosNum == targetPos.Value);

            if (bestTarget == null)
                return false;

            var skills = Core.Settings.GetAvailableMonsterTargetingSkills()
                .OrderBy(I=> _random.Next())
                .ToList();

            switch (bestTarget.Rarity)
            {
                case MonsterRarity.White:
                case MonsterRarity.Magic:
                    skills = skills
                        .Where(I => I.CastType == CastTypeSort.TargetMonster.ToString())
                        .ToList();
                    break;
                case MonsterRarity.Rare:
                    skills = skills
                        .Where(I => I.CastType == CastTypeSort.TargetRareMonster.ToString() || I.CastType == CastTypeSort.TargetMonster.ToString())
                        .ToList();
                    break;
            }

            var skill = skills.FirstOrDefault();
            if (skill == null) return false;
            skill.NextCast = DateTime.Now.AddMilliseconds(skill.MinimumDelay.Value);
            await Controls.UseKeyAtGridPos(targetPos.Value, skill.Hotkey.Value);
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

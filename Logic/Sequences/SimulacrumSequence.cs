using AutoPOE.Logic.Actions;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System.Numerics;

namespace AutoPOE.Logic.Sequences
{
    public class SimulacrumSequence : ISequence
    {
        private Task<ActionResultType> _currentTask;
        private IAction _currentAction;
        private DateTime _lastCombatTime = DateTime.MinValue;
        private const float COMBAT_TIMEOUT = 3.0f; // Seconds without valid targets before switching to explore

        // Cache actions to avoid recreating them
        private readonly CombatAction _combatAction = new CombatAction();
        private readonly ExploreAction _exploreAction = new ExploreAction();
        private readonly IdleAction _idleAction = new IdleAction();

        /// <summary>
        /// Gets the current action for debugging
        /// </summary>
        public IAction CurrentAction => _currentAction;

        public void Tick()
        {
            SimulacrumState.Tick();
            var nextAction = GetNextAction();
            if (_currentAction == null || _currentAction.GetType() != nextAction.GetType())
            {
                _currentAction = nextAction;
                _currentTask = _currentAction.Tick();
            }
            if (_currentTask == null || _currentTask.IsCompleted)
            {
                if (_currentTask?.Result == ActionResultType.Success || _currentTask?.Result == ActionResultType.Failure)
                    _currentAction = GetNextAction();
                _currentTask = _currentAction.Tick();
            }
        }


        public void Render()
        {
            _currentAction?.Render();
        }
        private IAction GetNextAction()
        {
            if (!Core.GameController.Player.IsAlive && Core.GameController.IngameState.IngameUi.ResurrectPanel.IsVisible)
                return new ReviveAction();

            if (Core.GameController.Area.CurrentArea.IsHideout)
                return new CreateMapAction();

            if (!SimulacrumState.MonolithPosition.HasValue)
                return new ExploreAction();

            if (Core.Map.ClosestValidGroundItem != null)
                return new LootAction();

            if (!SimulacrumState.IsWaveActive && SimulacrumState.StashPosition.HasValue &&
                (CanUseIncubators() || GetStorableInventoryCount >= Core.Settings.StoreItemThreshold && Core.Map.ClosestValidGroundItem == null))
                return new StoreItemsAction();

            if (SimulacrumState.IsWaveActive && Core.Map.ClosestValidGroundItem == null)
            {
                bool hasValidTargets = HasValidCombatTargets();

                if (hasValidTargets)
                {
                    _lastCombatTime = DateTime.Now;
                    return _combatAction;
                }
                else if (DateTime.Now > _lastCombatTime.AddSeconds(COMBAT_TIMEOUT))
                {
                    // No valid targets for a while, switch to explore
                    return _exploreAction;
                }
                else
                {
                    // Recently had targets, keep trying combat
                    return _combatAction;
                }
            }

            else if (DateTime.Now > SimulacrumState.CanStartWaveAt && Core.Map.ClosestValidGroundItem == null)
                return SimulacrumState.CurrentWave < 15 ? new StartWaveAction() : new LeaveMapAction();

            return _idleAction;
        }


        private static int GetStorableInventoryCount => Core.GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory.InventorySlotItems.Count;


        private bool CanUseIncubators()
        {
            if (!Core.Settings.UseIncubators || !Core.HasIncubators) return false;

            var equipment = Core.GameController.IngameState.ServerData.PlayerInventories
                .Where(inv => inv.Inventory.InventSlot >= InventorySlotE.BodyArmour1 && inv.Inventory.InventSlot <= InventorySlotE.Belt1 && inv.Inventory.Items.Count == 1)
                .Select(inv => inv.Inventory)
                .ToList();

            foreach (var equip in equipment)
            {
                var incubatorName = equip.Items.FirstOrDefault()?.GetComponent<Mods>()?.IncubatorName;
                if (string.IsNullOrEmpty(incubatorName))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if there are valid targets that the combat action can actually engage
        /// </summary>
        public bool HasValidCombatTargets()
        {
            var playerPos = Core.GameController.Player.GridPosNum;
            // Use the combat action's strategy max range instead of basic combat distance
            var maxRange = _combatAction.CurrentStrategy?.GetMaxCombatRange() ?? Core.Settings.CombatDistance.Value;

            var validTargets = Core.GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                .Where(m => m.IsAlive
                    && m.IsTargetable
                    && m.IsHostile
                    && m.GridPosNum.Distance(playerPos) < maxRange)
                .ToList();

            return validTargets.Any();
        }
    }
}

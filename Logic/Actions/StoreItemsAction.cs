using AutoPOE.Logic;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoPOE.Logic.Actions
{
    public class StoreItemsAction : IAction
    {
        public StoreItemsAction()
        {
            if (SimulacrumState.StashPosition == null) return;
            _currentPath = Core.Map.FindPath(Core.GameController.Player.GridPosNum, SimulacrumState.StashPosition.Value);
        }

        private Navigation.Path? _currentPath;

        // Remove hardcoded positions - will use Core.EquipmentSlotPositions instead
        // Kept as fallback if Core positions aren't initialized
        private static readonly Dictionary<InventorySlotE, Vector2> DefaultEquipmentSlotPositions = new Dictionary<InventorySlotE, Vector2>
        {
            { InventorySlotE.BodyArmour1, new Vector2(1587, 296) },
            { InventorySlotE.Weapon1, new Vector2(1369, 233) },
            { InventorySlotE.Offhand1, new Vector2(1784, 232) },
            { InventorySlotE.Helm1, new Vector2(1585, 165) },
            { InventorySlotE.Amulet1, new Vector2(1690, 245) },
            { InventorySlotE.Ring1, new Vector2(1480, 300) },
            { InventorySlotE.Ring2, new Vector2(1687, 306) },
            { InventorySlotE.Gloves1, new Vector2(1453, 398) },
            { InventorySlotE.Boots1, new Vector2(1719, 391) },
            { InventorySlotE.Belt1, new Vector2(1584, 421) }
        };

        private static Dictionary<InventorySlotE, Vector2> GetEquipmentSlotPositions()
        {
            // Use calibrated positions from Core if available, otherwise use defaults
            return Core.EquipmentSlotPositions ?? DefaultEquipmentSlotPositions;
        }

        /// <summary>
        /// Validates that we have positions for all required equipment slots
        /// </summary>
        private static bool ValidateEquipmentPositions()
        {
            var positions = GetEquipmentSlotPositions();
            var requiredSlots = new[]
            {
                InventorySlotE.BodyArmour1, InventorySlotE.Weapon1, InventorySlotE.Offhand1,
                InventorySlotE.Helm1, InventorySlotE.Amulet1, InventorySlotE.Ring1,
                InventorySlotE.Ring2, InventorySlotE.Gloves1, InventorySlotE.Boots1, InventorySlotE.Belt1
            };

            foreach (var slot in requiredSlots)
            {
                if (!positions.ContainsKey(slot))
                {
                    Core.Plugin.LogError($"Missing calibrated position for {slot}");
                    return false;
                }
            }
            return true;
        }

        public async Task<ActionResultType> Tick()
        {
            if (SimulacrumState.StoreItemAttemptCount > 100)
            {
                Core.IsBotRunning = false;
                return ActionResultType.Exception;
            }

            if (SimulacrumState.StashPosition == null) return ActionResultType.Exception;

            var playerInventory = Core.GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory.InventorySlotItems;

            if (IsFinished)
            {
                await Controls.ClosePanels();
                return ActionResultType.Success;
            }

            // --- 1. Movement ---
            if (_currentPath != null && !_currentPath.IsFinished)
            {
                await _currentPath.FollowPath();
                return ActionResultType.Running;
            }

            var playerPos = Core.GameController.Player.GridPosNum;
            if (playerPos.Distance(SimulacrumState.StashPosition.Value) > Core.Settings.NodeSize * 2)
            {
                _currentPath = Core.Map.FindPath(Core.GameController.Player.GridPosNum, SimulacrumState.StashPosition.Value);
                return ActionResultType.Running;
            }

            // --- 2. Open Stash ---
            if (!IsStashOpen)
            {
                var stashObj = Core.GameController.EntityListWrapper.OnlyValidEntities.FirstOrDefault(I => I.Metadata.Contains(GameConstants.EntityMetadata.Stash));
                if (stashObj == null) return ActionResultType.Exception;

                await Controls.ClickScreenPos(Controls.GetScreenByGridPos(stashObj.GridPosNum));
                await Task.Delay(300);

                if (!IsStashOpen) return ActionResultType.Running;

                // Wait for stash contents to load after opening
                await Task.Delay(200);
            }

            // --- 3. Store Items ---
            if (Core.GameController.IngameState.IngameUi.Cursor.Action == MouseActionType.HoldItem)
            {
                var invCenter = Core.GameController.IngameState.IngameUi.InventoryPanel.GetClientRect().Center;
                await Controls.ClickScreenPos(new Vector2(invCenter.X, invCenter.Y), isLeft: true, exactPosition: false, holdCtrl: false);
                await Task.Delay(100);
                return ActionResultType.Running;
            }

            // We use .ToList() to create a static copy, otherwise the loop skips items
            foreach (var item in playerInventory.ToList())
            {
                if (!Core.GameController.IngameState.IngameUi.InventoryPanel.IsVisible)
                    return ActionResultType.Failure;

                var center = item.GetClientRect().Center;
                var itemCenterVec = new Vector2(center.X, center.Y);

                ExileCore.Input.KeyDown(Keys.ControlKey);
                await Task.Delay(50);

                await Controls.ClickScreenPos(itemCenterVec, isLeft: true, exactPosition: false, holdCtrl: false);
                await Task.Delay(Core.Settings.ActionFrequency);

                ExileCore.Input.KeyUp(Keys.ControlKey);

                SimulacrumState.StoreItemAttemptCount++;

                if (Core.GameController.IngameState.IngameUi.Cursor.Action == MouseActionType.HoldItem)
                {
                    // Failed Ctrl+Click, drop item back
                    await Controls.ClickScreenPos(itemCenterVec, isLeft: true, exactPosition: false, holdCtrl: false);
                    await Task.Delay(100);
                    return ActionResultType.Running;
                }

                await Task.Delay(50);
            }

            // --- 4. Apply Incubators ---
            if (Core.Settings.UseIncubators)
            {
                // Validate equipment positions before attempting to apply incubators
                if (!ValidateEquipmentPositions())
                {
                    Core.Plugin.LogMessage("Skipping incubator application - equipment positions not calibrated");
                }
                else
                {
                    int incubatorAttempts = 0;
                    const int maxIncubatorAttempts = 20; // Prevent infinite loops

                    while (incubatorAttempts < maxIncubatorAttempts && await ApplyAnyIncubator())
                    {
                        await Task.Delay(400);
                        incubatorAttempts++;
                    }

                    if (incubatorAttempts >= maxIncubatorAttempts)
                    {
                        Core.Plugin.LogMessage($"Reached max incubator attempts ({maxIncubatorAttempts})");
                    }
                }
            }

            return ActionResultType.Running;
        }

        bool IsFinished => Core.GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory.InventorySlotItems.Count == 0
                            && (!Core.Settings.UseIncubators
                                || FindEmptyEquipmentSlot() == null
                                || FindIncubatorInStash() == null);

        bool IsStashOpen => Core.GameController.IngameState.IngameUi.StashElement.IsVisible;

        private async Task<bool> ApplyAnyIncubator()
        {
            try
            {
                var incubatorToApply = FindIncubatorInStash();
                var targetSlot = FindEmptyEquipmentSlot();

                if (incubatorToApply == null || targetSlot == null)
                    return false;

                // Validate we have inventory panel open
                if (!Core.GameController.IngameState.IngameUi.InventoryPanel.IsVisible)
                {
                    Core.Plugin.LogError("Inventory panel not visible when trying to apply incubator");
                    return false;
                }

                // Ensure cursor is free before attempting to pick up incubator
                if (Core.GameController.IngameState.IngameUi.Cursor.Action != MouseActionType.Free)
                {
                    Core.Plugin.LogError($"Cursor not free before picking incubator (state: {Core.GameController.IngameState.IngameUi.Cursor.Action})");
                    await Controls.UseKey(Keys.Escape);
                    await Task.Delay(200);

                    // Check again after escape
                    if (Core.GameController.IngameState.IngameUi.Cursor.Action != MouseActionType.Free)
                    {
                        Core.Plugin.LogError("Failed to free cursor - aborting incubator application");
                        return false;
                    }
                }

                // Small delay to ensure game is ready for interaction
                await Task.Delay(100);

                Core.Plugin.LogMessage($"Attempting to right-click incubator at position ({incubatorToApply.Value.X}, {incubatorToApply.Value.Y})");

                // Right-click incubator in stash
                await Controls.ClickScreenPos(incubatorToApply.Value, false, true);

                // Give more time for the cursor state to change
                await Task.Delay(150);

                Core.Plugin.LogMessage($"After right-click, cursor state: {Core.GameController.IngameState.IngameUi.Cursor.Action}");

                if (!await WaitForCursorState(MouseActionType.UseItem, 2000))
                {
                    Core.Plugin.LogError("Failed to pick up incubator - cursor didn't change to UseItem");
                    return false;
                }

                // Click equipment slot
                var equipmentPositions = GetEquipmentSlotPositions();
                if (!equipmentPositions.ContainsKey(targetSlot.Value))
                {
                    Core.Plugin.LogError($"No calibrated position for slot {targetSlot.Value}");
                    // Cancel held item
                    await Controls.UseKey(Keys.Escape);
                    return false;
                }

                await Controls.ClickScreenPos(equipmentPositions[targetSlot.Value]);

                // Check if we're still holding an item (application failed)
                if (Core.GameController.IngameState.IngameUi.Cursor.Action == MouseActionType.HoldItem
                    || Core.GameController.IngameState.IngameUi.Cursor.Action == MouseActionType.UseItem)
                {
                    Core.Plugin.LogError($"Failed to apply incubator to {targetSlot.Value} - cancelling");
                    await Controls.UseKey(Keys.Escape);
                    await Task.Delay(100);
                    return false;
                }

                if (!await WaitForCursorState(MouseActionType.Free, 2000))
                {
                    Core.Plugin.LogError("Cursor didn't return to Free state after applying incubator");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Core.Plugin.LogError($"ApplyAnyIncubator error: {ex.Message}");
                // Try to cancel any held item
                if (Core.GameController.IngameState.IngameUi.Cursor.Action != MouseActionType.Free)
                {
                    await Controls.UseKey(Keys.Escape);
                }
                return false;
            }
        }

        private async Task<bool> WaitForCursorState(MouseActionType expectedState, int timeoutMs)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (Core.GameController.IngameState.IngameUi.Cursor.Action == expectedState)
                {
                    return true;
                }
                await Task.Delay(50); // Poll more frequently than action frequency for cursor state
            }
            return false;
        }

        private InventorySlotE? FindEmptyEquipmentSlot()
        {
            try
            {
                var equipment = Core.GameController.IngameState.ServerData.PlayerInventories
                    .Where(inv => inv?.Inventory != null
                                && inv.Inventory.InventSlot >= InventorySlotE.BodyArmour1
                                && inv.Inventory.InventSlot <= InventorySlotE.Belt1
                                && inv.Inventory.Items.Count == 1)
                    .Select(inv => inv.Inventory)
                    .ToList();

                // Check if we have calibrated positions for this slot
                var equipmentPositions = GetEquipmentSlotPositions();

                foreach (var equip in equipment)
                {
                    // Skip if we don't have a calibrated position for this slot
                    if (!equipmentPositions.ContainsKey(equip.InventSlot))
                        continue;

                    var incubatorName = equip.Items.FirstOrDefault()?.GetComponent<Mods>()?.IncubatorName;
                    if (string.IsNullOrEmpty(incubatorName))
                        return equip.InventSlot;
                }
            }
            catch (Exception ex)
            {
                Core.Plugin.LogError($"FindEmptyEquipmentSlot error: {ex.Message}");
            }

            return null;
        }

        private Vector2? FindIncubatorInStash()
        {
            try
            {
                if (!IsStashOpen)
                {
                    Core.Plugin.LogError("FindIncubatorInStash: Stash not open");
                    return null;
                }

                var visibleStash = Core.GameController.IngameState.IngameUi.StashElement.VisibleStash;
                if (visibleStash == null)
                {
                    Core.Plugin.LogError("FindIncubatorInStash: VisibleStash is null");
                    return null;
                }

                var visibleItems = visibleStash.VisibleInventoryItems;
                if (visibleItems == null)
                {
                    Core.Plugin.LogError("FindIncubatorInStash: VisibleInventoryItems is null");
                    return null;
                }

                Core.Plugin.LogMessage($"FindIncubatorInStash: Scanning {visibleItems.Count} items in stash");

                // Use the same detection logic as UpdateIncubatorStatus
                var incubator = visibleItems
                    .FirstOrDefault(item => item?.Item != null && (
                        (!string.IsNullOrEmpty(item.Item.Path) && item.Item.Path.Contains(GameConstants.ItemMetadata.Incubator, System.StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(item.Item.Metadata) && item.Item.Metadata.Contains(GameConstants.ItemMetadata.Incubator, System.StringComparison.OrdinalIgnoreCase))));

                if (incubator == null)
                {
                    Core.Plugin.LogMessage("FindIncubatorInStash: No incubator found in visible items");
                    return null;
                }

                var rect = incubator.GetClientRect();
                var center = rect.Center;
                var position = new Vector2(center.X, center.Y);

                Core.Plugin.LogMessage($"FindIncubatorInStash: Found incubator at position ({position.X}, {position.Y}), rect: {rect.Width}x{rect.Height}");
                Core.Plugin.LogMessage($"FindIncubatorInStash: Incubator Path: {incubator.Item.Path}");

                return position;
            }
            catch (Exception ex)
            {
                Core.Plugin.LogError($"FindIncubatorInStash error: {ex.Message}");
                return null;
            }
        }

        public void Render() { }
    }
}
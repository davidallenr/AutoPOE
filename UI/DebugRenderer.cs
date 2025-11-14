using ExileCore;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AutoPOE.UI
{
    /// <summary>
    /// Handles all debug visualization and overlay rendering.
    /// Follows Single Responsibility Principle by managing only debug display concerns.
    /// </summary>
    public class DebugRenderer
    {
        private readonly GameController _gameController;
        private readonly ExileCore.Graphics _graphics;
        private readonly Settings _settings;
        private readonly EquipmentCalibrationManager _calibrationManager;

        public DebugRenderer(
            GameController gameController,
            ExileCore.Graphics graphics,
            Settings settings,
            EquipmentCalibrationManager calibrationManager)
        {
            _gameController = gameController ?? throw new System.ArgumentNullException(nameof(gameController));
            _graphics = graphics ?? throw new System.ArgumentNullException(nameof(graphics));
            _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
            _calibrationManager = calibrationManager ?? throw new System.ArgumentNullException(nameof(calibrationManager));
        }

        /// <summary>
        /// Renders the main debug information overlay showing bot status, positions, and system info.
        /// </summary>
        /// <param name="startPos">Starting screen position for the debug text</param>
        public void RenderDebugInfo(Vector2 startPos)
        {
            var drawPos = startPos;

            _graphics.DrawText("=== DEBUG INFO ===", drawPos, SharpDX.Color.Yellow);
            drawPos.Y += 20;

            _graphics.DrawText($"Bot Running: {Core.IsBotRunning}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            _graphics.DrawText($"Can Use Action: {Core.CanUseAction}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            _graphics.DrawText($"In Hideout: {_gameController.Area.CurrentArea.IsHideout}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            _graphics.DrawText($"Player Position: {_gameController.Player.GridPos}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            _graphics.DrawText($"Mouse Position: {Input.MousePosition}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            var windowRect = _gameController.Window.GetWindowRectangle();
            _graphics.DrawText($"Window Resolution: {windowRect.Width}x{windowRect.Height}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            _graphics.DrawText($"Current Area: {_gameController.Area.CurrentArea.DisplayName}", drawPos, SharpDX.Color.Cyan);

            // Visual Debug Rendering
            if (_settings.Debug.DrawInventory)
            {
                RenderInventoryOverlay();
            }
        }

        /// <summary>
        /// Renders visual overlays for inventory items and equipment slots.
        /// Shows green boxes around inventory items and cyan boxes around equipment slots.
        /// </summary>
        private void RenderInventoryOverlay()
        {
            var inventoryPanel = _gameController.IngameState.IngameUi.InventoryPanel;
            if (inventoryPanel == null || !inventoryPanel.IsVisible)
                return;

            var playerInventory = _gameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
            if (playerInventory?.InventorySlotItems == null)
                return;

            // Draw boxes around each inventory item
            foreach (var item in playerInventory.InventorySlotItems)
            {
                if (item?.Item == null)
                    continue;

                var rect = item.GetClientRect();
                _graphics.DrawFrame(rect, SharpDX.Color.Lime, 2);

                // Draw item name above the box
                var textPos = new Vector2(rect.X, rect.Y - 15);
                var itemName = item.Item.Path.Split('/').LastOrDefault() ?? "Unknown";
                _graphics.DrawText(itemName, textPos, SharpDX.Color.Yellow, 10);
            }

            // Draw equipment slots (but only if calibration mode is OFF to avoid overlap)
            if (!_settings.Calibration.CalibrateEquipment)
            {
                RenderEquipmentSlots();
            }
        }

        /// <summary>
        /// Renders cyan boxes around equipment slot positions for visualization.
        /// </summary>
        private void RenderEquipmentSlots()
        {
            var equipmentSlots = new List<InventorySlotE>
            {
                InventorySlotE.Helm1,
                InventorySlotE.BodyArmour1,
                InventorySlotE.Weapon1,
                InventorySlotE.Offhand1,
                InventorySlotE.Amulet1,
                InventorySlotE.Ring1,
                InventorySlotE.Ring2,
                InventorySlotE.Gloves1,
                InventorySlotE.Boots1,
                InventorySlotE.Belt1
            };

            foreach (var slot in equipmentSlots)
            {
                if (!_calibrationManager.EquipmentPositions.ContainsKey(slot))
                    continue;

                var position = _calibrationManager.EquipmentPositions[slot];
                var size = 60f;
                var rect = new SharpDX.RectangleF(
                    position.X - size / 2,
                    position.Y - size / 2,
                    size,
                    size
                );

                _graphics.DrawFrame(rect, SharpDX.Color.Cyan, 2);

                // Draw slot name
                var textPos = new Vector2(rect.X, rect.Y - 15);
                _graphics.DrawText(slot.ToString(), textPos, SharpDX.Color.White, 9);
            }
        }
    }
}

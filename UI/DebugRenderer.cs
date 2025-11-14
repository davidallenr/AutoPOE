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

            // Bot Status
            _graphics.DrawText($"Bot Running: {Core.IsBotRunning}", drawPos, Core.IsBotRunning ? SharpDX.Color.LimeGreen : SharpDX.Color.Red);
            drawPos.Y += 20;
            
            _graphics.DrawText($"Farm Method: {Core.Settings.FarmMethod.Value}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            
            // NEW: Window Focus Check (important for our new safety feature)
            var isForeground = Core.IsGameWindowForeground();
            _graphics.DrawText($"Window Focused: {isForeground}", drawPos, isForeground ? SharpDX.Color.LimeGreen : SharpDX.Color.Orange);
            drawPos.Y += 20;
            
            // Action Status - shows both timing and focus requirements
            var canUseAction = Core.CanUseAction;
            _graphics.DrawText($"Can Use Action: {canUseAction}", drawPos, canUseAction ? SharpDX.Color.LimeGreen : SharpDX.Color.Orange);
            drawPos.Y += 20;
            
            // NEW: Action Frequency setting
            _graphics.DrawText($"Action Delay: {Core.Settings.ActionFrequency.Value}ms", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;

            // Location Info
            _graphics.DrawText($"In Hideout: {_gameController.Area.CurrentArea.IsHideout}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            
            _graphics.DrawText($"Current Area: {_gameController.Area.CurrentArea.DisplayName}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;

            // Position Info
            _graphics.DrawText($"Player Position: {_gameController.Player.GridPos}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            
            _graphics.DrawText($"Mouse Position: {Input.MousePosition}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            
            // NEW: Window bounds info (relevant to our clamping feature)
            var windowRect = _gameController.Window.GetWindowRectangle();
            _graphics.DrawText($"Window: {windowRect.Width}x{windowRect.Height} @ ({windowRect.X},{windowRect.Y})", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;

            // NEW: Incubator status
            if (Core.Settings.FarmMethod == "Simulacrum")
            {
                _graphics.DrawText($"Has Incubators: {Core.HasIncubators}", drawPos, Core.HasIncubators ? SharpDX.Color.LimeGreen : SharpDX.Color.Gray);
                drawPos.Y += 20;
                
                // Debug: Show inventory/stash details
                var stashElement = _gameController.IngameState.IngameUi.StashElement;
                if (stashElement?.VisibleStash != null)
                {
                    var invType = stashElement.VisibleStash.InvType;
                    var itemCount = stashElement.VisibleStash.VisibleInventoryItems?.Count ?? 0;
                    _graphics.DrawText($"Stash Type: {invType}, Items: {itemCount}", drawPos, SharpDX.Color.Yellow);
                    drawPos.Y += 20;
                    
                    // Check for incubators in stash
                    var incubatorItems = stashElement.VisibleStash.VisibleInventoryItems?
                        .Where(item => item?.Item != null && (
                            (!string.IsNullOrEmpty(item.Item.Path) && item.Item.Path.Contains("Incubation", System.StringComparison.OrdinalIgnoreCase)) || 
                            (!string.IsNullOrEmpty(item.Item.Metadata) && item.Item.Metadata.Contains("Incubation", System.StringComparison.OrdinalIgnoreCase))))
                        .ToList();
                    
                    var incubCount = incubatorItems?.Count ?? 0;
                    _graphics.DrawText($"  Incubators Found: {incubCount}", drawPos, incubCount > 0 ? SharpDX.Color.LimeGreen : SharpDX.Color.Gray);
                    drawPos.Y += 15;
                    
                    // Show first few items in stash
                    if (stashElement.VisibleStash.VisibleInventoryItems != null)
                    {
                        int itemsShown = 0;
                        foreach (var item in stashElement.VisibleStash.VisibleInventoryItems.Take(5))
                        {
                            if (item?.Item != null)
                            {
                                var path = item.Item.Path ?? "null";
                                var metadata = item.Item.Metadata ?? "null";
                                var shortPath = path.Length > 40 ? "..." + path.Substring(path.Length - 37) : path;
                                var shortMeta = metadata.Length > 40 ? "..." + metadata.Substring(metadata.Length - 37) : metadata;
                                
                                _graphics.DrawText($"  Path: {shortPath}", drawPos, SharpDX.Color.Gray);
                                drawPos.Y += 15;
                                _graphics.DrawText($"  Meta: {shortMeta}", drawPos, SharpDX.Color.Gray);
                                drawPos.Y += 15;
                                
                                itemsShown++;
                                if (itemsShown >= 3) break;
                            }
                        }
                    }
                }
                else
                {
                    _graphics.DrawText($"Stash: Not Open", drawPos, SharpDX.Color.Gray);
                    drawPos.Y += 20;
                }
                
                // Debug: Show inventory details
                try
                {
                    var playerInv = _gameController.IngameState.Data.ServerData.PlayerInventories?[0]?.Inventory;
                    if (playerInv != null && playerInv.InventorySlotItems != null)
                    {
                        var itemCount = playerInv.InventorySlotItems.Count;
                        _graphics.DrawText($"Inventory Items: {itemCount}", drawPos, SharpDX.Color.Yellow);
                        drawPos.Y += 20;
                        
                        // Check for incubators and show which items match
                        var incubatorItems = playerInv.InventorySlotItems
                            .Where(item => item?.Item != null && (
                                (!string.IsNullOrEmpty(item.Item.Path) && item.Item.Path.Contains("Incubation", System.StringComparison.OrdinalIgnoreCase)) || 
                                (!string.IsNullOrEmpty(item.Item.Metadata) && item.Item.Metadata.Contains("Incubation", System.StringComparison.OrdinalIgnoreCase))))
                            .ToList();
                        
                        _graphics.DrawText($"  Incubators Found: {incubatorItems.Count}", drawPos, incubatorItems.Count > 0 ? SharpDX.Color.LimeGreen : SharpDX.Color.Gray);
                        drawPos.Y += 15;
                        
                        // Show first item in inventory
                        var firstItem = playerInv.InventorySlotItems.FirstOrDefault(i => i?.Item != null);
                        if (firstItem?.Item != null)
                        {
                            var path = firstItem.Item.Path ?? "null";
                            var metadata = firstItem.Item.Metadata ?? "null";
                            var shortPath = path.Length > 40 ? "..." + path.Substring(path.Length - 37) : path;
                            var shortMeta = metadata.Length > 40 ? "..." + metadata.Substring(metadata.Length - 37) : metadata;
                            
                            var hasIncub = path.Contains("Incubation", System.StringComparison.OrdinalIgnoreCase) || 
                                          metadata.Contains("Incubation", System.StringComparison.OrdinalIgnoreCase);
                            
                            _graphics.DrawText($"  First Item Path: {shortPath}", drawPos, hasIncub ? SharpDX.Color.LimeGreen : SharpDX.Color.Gray);
                            drawPos.Y += 15;
                            _graphics.DrawText($"  First Item Meta: {shortMeta}", drawPos, hasIncub ? SharpDX.Color.LimeGreen : SharpDX.Color.Gray);
                            drawPos.Y += 15;
                        }
                    }
                    else
                    {
                        _graphics.DrawText($"Inventory: Not Open", drawPos, SharpDX.Color.Gray);
                        drawPos.Y += 20;
                    }
                }
                catch (System.Exception ex)
                {
                    _graphics.DrawText($"Inventory: Error - {ex.Message}", drawPos, SharpDX.Color.Red);
                    drawPos.Y += 20;
                }
            }

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

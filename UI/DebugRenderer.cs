using AutoPOE.Logic;
using ExileCore;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
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
        /// <param name="sequenceManager">The sequence manager to get current action info</param>
        /// <returns>The Y position where the debug info ends</returns>
        public float RenderDebugInfo(Vector2 startPos, SequenceManager sequenceManager)
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
            if (Core.Settings.FarmMethod.Value == "Simulacrum")
            {
                _graphics.DrawText($"Has Incubators: {Core.HasIncubators}", drawPos, Core.HasIncubators ? SharpDX.Color.LimeGreen : SharpDX.Color.Gray);
                drawPos.Y += 20;
            }

            // Current Action Info (general, applies to all actions)
            var generalSequence = sequenceManager.GetCurrentSequence(_settings.FarmMethod.Value);
            if (generalSequence is Logic.Sequences.SimulacrumSequence generalSimSeq)
            {
                var actionInfo = generalSimSeq.CurrentAction?.GetType().Name ?? "None";
                _graphics.DrawText($"Current Action: {actionInfo}", drawPos,
                    actionInfo == "CombatAction" ? SharpDX.Color.Red :
                    actionInfo == "ExploreAction" ? SharpDX.Color.Yellow :
                    actionInfo == "IdleAction" ? SharpDX.Color.Gray : SharpDX.Color.Cyan);
                drawPos.Y += 20;
            }

            drawPos.Y += 10; // Add spacing before next section            // Debug: Show inventory/stash details
            if (_settings.Debug.ShowStashDebug)
            {
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

                    // Show first few items in stash (only if ShowItemDetails is enabled)
                    if (_settings.Debug.ShowItemDetails && stashElement.VisibleStash.VisibleInventoryItems != null)
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
            }

            // Debug: Show inventory details
            if (_settings.Debug.ShowInventoryDebug)
            {
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

                        // Show first item in inventory (only if ShowItemDetails is enabled)
                        if (_settings.Debug.ShowItemDetails)
                        {
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

            // Combat Strategy Info - Only show if enabled
            if (_settings.Debug.ShowCombatDebug)
            {
                drawPos.Y += 10; // Add spacing
                _graphics.DrawText("=== COMBAT ===", drawPos, SharpDX.Color.Yellow);
                drawPos.Y += 20;

                // Get current sequence and action
                var currentSequence = sequenceManager.GetCurrentSequence(_settings.FarmMethod.Value);
                string strategyName = "None";
                string targetReason = "N/A";
                string targetInfo = "None";

                if (currentSequence is Logic.Sequences.SimulacrumSequence simulacrumSeq)
                {
                    if (simulacrumSeq.CurrentAction is Logic.Actions.CombatAction combatAction)
                    {
                        var strategy = combatAction.CurrentStrategy;
                        strategyName = strategy.Name;
                        targetReason = strategy.LastTargetReason;

                        if (combatAction.LastTarget.HasValue)
                        {
                            var target = combatAction.LastTarget.Value;
                            var distance = target.Distance(_gameController.Player.GridPosNum);
                            targetInfo = $"({target.X:F0}, {target.Y:F0}) @ {distance:F1}u";
                        }
                    }

                    // Combat Target Validation (moved from Exploration section)
                    var playerPos = _gameController.Player.GridPosNum;
                    var hasValidTargets = simulacrumSeq.HasValidCombatTargets();
                    var combatRange = simulacrumSeq.CurrentAction is Logic.Actions.CombatAction combatActionRange ?
                        combatActionRange.CurrentStrategy?.GetMaxCombatRange() ?? Core.Settings.CombatDistance.Value :
                        Core.Settings.CombatDistance.Value;

                    // Monster Analysis (moved from Exploration section)
                    var allMonsters = Core.GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                        .Where(m => m.IsAlive && m.IsTargetable && m.IsHostile)
                        .ToList();
                    var monstersInRange = allMonsters.Count(m => m.GridPosNum.Distance(playerPos) < combatRange);
                    var totalMonsters = allMonsters.Count;

                    // Boss Detection
                    var bossMonsters = allMonsters
                        .Where(m => m.Rarity == ExileCore.Shared.Enums.MonsterRarity.Unique &&
                                   (m.RenderName?.Contains("Kosis") == true ||
                                    m.RenderName?.Contains("Omniphobia") == true ||
                                    m.RenderName?.Contains("Delirium Boss") == true))
                        .ToList();

                    // Display combat info
                    _graphics.DrawText($"Strategy: {strategyName}", drawPos, strategyName != "None" ? SharpDX.Color.Cyan : SharpDX.Color.Gray);
                    drawPos.Y += 20;

                    _graphics.DrawText($"Combat Range: {combatRange}", drawPos, SharpDX.Color.Cyan);
                    drawPos.Y += 20;

                    _graphics.DrawText($"Valid Targets: {hasValidTargets}", drawPos, hasValidTargets ? SharpDX.Color.LimeGreen : SharpDX.Color.Red);
                    drawPos.Y += 20;

                    _graphics.DrawText($"Monsters: {monstersInRange}/{totalMonsters} in range", drawPos,
                        monstersInRange > 0 ? SharpDX.Color.LimeGreen : SharpDX.Color.Gray);
                    drawPos.Y += 20;

                    // Boss Priority Display
                    if (bossMonsters.Any())
                    {
                        var boss = bossMonsters.First();
                        var bossDistance = boss.GridPosNum.Distance(playerPos);
                        _graphics.DrawText($"BOSS DETECTED: {boss.RenderName} @ {bossDistance:F1}u", drawPos, SharpDX.Color.Gold);
                        drawPos.Y += 20;
                    }
                    else
                    {
                        _graphics.DrawText($"Boss Status: None detected", drawPos, SharpDX.Color.Gray);
                        drawPos.Y += 20;
                    }

                    _graphics.DrawText($"Target Reason: {targetReason}", drawPos, targetReason != "N/A" ? SharpDX.Color.Cyan : SharpDX.Color.Gray);
                    drawPos.Y += 20;

                    _graphics.DrawText($"Target Position: {targetInfo}", drawPos, targetInfo.Contains("@") ? SharpDX.Color.LimeGreen : SharpDX.Color.Gray);
                    drawPos.Y += 20;

                }
            }

            // Exploration Debug Info - Only show if enabled
            if (_settings.Debug.ShowExplorationDebug)
            {
                drawPos.Y += 10; // Add spacing
                _graphics.DrawText("=== EXPLORATION ===", drawPos, SharpDX.Color.Yellow);
                drawPos.Y += 20;

                var currentSequence = sequenceManager.GetCurrentSequence(_settings.FarmMethod.Value);
                if (currentSequence is Logic.Sequences.SimulacrumSequence simulacrumSeq)
                {
                    var actionInfo = simulacrumSeq.CurrentAction?.GetType().Name ?? "None";
                    _graphics.DrawText($"Action: {actionInfo}", drawPos, SharpDX.Color.Gray);
                    drawPos.Y += 20;

                    // Simulacrum Center (core to exploration)
                    var simulacrumCenter = Core.Map.GetSimulacrumCenter();
                    var centerValid = simulacrumCenter != Vector2.Zero;
                    _graphics.DrawText($"Simulacrum Center: {simulacrumCenter} (Valid: {centerValid})", drawPos,
                        centerValid ? SharpDX.Color.LimeGreen : SharpDX.Color.Red);
                    drawPos.Y += 20;

                    var playerPos = _gameController.Player.GridPosNum;
                    var distanceToCenter = simulacrumCenter != Vector2.Zero ? playerPos.Distance(simulacrumCenter) : 0;
                    _graphics.DrawText($"Distance to Center: {distanceToCenter:F1}", drawPos, SharpDX.Color.Cyan);
                    drawPos.Y += 20;

                    // Pathfinding Information
                    var hasPath = false;
                    Vector2? pathTarget = null;
                    string nextChunkInfo = "N/A";
                    int blacklistedCount = 0;
                    int consecutiveFailures = 0;
                    Vector2? nearbyEnemyTarget = null;

                    if (simulacrumSeq.CurrentAction is Logic.Actions.ExploreAction exploreAction)
                    {
                        hasPath = exploreAction.CurrentPath != null;
                        pathTarget = exploreAction.CurrentPath?.Next;
                        blacklistedCount = exploreAction.BlacklistedChunkCount;
                        consecutiveFailures = exploreAction.ConsecutiveFailures;

                        // Check if exploration is targeting nearby enemies
                        var nearbyEnemies = Core.GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                            .Where(m => m.IsHostile && m.IsTargetable && m.IsAlive)
                            .Where(m => m.GridPosNum.Distance(playerPos) > Core.Settings.CombatDistance.Value)
                            .Where(m => m.GridPosNum.Distance(playerPos) <= Math.Min(Core.Settings.CombatDistance.Value * 3, 150))
                            .OrderBy(m => m.GridPosNum.Distance(playerPos))
                            .FirstOrDefault();

                        if (nearbyEnemies != null)
                        {
                            nearbyEnemyTarget = nearbyEnemies.GridPosNum;
                        }
                    }

                    var nextChunk = Core.Map.GetNextUnrevealedChunk();
                    nextChunkInfo = nextChunk != null ? $"{nextChunk.Position}" : "All Revealed";

                    // Path Information
                    var pathInfo = hasPath ? $"Target: {pathTarget}" : "No Path";
                    _graphics.DrawText($"Current Path: {pathInfo}", drawPos, hasPath ? SharpDX.Color.Cyan : SharpDX.Color.Orange);
                    drawPos.Y += 20;

                    // Enemy Seeking (new intelligent exploration behavior)
                    if (nearbyEnemyTarget.HasValue)
                    {
                        var enemyDistance = nearbyEnemyTarget.Value.Distance(playerPos);
                        _graphics.DrawText($"Seeking Enemy: {nearbyEnemyTarget.Value} @ {enemyDistance:F1}u", drawPos, SharpDX.Color.LightGreen);
                        drawPos.Y += 20;
                    }
                    else
                    {
                        _graphics.DrawText($"Enemy Seeking: None found", drawPos, SharpDX.Color.Gray);
                        drawPos.Y += 20;
                    }

                    // Chunk Exploration
                    _graphics.DrawText($"Next Chunk: {nextChunkInfo}", drawPos, nextChunk != null ? SharpDX.Color.Cyan : SharpDX.Color.Gray);
                    drawPos.Y += 20;

                    _graphics.DrawText($"Blacklisted Chunks: {blacklistedCount}", drawPos, blacklistedCount > 0 ? SharpDX.Color.Orange : SharpDX.Color.Gray);
                    drawPos.Y += 20;

                    _graphics.DrawText($"Pathfinding Failures: {consecutiveFailures}", drawPos,
                        consecutiveFailures > 50 ? SharpDX.Color.Red :
                        consecutiveFailures > 25 ? SharpDX.Color.Orange : SharpDX.Color.Gray);
                    drawPos.Y += 20;

                    // Show warning if too many failures
                    if (consecutiveFailures > 25)
                    {
                        _graphics.DrawText($"WARNING: High pathfinding failures - may be stuck!", drawPos, SharpDX.Color.Red);
                        drawPos.Y += 20;
                    }

                }
            }

            // Visual Debug Rendering
            if (_settings.Debug.DrawInventory)
            {
                RenderInventoryOverlay();
            }

            return drawPos.Y;
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
                _graphics.DrawText(itemName, textPos, SharpDX.Color.Yellow);
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
                _graphics.DrawText(slot.ToString(), textPos, SharpDX.Color.White);
            }
        }
    }
}
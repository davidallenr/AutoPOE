using ExileCore;
using ExileCore.Shared.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace AutoPOE.UI
{
    /// <summary>
    /// Manages equipment slot position calibration, including loading, saving, and UI rendering.
    /// Follows Single Responsibility Principle by handling only calibration concerns.
    /// </summary>
    public class EquipmentCalibrationManager
    {
        private readonly GameController _gameController;
        private readonly ExileCore.Graphics _graphics;
        private readonly Settings _settings;
        private readonly string _configDirectory;
        private readonly Action<string> _logMessage;
        private readonly Action<string> _logError;

        private Dictionary<InventorySlotE, Vector2> _equipmentPositions = new Dictionary<InventorySlotE, Vector2>();
        private string _lastCalibrationSlot = "Helm1";
        private DateTime _lastPositionChangeTime = DateTime.MinValue;
        private bool _hasPendingSave = false;

        private readonly List<InventorySlotE> _equipmentSlots = new List<InventorySlotE>
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

        public IReadOnlyDictionary<InventorySlotE, Vector2> EquipmentPositions => _equipmentPositions;

        public EquipmentCalibrationManager(
            GameController gameController,
            ExileCore.Graphics graphics,
            Settings settings,
            string configDirectory,
            Action<string> logMessage,
            Action<string> logError)
        {
            _gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
            _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _configDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
            _logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
            _logError = logError ?? throw new ArgumentNullException(nameof(logError));
        }

        /// <summary>
        /// Initializes equipment positions by loading from file or using defaults.
        /// </summary>
        public void Initialize()
        {
            LoadEquipmentPositions();
        }

        private void LoadEquipmentPositions()
        {
            // Default positions as fallback
            var defaultPositions = new Dictionary<InventorySlotE, Vector2>
            {
                { InventorySlotE.Helm1, new Vector2(1585, 165) },
                { InventorySlotE.BodyArmour1, new Vector2(1587, 296) },
                { InventorySlotE.Weapon1, new Vector2(1369, 233) },
                { InventorySlotE.Offhand1, new Vector2(1784, 232) },
                { InventorySlotE.Amulet1, new Vector2(1690, 245) },
                { InventorySlotE.Ring1, new Vector2(1480, 300) },
                { InventorySlotE.Ring2, new Vector2(1687, 306) },
                { InventorySlotE.Gloves1, new Vector2(1453, 398) },
                { InventorySlotE.Boots1, new Vector2(1719, 391) },
                { InventorySlotE.Belt1, new Vector2(1584, 421) }
            };

            // Try to load saved positions from file
            var configPath = Path.Combine(_configDirectory, "equipment_positions.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var savedPositions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);

                    _equipmentPositions = new Dictionary<InventorySlotE, Vector2>();
                    foreach (var kvp in savedPositions)
                    {
                        if (Enum.TryParse<InventorySlotE>(kvp.Key, out var slot))
                        {
                            var x = kvp.Value.GetProperty("X").GetSingle();
                            var y = kvp.Value.GetProperty("Y").GetSingle();
                            _equipmentPositions[slot] = new Vector2(x, y);
                        }
                    }

                    _logMessage($"Loaded equipment positions from {configPath}");
                }
                catch (Exception ex)
                {
                    _logError($"Failed to load equipment positions: {ex.Message}");
                    _equipmentPositions = defaultPositions;
                }
            }
            else
            {
                _equipmentPositions = defaultPositions;
            }
        }

        private void SaveEquipmentPositions()
        {
            try
            {
                var configPath = Path.Combine(_configDirectory, "equipment_positions.json");

                // Convert Vector2 to a serializable format
                var saveData = _equipmentPositions.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => new { X = kvp.Value.X, Y = kvp.Value.Y }
                );

                var json = System.Text.Json.JsonSerializer.Serialize(saveData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(configPath, json);
                _logMessage($"Saved equipment positions to {configPath}");
            }
            catch (Exception ex)
            {
                _logError($"Failed to save equipment positions: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates calibration logic and handles debounced saving.
        /// Should be called every frame when calibration is active.
        /// </summary>
        public void Update()
        {
            // Handle debounced save for equipment positions
            if (_hasPendingSave && (DateTime.Now - _lastPositionChangeTime).TotalMilliseconds > 500)
            {
                SaveEquipmentPositions();
                _hasPendingSave = false;
            }
        }

        /// <summary>
        /// Renders the calibration UI overlay.
        /// </summary>
        /// <param name="debugInfoEndY">The Y position where debug info ends, to avoid overlap</param>
        public void RenderCalibrationMode(float debugInfoEndY = 0)
        {
            if (_equipmentPositions == null)
            {
                _logError("Equipment positions not initialized in RenderCalibrationMode");
                return;
            }

            var inventoryPanel = _gameController.IngameState.IngameUi.InventoryPanel;
            if (inventoryPanel == null || !inventoryPanel.IsVisible)
            {
                return;
            }

            // Get the currently selected slot from settings
            var selectedSlotName = _settings.Calibration.CalibrationSlot.Value;
            if (string.IsNullOrEmpty(selectedSlotName))
            {
                _logError("CalibrationSlot value is null or empty");
                return;
            }

            if (!Enum.TryParse<InventorySlotE>(selectedSlotName, out var currentSlot))
            {
                _logError($"Failed to parse slot name: {selectedSlotName}");
                return;
            }

            // Ensure the slot exists in the dictionary
            if (!_equipmentPositions.ContainsKey(currentSlot))
            {
                // Initialize with default position if missing
                _equipmentPositions[currentSlot] = new Vector2(1585, 300);
            }

            // Check if the user changed the selected slot in the dropdown
            if (selectedSlotName != _lastCalibrationSlot)
            {
                // Slot changed - update sliders to show this slot's current position
                var currentPosition = _equipmentPositions[currentSlot];
                _settings.Calibration.CalibrationX.Value = (int)currentPosition.X;
                _settings.Calibration.CalibrationY.Value = (int)currentPosition.Y;
                _lastCalibrationSlot = selectedSlotName;
            }

            // Update position from settings sliders
            var newPosition = new Vector2(
                _settings.Calibration.CalibrationX.Value,
                _settings.Calibration.CalibrationY.Value
            );

            // Check if position actually changed
            var oldPosition = _equipmentPositions[currentSlot];
            bool positionChanged = oldPosition != newPosition;

            _equipmentPositions[currentSlot] = newPosition;

            // Handle keyboard input for faster adjustments
            var moveSpeed = Input.IsKeyDown(Keys.ShiftKey) ? 5f : 1f;

            if (Input.IsKeyDown(Keys.Left))
            {
                _settings.Calibration.CalibrationX.Value -= (int)moveSpeed;
                _equipmentPositions[currentSlot] = new Vector2(
                    _equipmentPositions[currentSlot].X - moveSpeed, _equipmentPositions[currentSlot].Y);
                positionChanged = true;
            }
            if (Input.IsKeyDown(Keys.Right))
            {
                _settings.Calibration.CalibrationX.Value += (int)moveSpeed;
                _equipmentPositions[currentSlot] = new Vector2(
                    _equipmentPositions[currentSlot].X + moveSpeed, _equipmentPositions[currentSlot].Y);
                positionChanged = true;
            }
            if (Input.IsKeyDown(Keys.Up))
            {
                _settings.Calibration.CalibrationY.Value -= (int)moveSpeed;
                _equipmentPositions[currentSlot] = new Vector2(
                    _equipmentPositions[currentSlot].X, _equipmentPositions[currentSlot].Y - moveSpeed);
                positionChanged = true;
            }
            if (Input.IsKeyDown(Keys.Down))
            {
                _settings.Calibration.CalibrationY.Value += (int)moveSpeed;
                _equipmentPositions[currentSlot] = new Vector2(
                    _equipmentPositions[currentSlot].X, _equipmentPositions[currentSlot].Y + moveSpeed);
                positionChanged = true;
            }

            // Mark for debounced save if position changed
            if (positionChanged)
            {
                _lastPositionChangeTime = DateTime.Now;
                _hasPendingSave = true;
            }

            // Render visual overlays
            RenderEquipmentSlotOverlays(currentSlot, newPosition);
            RenderInstructions(currentSlot, newPosition, debugInfoEndY);
        }

        private void RenderEquipmentSlotOverlays(InventorySlotE currentSlot, Vector2 newPosition)
        {
            // Draw all equipment slots with a faded overlay
            foreach (var slot in _equipmentSlots)
            {
                // Skip if slot isn't in the dictionary yet
                if (!_equipmentPositions.ContainsKey(slot))
                    continue;

                var position = _equipmentPositions[slot];
                var isSelected = slot == currentSlot;

                var size = 60f;
                var rect = new SharpDX.RectangleF(
                    position.X - size / 2,
                    position.Y - size / 2,
                    size,
                    size
                );

                // Color: Bright yellow if selected, faded green otherwise
                SharpDX.Color color;
                int thickness;

                if (isSelected)
                {
                    color = SharpDX.Color.Yellow;
                    thickness = 4;
                }
                else
                {
                    color = new SharpDX.Color(0, 255, 0, 100); // Transparent green
                    thickness = 1;
                }

                _graphics.DrawFrame(rect, color, thickness);

                // Draw crosshair for selected slot only
                if (isSelected)
                {
                    _graphics.DrawLine(
                        new Vector2(position.X - 20, position.Y),
                        new Vector2(position.X + 20, position.Y),
                        2, color);
                    _graphics.DrawLine(
                        new Vector2(position.X, position.Y - 20),
                        new Vector2(position.X, position.Y + 20),
                        2, color);
                }

                // Draw slot name
                var textPos = new Vector2(rect.X, rect.Y - 20);
                var textColor = isSelected ? SharpDX.Color.Yellow : new SharpDX.Color(255, 255, 255, 100);
                _graphics.DrawText(slot.ToString(), textPos, textColor);
            }
        }

        private void RenderInstructions(InventorySlotE currentSlot, Vector2 newPosition, float debugInfoEndY)
        {
            // Position instructions below debug info to avoid overlap
            var instructPos = new Vector2(50, Math.Max(500, debugInfoEndY + 20));

            _graphics.DrawText("=== CALIBRATION MODE ===", instructPos, SharpDX.Color.Yellow);
            instructPos.Y += 25;
            _graphics.DrawText($"Calibrating: {currentSlot}", instructPos, SharpDX.Color.White);
            instructPos.Y += 20;
            _graphics.DrawText($"Position: X={newPosition.X:F0}, Y={newPosition.Y:F0}", instructPos, SharpDX.Color.Cyan);
            instructPos.Y += 20;
            _graphics.DrawText("1. Select slot in settings dropdown", instructPos, SharpDX.Color.White);
            instructPos.Y += 18;
            _graphics.DrawText("2. Use sliders OR arrow keys to position", instructPos, SharpDX.Color.White);
            instructPos.Y += 18;
            _graphics.DrawText("3. Repeat for all equipment slots", instructPos, SharpDX.Color.White);
        }
    }
}

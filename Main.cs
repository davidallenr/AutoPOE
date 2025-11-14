
using AutoPOE.Logic;
using AutoPOE.Logic.Sequences;
using AutoPOE.Logic.Sequences;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System.Windows.Forms;

namespace AutoPOE
{
    public class Main : BaseSettingsPlugin<Settings>
    {
        private bool _wasInSimulacrum = false;
        private ISequence? _sequence;

        // Equipment calibration
        private Dictionary<InventorySlotE, System.Numerics.Vector2> _equipmentPositions = new Dictionary<InventorySlotE, System.Numerics.Vector2>();
        private int _selectedSlotIndex = 0;
        private string _lastCalibrationSlot = "Helm1";
        private DateTime _lastPositionChangeTime = DateTime.MinValue;
        private bool _hasPendingSave = false;
        private List<InventorySlotE> _equipmentSlots = new List<InventorySlotE>
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

        private ISequence _scarabTraderSequence = new ScarabTraderSequence();
        private ISequence _debugSequence = new DebugSequence();
        public override bool Initialise()
        {
            this.Name = "Auto POE";

            Core.Initialize(GameController, Settings, Graphics, this);

            _sequence = new SimulacrumSequence();

            // Load or initialize equipment positions
            LoadEquipmentPositions();

            Settings.ConfigureSkills();
            return base.Initialise();
        }

        private void LoadEquipmentPositions()
        {
            // Default positions as fallback
            var defaultPositions = new Dictionary<InventorySlotE, System.Numerics.Vector2>
            {
                { InventorySlotE.Helm1, new System.Numerics.Vector2(1585, 165) },
                { InventorySlotE.BodyArmour1, new System.Numerics.Vector2(1587, 296) },
                { InventorySlotE.Weapon1, new System.Numerics.Vector2(1369, 233) },
                { InventorySlotE.Offhand1, new System.Numerics.Vector2(1784, 232) },
                { InventorySlotE.Amulet1, new System.Numerics.Vector2(1690, 245) },
                { InventorySlotE.Ring1, new System.Numerics.Vector2(1480, 300) },
                { InventorySlotE.Ring2, new System.Numerics.Vector2(1687, 306) },
                { InventorySlotE.Gloves1, new System.Numerics.Vector2(1453, 398) },
                { InventorySlotE.Boots1, new System.Numerics.Vector2(1719, 391) },
                { InventorySlotE.Belt1, new System.Numerics.Vector2(1584, 421) }
            };

            // Try to load saved positions from file
            var configPath = Path.Combine(ConfigDirectory, "equipment_positions.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var savedPositions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);
                    
                    _equipmentPositions = new Dictionary<InventorySlotE, System.Numerics.Vector2>();
                    foreach (var kvp in savedPositions)
                    {
                        if (Enum.TryParse<InventorySlotE>(kvp.Key, out var slot))
                        {
                            var x = kvp.Value.GetProperty("X").GetSingle();
                            var y = kvp.Value.GetProperty("Y").GetSingle();
                            _equipmentPositions[slot] = new System.Numerics.Vector2(x, y);
                        }
                    }
                    
                    LogMessage($"Loaded equipment positions from {configPath}");
                }
                catch (Exception ex)
                {
                    LogError($"Failed to load equipment positions: {ex.Message}");
                    _equipmentPositions = defaultPositions;
                }
            }
            else
            {
                _equipmentPositions = defaultPositions;
            }

            // Sync to Core so StoreItemsAction can use them
            Core.EquipmentSlotPositions = _equipmentPositions;
        }

        private void SaveEquipmentPositions()
        {
            try
            {
                var configPath = Path.Combine(ConfigDirectory, "equipment_positions.json");
                
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
                LogMessage($"Saved equipment positions to {configPath}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to save equipment positions: {ex.Message}");
            }
        }


        public override Job Tick()
        {
            if (!Core.IsBotRunning || !Settings.Enable || !GameController.InGame)
                return base.Tick();

            if (GameController.IsLoading)
                return base.Tick();

            if (Core.CanUseAction)
            {
                switch (Core.Settings.FarmMethod)
                {
                    case "Simulacrum":
                        _sequence?.Tick();
                        break;
                    case "ScarabTrader":
                        _scarabTraderSequence?.Tick();
                        break;
                    case "Debug":
                        _debugSequence?.Tick();
                        break;

                }
            }

            return base.Tick();
        }

        public override void Render()
        {
            if (!Settings.Enable || !GameController.InGame || GameController.IngameState.Data == null)
                return;

            if (Settings.StartBot.PressedOnce())
            {
                _scarabTraderSequence = new ScarabTraderSequence();
                Core.IsBotRunning = !Core.IsBotRunning;
            }

            switch (Core.Settings.FarmMethod)
            {
                case "Simulacrum":
                    _sequence?.Render();
                    break;
                case "ScarabTrader":
                    _scarabTraderSequence?.Render();
                    break;
                case "Debug":
                    _debugSequence?.Render();
                    break;
            }

            var drawPos = new System.Numerics.Vector2(100, 200);

            if (Core.Settings.FarmMethod == "Simulacrum")
            {
                if (!GameController.Area.CurrentArea.IsHideout)
                {
                    Graphics.DrawText($"Current Wave: {SimulacrumState.CurrentWave} / 15", drawPos, SharpDX.Color.White);
                    drawPos.Y += 20;
                    Graphics.DrawText($"Current Duration: {SimulacrumState.CurrentRunDuration:mm\\:ss}", drawPos, SharpDX.Color.White);
                    drawPos.Y += 20;
                }

                Graphics.DrawText($"Total Runs: {SimulacrumState.TotalRunsCompleted}", drawPos, SharpDX.Color.White);
                drawPos.Y += 20;
                Graphics.DrawText($"Avg. Time: {TimeSpan.FromSeconds(SimulacrumState.AverageTimePerRun):mm\\:ss}", drawPos, SharpDX.Color.White);
            }

            // Debug Mode Display
            if (Settings.Debug.EnableDebugMode)
            {
                drawPos.Y += 40;
                RenderDebugInfo(drawPos);
            }

            // Calibration Mode
            if (Settings.Calibration.CalibrateEquipment)
            {
                RenderCalibrationMode();
            }

            // Handle debounced save for equipment positions
            if (_hasPendingSave && (DateTime.Now - _lastPositionChangeTime).TotalMilliseconds > 500)
            {
                SaveEquipmentPositions();
                _hasPendingSave = false;
            }
        }

        private void RenderDebugInfo(System.Numerics.Vector2 startPos)
        {
            var drawPos = startPos;
            
            Graphics.DrawText("=== DEBUG INFO ===", drawPos, SharpDX.Color.Yellow);
            drawPos.Y += 20;

            Graphics.DrawText($"Bot Running: {Core.IsBotRunning}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            Graphics.DrawText($"Can Use Action: {Core.CanUseAction}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            Graphics.DrawText($"In Hideout: {GameController.Area.CurrentArea.IsHideout}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            Graphics.DrawText($"Player Position: {GameController.Player.GridPos}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            Graphics.DrawText($"Mouse Position: {Input.MousePosition}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            var windowRect = GameController.Window.GetWindowRectangle();
            Graphics.DrawText($"Window Resolution: {windowRect.Width}x{windowRect.Height}", drawPos, SharpDX.Color.Cyan);
            drawPos.Y += 20;
            Graphics.DrawText($"Current Area: {GameController.Area.CurrentArea.DisplayName}", drawPos, SharpDX.Color.Cyan);

            // Visual Debug Rendering
            if (Settings.Debug.DrawInventory)
            {
                RenderInventoryItems();
            }
        }

        private void RenderCalibrationMode()
        {
            if (_equipmentPositions == null)
            {
                LogError("Equipment positions not initialized in RenderCalibrationMode");
                return;
            }

            var inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel;
            if (inventoryPanel == null || !inventoryPanel.IsVisible)
            {
                return;
            }

            // Get the currently selected slot from settings
            var selectedSlotName = Settings.Calibration.CalibrationSlot.Value;
            if (string.IsNullOrEmpty(selectedSlotName))
            {
                LogError("CalibrationSlot value is null or empty");
                return;
            }

            if (!Enum.TryParse<InventorySlotE>(selectedSlotName, out var currentSlot))
            {
                LogError($"Failed to parse slot name: {selectedSlotName}");
                return;
            }

            // Ensure the slot exists in the dictionary
            if (!_equipmentPositions.ContainsKey(currentSlot))
            {
                // Initialize with default position if missing
                _equipmentPositions[currentSlot] = new System.Numerics.Vector2(1585, 300);
            }

            // Check if the user changed the selected slot in the dropdown
            if (selectedSlotName != _lastCalibrationSlot)
            {
                // Slot changed - update sliders to show this slot's current position
                var currentPosition = _equipmentPositions[currentSlot];
                Settings.Calibration.CalibrationX.Value = (int)currentPosition.X;
                Settings.Calibration.CalibrationY.Value = (int)currentPosition.Y;
                _lastCalibrationSlot = selectedSlotName;
            }

            // Update position from settings sliders
            var newPosition = new System.Numerics.Vector2(
                Settings.Calibration.CalibrationX.Value,
                Settings.Calibration.CalibrationY.Value
            );
            
            // Check if position actually changed
            var oldPosition = _equipmentPositions[currentSlot];
            bool positionChanged = oldPosition != newPosition;
            
            _equipmentPositions[currentSlot] = newPosition;

            // Sync to Core so StoreItemsAction uses the updated positions
            Core.EquipmentSlotPositions = _equipmentPositions;

            // Handle keyboard input for faster adjustments
            var moveSpeed = Input.IsKeyDown(Keys.ShiftKey) ? 5f : 1f;
            
            if (Input.IsKeyDown(Keys.Left))
            {
                Settings.Calibration.CalibrationX.Value -= (int)moveSpeed;
                _equipmentPositions[currentSlot] = new System.Numerics.Vector2(
                    _equipmentPositions[currentSlot].X - moveSpeed, _equipmentPositions[currentSlot].Y);
                positionChanged = true;
            }
            if (Input.IsKeyDown(Keys.Right))
            {
                Settings.Calibration.CalibrationX.Value += (int)moveSpeed;
                _equipmentPositions[currentSlot] = new System.Numerics.Vector2(
                    _equipmentPositions[currentSlot].X + moveSpeed, _equipmentPositions[currentSlot].Y);
                positionChanged = true;
            }
            if (Input.IsKeyDown(Keys.Up))
            {
                Settings.Calibration.CalibrationY.Value -= (int)moveSpeed;
                _equipmentPositions[currentSlot] = new System.Numerics.Vector2(
                    _equipmentPositions[currentSlot].X, _equipmentPositions[currentSlot].Y - moveSpeed);
                positionChanged = true;
            }
            if (Input.IsKeyDown(Keys.Down))
            {
                Settings.Calibration.CalibrationY.Value += (int)moveSpeed;
                _equipmentPositions[currentSlot] = new System.Numerics.Vector2(
                    _equipmentPositions[currentSlot].X, _equipmentPositions[currentSlot].Y + moveSpeed);
                positionChanged = true;
            }

            // Always sync after any changes
            Core.EquipmentSlotPositions = _equipmentPositions;
            
            // Mark for debounced save if position changed
            if (positionChanged)
            {
                _lastPositionChangeTime = DateTime.Now;
                _hasPendingSave = true;
            }

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
                
                Graphics.DrawFrame(rect, color, thickness);

                // Draw crosshair for selected slot only
                if (isSelected)
                {
                    Graphics.DrawLine(
                        new System.Numerics.Vector2(position.X - 20, position.Y),
                        new System.Numerics.Vector2(position.X + 20, position.Y),
                        2, color);
                    Graphics.DrawLine(
                        new System.Numerics.Vector2(position.X, position.Y - 20),
                        new System.Numerics.Vector2(position.X, position.Y + 20),
                        2, color);
                }

                // Draw slot name
                var textPos = new System.Numerics.Vector2(rect.X, rect.Y - 20);
                var textColor = isSelected ? SharpDX.Color.Yellow : new SharpDX.Color(255, 255, 255, 100);
                Graphics.DrawText(slot.ToString(), textPos, textColor, isSelected ? 11 : 8);
            }

            // Draw instructions
            var instructPos = new System.Numerics.Vector2(100, 500);
            Graphics.DrawText("=== CALIBRATION MODE ===", instructPos, SharpDX.Color.Yellow, 14);
            instructPos.Y += 25;
            Graphics.DrawText($"Calibrating: {currentSlot}", instructPos, SharpDX.Color.White, 12);
            instructPos.Y += 20;
            Graphics.DrawText($"Position: X={newPosition.X:F0}, Y={newPosition.Y:F0}", instructPos, SharpDX.Color.Cyan, 10);
            instructPos.Y += 20;
            Graphics.DrawText("1. Select slot in settings dropdown", instructPos, SharpDX.Color.White, 10);
            instructPos.Y += 18;
            Graphics.DrawText("2. Use sliders OR arrow keys to position", instructPos, SharpDX.Color.White, 10);
            instructPos.Y += 18;
            Graphics.DrawText("3. Repeat for all equipment slots", instructPos, SharpDX.Color.White, 10);
        }

        private void RenderInventoryItems()
        {
            if (_equipmentPositions == null)
                return;

            var inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel;
            if (inventoryPanel == null || !inventoryPanel.IsVisible)
                return;

            var playerInventory = GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
            if (playerInventory?.InventorySlotItems == null)
                return;

            // Draw boxes around each inventory item
            foreach (var item in playerInventory.InventorySlotItems)
            {
                if (item?.Item == null)
                    continue;

                var rect = item.GetClientRect();
                Graphics.DrawFrame(rect, SharpDX.Color.Lime, 2);
                
                // Draw item name above the box
                var textPos = new System.Numerics.Vector2(rect.X, rect.Y - 15);
                var itemName = item.Item.Path.Split('/').LastOrDefault() ?? "Unknown";
                Graphics.DrawText(itemName, textPos, SharpDX.Color.Yellow, 10);
            }

            // Also draw equipment slots (but only if calibration mode is OFF)
            if (!Settings.Calibration.CalibrateEquipment)
            {
                foreach (var slot in _equipmentSlots)
                {
                    var position = _equipmentPositions[slot];
                    var size = 60f;
                    var rect = new SharpDX.RectangleF(
                        position.X - size / 2,
                        position.Y - size / 2,
                        size,
                        size
                    );

                    Graphics.DrawFrame(rect, SharpDX.Color.Cyan, 2);
                
                    // Draw slot name
                    var textPos = new System.Numerics.Vector2(rect.X, rect.Y - 15);
                    Graphics.DrawText(slot.ToString(), textPos, SharpDX.Color.White, 9);
                }
            }
        }

        async public override void AreaChange(AreaInstance area)
        {
            var isInSimulacrum = !area.IsHideout;
            if (_wasInSimulacrum && !isInSimulacrum)
                SimulacrumState.RecordRun(SimulacrumState.CurrentWave, SimulacrumState.CurrentRunDuration.TotalSeconds);
            _wasInSimulacrum = isInSimulacrum;


            Core.AreaChanged();
            SimulacrumState.AreaChanged();
            Settings.ConfigureSkills();

            // Bring window to front after area change
            await Task.Delay(100);
            // Controls.BringGameWindowToFront();





        }
    }
}

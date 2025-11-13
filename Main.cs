
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

        private ISequence _scarabTraderSequence = new ScarabTraderSequence();
        private ISequence _debugSequence = new DebugSequence();
        public override bool Initialise()
        {
            this.Name = "Auto POE";

            Core.Initialize(GameController, Settings, Graphics, this);


            _sequence = new SimulacrumSequence();

            Settings.ConfigureSkills();
            return base.Initialise();
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
                RenderEquipmentSlots();
            }
        }

        private void RenderEquipmentSlots()
        {
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

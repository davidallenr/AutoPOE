
using AutoPOE.Logic;
using AutoPOE.Logic.Sequences;
using AutoPOE.Logic.Sequences;
using AutoPOE.UI;
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

        // UI managers
        private EquipmentCalibrationManager _calibrationManager;
        private DebugRenderer _debugRenderer;
        private SimulacrumStatsRenderer _simulacrumStatsRenderer;

        private ISequence _scarabTraderSequence = new ScarabTraderSequence();
        private ISequence _debugSequence = new DebugSequence();
        public override bool Initialise()
        {
            this.Name = "Auto POE";

            Core.Initialize(GameController, Settings, Graphics, this);

            _sequence = new SimulacrumSequence();

            // Initialize equipment calibration manager
            _calibrationManager = new EquipmentCalibrationManager(
                GameController,
                Graphics,
                Settings,
                ConfigDirectory,
                LogMessage,
                LogError
            );
            _calibrationManager.Initialize();

            // Sync calibration positions to Core
            Core.EquipmentSlotPositions = _calibrationManager.EquipmentPositions
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Initialize debug renderer
            _debugRenderer = new DebugRenderer(
                GameController,
                Graphics,
                Settings,
                _calibrationManager
            );

            // Initialize Simulacrum stats renderer
            _simulacrumStatsRenderer = new SimulacrumStatsRenderer(
                GameController,
                Graphics
            );

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

            // Simulacrum stats display
            if (Core.Settings.FarmMethod == "Simulacrum")
            {
                _simulacrumStatsRenderer.RenderStats(drawPos);
                drawPos.Y += 100; // Adjust position for debug info below
            }

            // Debug Mode Display
            if (Settings.Debug.EnableDebugMode)
            {
                drawPos.Y += 40;
                _debugRenderer.RenderDebugInfo(drawPos);
            }

            // Calibration Mode
            if (Settings.Calibration.CalibrateEquipment)
            {
                _calibrationManager.Update();
                _calibrationManager.RenderCalibrationMode();
                
                // Sync calibration positions to Core
                Core.EquipmentSlotPositions = _calibrationManager.EquipmentPositions
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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

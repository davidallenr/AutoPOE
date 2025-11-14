
using AutoPOE.Logic;
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

        // Managers
        public SequenceManager _sequenceManager; // Made public for debug access
        private EquipmentCalibrationManager _calibrationManager;
        private DebugRenderer _debugRenderer;
        private SimulacrumStatsRenderer _simulacrumStatsRenderer;

        public override bool Initialise()
        {
            this.Name = "Auto POE";

            Core.Initialize(GameController, Settings, Graphics, this);

            // Initialize sequence manager
            _sequenceManager = new SequenceManager();

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

            // Initialize persistent Simulacrum statistics
            SimulacrumState.InitializeStatsPersistence(ConfigDirectory, LogMessage, LogError);

            // Set up button callbacks
            Settings.Debug.ForceApplyIncubators.OnPressed = () => _ = ApplyIncubatorsManually();

            Settings.ConfigureSkills();
            return base.Initialise();
        }

        public override Job Tick()
        {
            if (!Core.IsBotRunning || !Settings.Enable || !GameController.InGame)
                return base.Tick();

            if (GameController.IsLoading)
                return base.Tick();

            _sequenceManager.Tick(Core.Settings.FarmMethod);

            return base.Tick();
        }

        public override void Render()
        {
            if (!Settings.Enable || !GameController.InGame || GameController.IngameState.Data == null)
                return;

            if (Settings.StartBot.PressedOnce())
            {
                _sequenceManager.ResetScarabTraderSequence();
                Core.IsBotRunning = !Core.IsBotRunning;
            }

            // Update incubator status (needed for debug display even when bot isn't running)
            SimulacrumState.UpdateIncubatorStatus();

            _sequenceManager.Render(Core.Settings.FarmMethod);

            var drawPos = new System.Numerics.Vector2(450, 100);

            // Simulacrum stats display
            if (Core.Settings.FarmMethod == "Simulacrum")
            {
                _simulacrumStatsRenderer.RenderStats(drawPos);
                drawPos.X -= 400; // Adjust position for debug info below
            }

            // Debug Mode Display
            float debugEndY = drawPos.Y;
            if (Settings.Debug.EnableDebugMode)
            {
                // drawPos.Y += 280;
                debugEndY = _debugRenderer.RenderDebugInfo(drawPos, _sequenceManager);
            }

            // Calibration Mode
            if (Settings.Calibration.CalibrateEquipment)
            {
                _calibrationManager.Update();
                _calibrationManager.RenderCalibrationMode(debugEndY);

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

        /// <summary>
        /// Manually applies incubators from stash to equipment.
        /// Triggered by the "Force Apply Incubators" button in debug settings.
        /// </summary>
        private async Task ApplyIncubatorsManually()
        {
            try
            {
                LogMessage("Manual incubator application started...");

                // Validate prerequisites
                if (!GameController.IngameState.IngameUi.StashElement.IsVisible)
                {
                    LogError("Stash must be open to apply incubators");
                    return;
                }

                if (!GameController.IngameState.IngameUi.InventoryPanel.IsVisible)
                {
                    LogError("Inventory must be open to apply incubators");
                    return;
                }

                if (!Core.HasIncubators)
                {
                    LogMessage("No incubators found in stash or inventory");
                    return;
                }

                // Use the StoreItemsAction logic via reflection
                var storeAction = new Logic.Actions.StoreItemsAction();

                int applied = 0;
                const int maxAttempts = 20;

                for (int i = 0; i < maxAttempts; i++)
                {
                    var applyMethod = storeAction.GetType()
                        .GetMethod("ApplyAnyIncubator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (applyMethod != null)
                    {
                        var task = (Task<bool>)applyMethod.Invoke(storeAction, null);
                        var success = await task;

                        if (success)
                        {
                            applied++;
                            LogMessage($"Applied incubator {applied}");
                            await Task.Delay(400);
                        }
                        else
                        {
                            LogMessage($"No more incubators to apply (applied {applied} total)");
                            break;
                        }
                    }
                    else
                    {
                        LogError("Failed to access ApplyAnyIncubator method");
                        break;
                    }
                }

                if (applied > 0)
                {
                    LogMessage($"Manual incubator application complete: {applied} incubators applied");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error during manual incubator application: {ex.Message}");
            }
        }
    }
}

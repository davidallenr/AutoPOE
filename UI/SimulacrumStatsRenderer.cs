using AutoPOE.Logic;
using ExileCore;
using System;
using System.Numerics;

namespace AutoPOE.UI
{
    /// <summary>
    /// Handles rendering of Simulacrum run statistics and progress information.
    /// Follows Single Responsibility Principle by managing only Simulacrum stats display.
    /// </summary>
    public class SimulacrumStatsRenderer
    {
        private readonly GameController _gameController;
        private readonly ExileCore.Graphics _graphics;

        public SimulacrumStatsRenderer(
            GameController gameController,
            ExileCore.Graphics graphics)
        {
            _gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
            _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        }

        /// <summary>
        /// Renders Simulacrum statistics including current wave, duration, total runs, and average time.
        /// </summary>
        /// <param name="startPos">Starting screen position for the stats display</param>
        public void RenderStats(Vector2 startPos)
        {
            var drawPos = startPos;

            // Show current wave and duration when in Simulacrum (not hideout)
            if (!_gameController.Area.CurrentArea.IsHideout)
            {
                _graphics.DrawText($"Current Wave: {SimulacrumState.CurrentWave} / 15", drawPos, SharpDX.Color.White);
                drawPos.Y += 20;
                _graphics.DrawText($"Current Duration: {SimulacrumState.CurrentRunDuration:mm\\:ss}", drawPos, SharpDX.Color.White);
                drawPos.Y += 20;
            }

            // Always show total runs and average time
            _graphics.DrawText($"Total Runs: {SimulacrumState.TotalRunsCompleted}", drawPos, SharpDX.Color.White);
            drawPos.Y += 20;
            _graphics.DrawText($"Avg. Time: {TimeSpan.FromSeconds(SimulacrumState.AverageTimePerRun):mm\\:ss}", drawPos, SharpDX.Color.White);
        }
    }
}

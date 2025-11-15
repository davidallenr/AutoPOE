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
        /// Renders Simulacrum statistics including current wave, duration, total runs, and enhanced persistent stats.
        /// </summary>
        /// <param name="startPos">Starting screen position for the stats display</param>
        public void RenderStats(Vector2 startPos)
        {
            var drawPos = startPos;

            // Display simple legacy statistics
            var stats = SimulacrumState.GetLegacyStats();

            _graphics.DrawText($"=== SIMULACRUM STATS ===", drawPos, SharpDX.Color.Yellow);
            drawPos.Y += 20;

            _graphics.DrawText($"Current Wave: {SimulacrumState.CurrentWave} / 15", drawPos, SharpDX.Color.White);
            drawPos.Y += 20;

            _graphics.DrawText($"Current Duration: {SimulacrumState.CurrentRunDuration:mm\\:ss}", drawPos, SharpDX.Color.White);
            drawPos.Y += 20;

            _graphics.DrawText($"Total Runs: {stats.TotalRuns}", drawPos, SharpDX.Color.White);
            drawPos.Y += 20;

            if (stats.AverageDuration > 0)
            {
                _graphics.DrawText($"Average Run: {TimeSpan.FromSeconds(stats.AverageDuration):mm\\:ss}", drawPos, SharpDX.Color.White);
                drawPos.Y += 20;
            }
        }
    }
}

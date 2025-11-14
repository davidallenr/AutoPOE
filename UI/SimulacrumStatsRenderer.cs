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

            // Show current wave and duration when in Simulacrum (not hideout)
            if (!_gameController.Area.CurrentArea.IsHideout)
            {
                _graphics.DrawText($"Current Wave: {SimulacrumState.CurrentWave} / 15", drawPos, SharpDX.Color.White);
                drawPos.Y += 20;
                _graphics.DrawText($"Current Duration: {SimulacrumState.CurrentRunDuration:mm\\:ss}", drawPos, SharpDX.Color.White);
                drawPos.Y += 20;
                drawPos.Y += 10; // Add spacing
            }

            // Get enhanced statistics (includes persistent data)
            var stats = SimulacrumState.GetEnhancedStats();

            // Core statistics (always shown)
            _graphics.DrawText($"=== SIMULACRUM STATS ===", drawPos, SharpDX.Color.Yellow);
            drawPos.Y += 20;

            _graphics.DrawText($"Total Runs: {stats.TotalRuns}", drawPos, SharpDX.Color.White);
            drawPos.Y += 20;

            if (stats.OverallAverageTime > 0)
            {
                _graphics.DrawText($"Overall Avg: {TimeSpan.FromSeconds(stats.OverallAverageTime):mm\\:ss}", drawPos, SharpDX.Color.White);
                drawPos.Y += 20;
            }

            // Recent performance (if available)
            if (stats.Last10Average > 0)
            {
                var recentColor = stats.Last10Average < stats.OverallAverageTime ? SharpDX.Color.LimeGreen : SharpDX.Color.Orange;
                _graphics.DrawText($"Last 10 Avg: {TimeSpan.FromSeconds(stats.Last10Average):mm\\:ss}", drawPos, recentColor);
                drawPos.Y += 20;
            }

            // Daily statistics
            if (stats.RunsToday > 0)
            {
                _graphics.DrawText($"Today: {stats.RunsToday} runs", drawPos, SharpDX.Color.Cyan);
                drawPos.Y += 20;

                if (stats.AverageTimeToday > 0)
                {
                    _graphics.DrawText($"Today Avg: {TimeSpan.FromSeconds(stats.AverageTimeToday):mm\\:ss}", drawPos, SharpDX.Color.Cyan);
                    drawPos.Y += 20;
                }
            }

            // Best/Worst times (if available)
            if (stats.BestTime > 0)
            {
                _graphics.DrawText($"Best: {TimeSpan.FromSeconds(stats.BestTime):mm\\:ss}", drawPos, SharpDX.Color.LimeGreen);
                drawPos.Y += 20;
            }

            // Success rate
            if (stats.TotalRuns > 0)
            {
                var successColor = stats.SuccessRate >= 95 ? SharpDX.Color.LimeGreen :
                                  stats.SuccessRate >= 80 ? SharpDX.Color.Yellow : SharpDX.Color.Orange;
                _graphics.DrawText($"Success Rate: {stats.SuccessRate:F1}%", drawPos, successColor);
                drawPos.Y += 20;
            }

            // Total time spent (if significant)
            if (stats.TotalTimeSpent > 3600) // More than 1 hour
            {
                var totalTime = TimeSpan.FromSeconds(stats.TotalTimeSpent);
                _graphics.DrawText($"Total Time: {totalTime.Hours}h {totalTime.Minutes}m", drawPos, SharpDX.Color.Gray);
                drawPos.Y += 20;
            }
        }
    }
}

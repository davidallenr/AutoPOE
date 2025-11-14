using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AutoPOE.Logic
{
    /// <summary>
    /// Handles persistence of Simulacrum run statistics across bot sessions.
    /// Maintains long-term performance tracking and analytics.
    /// </summary>
    public class SimulacrumStatsPersistence
    {
        private readonly string _configDirectory;
        private readonly Action<string> _logMessage;
        private readonly Action<string> _logError;

        private SimulacrumStatsData _statsData;
        private readonly string _statsFilePath;

        public SimulacrumStatsPersistence(string configDirectory, Action<string> logMessage, Action<string> logError)
        {
            _configDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
            _logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
            _logError = logError ?? throw new ArgumentNullException(nameof(logError));
            _statsFilePath = Path.Combine(_configDirectory, "simulacrum_stats.json");
            _statsData = new SimulacrumStatsData();
        }

        /// <summary>
        /// Loads statistics from disk on startup
        /// </summary>
        public void LoadStats()
        {
            try
            {
                if (File.Exists(_statsFilePath))
                {
                    var json = File.ReadAllText(_statsFilePath);
                    var loadedData = System.Text.Json.JsonSerializer.Deserialize<SimulacrumStatsData>(json);

                    if (loadedData != null)
                    {
                        _statsData = loadedData;
                        _logMessage($"Loaded Simulacrum stats: {_statsData.TotalRuns} total runs, avg {_statsData.GetOverallAverageTime():F1}s");
                    }
                }
                else
                {
                    _logMessage("No existing Simulacrum stats file found, starting fresh");
                }
            }
            catch (Exception ex)
            {
                _logError($"Failed to load Simulacrum stats: {ex.Message}");
                _statsData = new SimulacrumStatsData(); // Reset to defaults on error
            }
        }

        /// <summary>
        /// Records a completed run and saves to disk
        /// </summary>
        public void RecordCompletedRun(double durationSeconds, int wavesCompleted = 15)
        {
            try
            {
                var runData = new SimulacrumRunData
                {
                    Timestamp = DateTime.Now,
                    DurationSeconds = durationSeconds,
                    WavesCompleted = wavesCompleted,
                    SuccessfulCompletion = wavesCompleted >= 15
                };

                _statsData.Runs.Add(runData);
                _statsData.TotalRuns++;
                _statsData.LastUpdated = DateTime.Now;

                // Keep only last 1000 runs to prevent file from growing too large
                if (_statsData.Runs.Count > 1000)
                {
                    _statsData.Runs.RemoveRange(0, _statsData.Runs.Count - 1000);
                }

                SaveStats();
                _logMessage($"Recorded Simulacrum run: {durationSeconds:F1}s, {wavesCompleted} waves");
            }
            catch (Exception ex)
            {
                _logError($"Failed to record Simulacrum run: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves current statistics to disk
        /// </summary>
        private void SaveStats()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_statsData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_statsFilePath, json);
            }
            catch (Exception ex)
            {
                _logError($"Failed to save Simulacrum stats: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets comprehensive statistics for display
        /// </summary>
        public SimulacrumStatsDisplay GetDisplayStats()
        {
            return new SimulacrumStatsDisplay
            {
                TotalRuns = _statsData.TotalRuns,
                OverallAverageTime = _statsData.GetOverallAverageTime(),
                Last10Average = _statsData.GetRecentAverageTime(10),
                Last50Average = _statsData.GetRecentAverageTime(50),
                BestTime = _statsData.GetBestTime(),
                WorstTime = _statsData.GetWorstTime(),
                SuccessRate = _statsData.GetSuccessRate(),
                TotalTimeSpent = _statsData.GetTotalTimeSpent(),
                RunsToday = _statsData.GetRunsToday(),
                AverageTimeToday = _statsData.GetAverageTodayTime()
            };
        }

        /// <summary>
        /// Resets all statistics (with confirmation)
        /// </summary>
        public void ResetStats()
        {
            _statsData = new SimulacrumStatsData();
            SaveStats();
            _logMessage("Simulacrum statistics have been reset");
        }
    }

    /// <summary>
    /// Container for all persistent Simulacrum statistics data
    /// </summary>
    public class SimulacrumStatsData
    {
        public List<SimulacrumRunData> Runs { get; set; } = new List<SimulacrumRunData>();
        public int TotalRuns { get; set; } = 0;
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public double GetOverallAverageTime()
        {
            var successfulRuns = Runs.Where(r => r.SuccessfulCompletion).ToList();
            return successfulRuns.Any() ? successfulRuns.Average(r => r.DurationSeconds) : 0;
        }

        public double GetRecentAverageTime(int runCount)
        {
            var recentRuns = Runs.Where(r => r.SuccessfulCompletion)
                                 .OrderByDescending(r => r.Timestamp)
                                 .Take(runCount)
                                 .ToList();
            return recentRuns.Any() ? recentRuns.Average(r => r.DurationSeconds) : 0;
        }

        public double GetBestTime()
        {
            var successfulRuns = Runs.Where(r => r.SuccessfulCompletion).ToList();
            return successfulRuns.Any() ? successfulRuns.Min(r => r.DurationSeconds) : 0;
        }

        public double GetWorstTime()
        {
            var successfulRuns = Runs.Where(r => r.SuccessfulCompletion).ToList();
            return successfulRuns.Any() ? successfulRuns.Max(r => r.DurationSeconds) : 0;
        }

        public double GetSuccessRate()
        {
            if (Runs.Count == 0) return 0;
            return (double)Runs.Count(r => r.SuccessfulCompletion) / Runs.Count * 100;
        }

        public double GetTotalTimeSpent()
        {
            return Runs.Sum(r => r.DurationSeconds);
        }

        public int GetRunsToday()
        {
            var today = DateTime.Today;
            return Runs.Count(r => r.Timestamp.Date == today);
        }

        public double GetAverageTodayTime()
        {
            var today = DateTime.Today;
            var todayRuns = Runs.Where(r => r.Timestamp.Date == today && r.SuccessfulCompletion).ToList();
            return todayRuns.Any() ? todayRuns.Average(r => r.DurationSeconds) : 0;
        }
    }

    /// <summary>
    /// Data for a single Simulacrum run
    /// </summary>
    public class SimulacrumRunData
    {
        public DateTime Timestamp { get; set; }
        public double DurationSeconds { get; set; }
        public int WavesCompleted { get; set; }
        public bool SuccessfulCompletion { get; set; }
    }

    /// <summary>
    /// Display-ready statistics for UI rendering
    /// </summary>
    public class SimulacrumStatsDisplay
    {
        public int TotalRuns { get; set; }
        public double OverallAverageTime { get; set; }
        public double Last10Average { get; set; }
        public double Last50Average { get; set; }
        public double BestTime { get; set; }
        public double WorstTime { get; set; }
        public double SuccessRate { get; set; }
        public double TotalTimeSpent { get; set; }
        public int RunsToday { get; set; }
        public double AverageTimeToday { get; set; }
    }
}
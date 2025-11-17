using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using AutoPOE.Navigation;
using ExileCore.Shared.Helpers;
using AutoPOE.Logic;
using ExileCore;
using ExileCore.Shared.Enums;

namespace AutoPOE.Logic.Actions
{
    public class ExploreAction : IAction
    {
        private List<Vector2> _blacklistedChunks = new List<Vector2>();
        private List<Vector2> _areaWaypoints = new List<Vector2>();
        private int _currentWaypointIndex = 0;

        // Failsafe fields
        private int _consecutiveFailures = 0;
        private const int MAX_CONSECUTIVE_FAILURES = 100; // Give up after 100 ticks of finding no valid path

        public ExploreAction()
        {
            Core.Map.ResetAllChunks();
            _blacklistedChunks.Clear();

            // Load waypoints for current area
            var currentArea = Core.GameController.Area.CurrentArea.Name;
            _areaWaypoints = GameConstants.SimulacrumAreas.GetAreaWaypoints(currentArea);

            // Start by pathing to simulacrum center
            _currentPath = Core.Map.FindPath(Core.GameController.Player.GridPosNum, Core.Map.GetSimulacrumCenter());
        }

        private Random _random = new Random();
        private Navigation.Path? _currentPath;

        /// <summary>
        /// Gets the current path for debugging
        /// </summary>
        public Navigation.Path? CurrentPath => _currentPath;

        /// <summary>
        /// Gets the count of blacklisted chunks for debugging
        /// </summary>
        public int BlacklistedChunkCount => _blacklistedChunks.Count;

        /// <summary>
        /// Gets the consecutive failures count for debugging
        /// </summary>
        public int ConsecutiveFailures => _consecutiveFailures;

        /// <summary>
        /// Increments consecutive failures and checks if max threshold is reached.
        /// </summary>
        /// <returns>True if max failures reached, false otherwise</returns>
        private bool IncrementAndCheckMaxFailures()
        {
            _consecutiveFailures++;
            return _consecutiveFailures > MAX_CONSECUTIVE_FAILURES;
        }

        /// <summary>
        /// Resets the consecutive failures counter.
        /// </summary>
        private void ResetFailureCounter()
        {
            _consecutiveFailures = 0;
        }

        public async Task<ActionResultType> Tick()
        {
            Core.Map.UpdateRevealedChunks();
            var playerPos = Core.GameController.Player.GridPosNum;

            // Handle stuck situations with movement skill
            if (DateTime.Now > SimulacrumState.LastMovedAt.AddSeconds(2))
            {
                var nextPos = Core.GameController.Player.GridPosNum + new Vector2(_random.Next(-50, 50), _random.Next(-50, 50));
                await Controls.UseKeyAtGridPos(nextPos, Core.Settings.GetNextMovementSkill());
                return ActionResultType.Running;
            }

            if (_currentPath != null && !_currentPath.IsFinished)
            {
                await _currentPath.FollowPath();
                // Don't reset failure counter here - we only want to reset when we successfully
                // path to a new unrevealed chunk, not just while wandering around
                return ActionResultType.Running;
            }

            // Path is finished or null, find a new target chunk
            var nextChunk = Core.Map.GetNextUnrevealedChunk();
            if (nextChunk == null)
            {
                // No more chunks - try waypoints if available
                if (_areaWaypoints.Count > 0 && _currentWaypointIndex < _areaWaypoints.Count)
                {
                    var waypoint = _areaWaypoints[_currentWaypointIndex];
                    var distanceToWaypoint = playerPos.Distance(waypoint);

                    // If we're close to current waypoint, move to next one
                    if (distanceToWaypoint < 50)
                    {
                        _currentWaypointIndex++;
                        if (_currentWaypointIndex >= _areaWaypoints.Count)
                        {
                            return ActionResultType.Success; // All waypoints visited
                        }
                        waypoint = _areaWaypoints[_currentWaypointIndex];
                    }

                    // Path to waypoint
                    _currentPath = Core.Map.FindPath(playerPos, waypoint);
                    if (_currentPath != null)
                    {
                        return ActionResultType.Running;
                    }
                    else
                    {
                        // Can't path to waypoint, skip to next
                        _currentWaypointIndex++;
                        return ActionResultType.Running;
                    }
                }

                return ActionResultType.Success; // All exploration complete
            }

            // Check if chunk is blacklisted and skip if it is.
            if (_blacklistedChunks.Contains(nextChunk.Position))
            {
                if (IncrementAndCheckMaxFailures())
                {
                    _blacklistedChunks.Clear(); // Clear for next run
                    return ActionResultType.Success; // Got stuck
                }
                return ActionResultType.Running; // Go next tick and skip the chunk 
            }

            _currentPath = Core.Map.FindPath(Core.GameController.Player.GridPosNum, nextChunk.Position);

            if (_currentPath == null)
            {
                // Couldn't find good path, blacklist this chunk
                _blacklistedChunks.Add(nextChunk.Position);
                _consecutiveFailures++;

                // If we're struggling (50+ failures), try going back to center
                if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES / 2)
                {
                    var center = Core.Map.GetSimulacrumCenter();
                    if (center != Vector2.Zero && playerPos.Distance(center) > 100)
                    {
                        _currentPath = Core.Map.FindPath(playerPos, center);
                        if (_currentPath != null)
                        {
                            // Don't reset counter - we're trying to recover, not making progress yet
                            return ActionResultType.Running;
                        }
                    }
                }
            }
            else
            {
                // Successfully found path to a new unrevealed chunk - reset counter
                _consecutiveFailures = 0;
            }

            return ActionResultType.Running;
        }

        public void Render()
        {
            _currentPath?.Render();
        }
    }
}
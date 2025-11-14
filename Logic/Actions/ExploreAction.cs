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

        // Failsafe fields
        private int _consecutiveFailures = 0;
        private const int MAX_CONSECUTIVE_FAILURES = 100; // Give up after 100 ticks of finding no valid path

        public ExploreAction()
        {
            Core.Map.ResetAllChunks();
            _blacklistedChunks.Clear();

            // Only try to path to simulacrum center if it's a valid position
            var simulacrumCenter = Core.Map.GetSimulacrumCenter();
            if (simulacrumCenter != Vector2.Zero)
            {
                _currentPath = Core.Map.FindPath(Core.GameController.Player.GridPosNum, simulacrumCenter);
                // Note: _currentPath might still be null if pathfinding fails - that's handled in Tick()
            }
            // If no valid center, _currentPath will remain null and Tick() will handle finding chunks
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

        public async Task<ActionResultType> Tick()
        {
            Core.Map.UpdateRevealedChunks();
            var playerPos = Core.GameController.Player.GridPosNum;

            if (DateTime.Now > SimulacrumState.LastMovedAt.AddSeconds(2))
            {
                var nextPos = Core.GameController.Player.GridPosNum + new Vector2(_random.Next(-50, 50), _random.Next(-50, 50));
                await Controls.UseKeyAtGridPos(nextPos, Core.Settings.GetNextMovementSkill());
                return ActionResultType.Running;
            }

            if (_currentPath != null && !_currentPath.IsFinished)
            {
                // Validate that the path is still reasonable before following it
                var pathTarget = _currentPath.Next;
                if (pathTarget.HasValue && pathTarget.Value != Vector2.Zero && pathTarget.Value.Distance(playerPos) > 1000)
                {
                    // Path target is too far away, probably invalid - abandon this path
                    _currentPath = null;
                }
                else
                {
                    await _currentPath.FollowPath();
                    _consecutiveFailures = 0; // We are on a valid path, reset counter
                    return ActionResultType.Running;
                }
            }

            // Path is finished or null, find a new target chunk
            var nextChunk = Core.Map.GetNextUnrevealedChunk();
            if (nextChunk == null)
            {
                // No more chunks to explore, try to return to simulacrum center
                var simulacrumCenter = Core.Map.GetSimulacrumCenter();
                if (simulacrumCenter != Vector2.Zero && playerPos.Distance(simulacrumCenter) > 50)
                {
                    _currentPath = Core.Map.FindPath(playerPos, simulacrumCenter);
                    if (_currentPath != null)
                    {
                        return ActionResultType.Running;
                    }
                }
                return ActionResultType.Success; // All exploration complete
            }

            // Check if chunk is blacklisted and skip if it is.
            if (_blacklistedChunks.Contains(nextChunk.Position))
            {
                _consecutiveFailures++;
                if (_consecutiveFailures > MAX_CONSECUTIVE_FAILURES)
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

                // If we have too many consecutive failures, try a fallback approach
                if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES / 2)
                {
                    // Try to move towards simulacrum center instead of exploring
                    var simulacrumCenter = Core.Map.GetSimulacrumCenter();
                    if (simulacrumCenter != Vector2.Zero && playerPos.Distance(simulacrumCenter) > 100)
                    {
                        _currentPath = Core.Map.FindPath(playerPos, simulacrumCenter);
                        if (_currentPath != null)
                        {
                            _consecutiveFailures = 0; // Reset since we found a valid path
                        }
                    }
                }
            }
            else
            {
                // We found a new valid path
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
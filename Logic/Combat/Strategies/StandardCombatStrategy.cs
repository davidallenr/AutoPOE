using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System.Numerics;

namespace AutoPOE.Logic.Combat.Strategies
{
    /// <summary>
    /// Standard combat strategy - targets highest rarity monsters within range
    /// </summary>
    public class StandardCombatStrategy : ICombatStrategy
    {
        public string Name => "Standard";
        public bool ShouldKite => false;
        public int KiteDistance => 0;
        public string LastTargetReason { get; private set; } = "No target selected yet";

        public Task<Vector2?> SelectTarget(List<ExileCore.PoEMemory.MemoryObjects.Entity> monsters)
        {
            // Filter to valid hostile monsters within combat distance
            var validMonsters = monsters
                .Where(m => m.IsHostile 
                    && m.IsTargetable 
                    && m.IsAlive 
                    && m.GridPosNum.Distance(Core.GameController.Player.GridPosNum) < Core.Settings.CombatDistance)
                .ToList();

            if (!validMonsters.Any())
            {
                LastTargetReason = "No valid monsters in range";
                return Task.FromResult<Vector2?>(null);
            }

            // Select highest rarity monster (Unique > Rare > Magic > White)
            var bestTarget = validMonsters
                .OrderByDescending(m => Navigation.Map.GetMonsterRarityWeight(m.Rarity))
                .ThenBy(m => m.GridPosNum.Distance(Core.GameController.Player.GridPosNum)) // Prefer closer if same rarity
                .FirstOrDefault();

            if (bestTarget == null)
            {
                LastTargetReason = "No valid target found";
                return Task.FromResult<Vector2?>(null);
            }

            // Set reason for debugging
            var distance = bestTarget.GridPosNum.Distance(Core.GameController.Player.GridPosNum);
            LastTargetReason = $"{bestTarget.Rarity} monster at {distance:F1} units";
            
            if (!string.IsNullOrEmpty(bestTarget.RenderName))
            {
                LastTargetReason += $" ({bestTarget.RenderName})";
            }

            return Task.FromResult<Vector2?>(bestTarget.GridPosNum);
        }
    }
}

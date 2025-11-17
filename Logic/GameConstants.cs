using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoPOE.Logic
{
    /// <summary>
    /// Contains all game-specific constants to avoid magic strings throughout the codebase
    /// </summary>
    public static class GameConstants
    {
        /// <summary>
        /// Known Simulacrum boss names as they appear in RenderName
        /// </summary>
        public static class SimulacrumBosses
        {
            public const string Kosis = "Kosis";
            public const string Omniphobia = "Omniphobia";
            public const string DeliriumBoss = "Delirium Boss";

            private static readonly HashSet<string> _bossNames = new HashSet<string>
            {
                Kosis,
                Omniphobia,
                DeliriumBoss
            };

            /// <summary>
            /// Checks if a name contains any known Simulacrum boss identifier
            /// </summary>
            public static bool IsBoss(string? name)
            {
                if (string.IsNullOrEmpty(name))
                    return false;

                return _bossNames.Any(boss => name.Contains(boss, StringComparison.OrdinalIgnoreCase));
            }

            /// <summary>
            /// Gets the priority value for boss targeting (higher = more priority)
            /// </summary>
            public static int GetBossPriority(string? name)
            {
                if (string.IsNullOrEmpty(name))
                    return 0;

                if (name.Contains(Kosis, StringComparison.OrdinalIgnoreCase))
                    return 2;

                if (name.Contains(Omniphobia, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(DeliriumBoss, StringComparison.OrdinalIgnoreCase))
                    return 1;

                return 0;
            }
        }

        /// <summary>
        /// Entity metadata paths used for entity identification
        /// </summary>
        public static class EntityMetadata
        {
            public const string SimulacrumMonolith = "Objects/Afflictionator";
            public const string Stash = "Metadata/MiscellaneousObjects/Stash";
            public const string MappingDevice = "MappingDevice";
            public const string SimulacrumFragment = "CurrencyAfflictionFragment";
        }

        /// <summary>
        /// Item metadata paths for identifying items
        /// </summary>
        public static class ItemMetadata
        {
            public const string Incubator = "Incubation";
        }

        /// <summary>
        /// State machine state names for the Simulacrum monolith
        /// </summary>
        public static class MonolithStates
        {
            public const string Active = "active";
            public const string Goodbye = "goodbye";
            public const string Wave = "wave";
        }

        /// <summary>
        /// Simulacrum area names (delirium maps)
        /// </summary>
        public static class SimulacrumAreas
        {
            public const string BridgeEnraptured = "The Bridge Enraptured";
            public const string OriathDelusion = "Oriath Delusion";
            public const string SyndromeEncampment = "The Syndrome Encampment";
            public const string Hysteriagate = "Hysteriagate";
            public const string LunacysWatch = "Lunacy's Watch";

            private static readonly HashSet<string> _simulacrumAreaNames = new HashSet<string>
            {
                BridgeEnraptured,
                OriathDelusion,
                SyndromeEncampment,
                Hysteriagate,
                LunacysWatch
            };

            /// <summary>
            /// Checks if the given area name is a Simulacrum area
            /// </summary>
            public static bool IsSimulacrumArea(string? areaName)
            {
                if (string.IsNullOrEmpty(areaName))
                    return false;

                return _simulacrumAreaNames.Contains(areaName);
            }

            /// <summary>
            /// Gets known waypoints (common monster spawn locations) for specific Simulacrum maps
            /// </summary>
            public static List<System.Numerics.Vector2> GetAreaWaypoints(string? areaName)
            {
                if (string.IsNullOrEmpty(areaName))
                    return new List<System.Numerics.Vector2>();

                // Use Contains for more flexible matching
                if (areaName.Contains(BridgeEnraptured, StringComparison.OrdinalIgnoreCase))
                {
                    return new List<System.Numerics.Vector2>
                    {
                        new System.Numerics.Vector2(550, 696),
                        new System.Numerics.Vector2(545, 404)
                    };
                }

                // Add more maps here as needed
                // if (areaName.Contains(OriathDelusion, StringComparison.OrdinalIgnoreCase))
                // {
                //     return new List<System.Numerics.Vector2> { ... };
                // }

                return new List<System.Numerics.Vector2>();
            }
        }

        /// <summary>
        /// Farm method identifiers
        /// </summary>
        public static class FarmMethods
        {
            public const string Simulacrum = "Simulacrum";
            public const string ScarabTrader = "ScarabTrader";
        }

        /// <summary>
        /// NPC dialog text strings
        /// </summary>
        public static class NpcDialogText
        {
            public const string SellItems = "Sell Items";
        }

        /// <summary>
        /// Buff names from the game engine
        /// </summary>
        public static class BuffNames
        {
            public const string MineManaReservation = "mine_mana_reservation";
        }
    }
}

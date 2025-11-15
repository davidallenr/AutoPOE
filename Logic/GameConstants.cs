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
    }
}

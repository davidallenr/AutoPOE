using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System;
using System.Windows.Forms;
using static AutoPOE.Settings.Skill;
using AutoPOE.Logic;

namespace AutoPOE
{
    public class Settings : ISettings
    {
        public Settings()
        {
            FarmMethod.SetListValues(Enum.GetNames(typeof(BotMode)).ToList());
        }

        private readonly Random _random = new Random();


        public ToggleNode Enable { get; set; } = new ToggleNode(false);
        public HotkeyNode StartBot { get; set; } = (HotkeyNode)Keys.Insert;

        [Menu("Farm Method", "What type of farming do you want to do?")]
        public ListNode FarmMethod { get; set; } = new ListNode() { Value = "Simulacrum" };

        [Menu("Action Frequency", "What is the minimm time between inputs?")]
        public RangeNode<int> ActionFrequency { get; set; } = new RangeNode<int>(100, 25, 500);

        [Menu("Clamp Size")]
        public RangeNode<int> ClampSize { get; set; } = new RangeNode<int>(400, 100, 1000);

        [Menu("Combat Distance", "How close to stand to monsters to fight them.")]
        public RangeNode<int> CombatDistance { get; set; } = new RangeNode<int>(15, 5, 50);


        [Menu("Pathfinding Node Size", "How close to a pathfinding node before we consider it 'complete'.")]
        public RangeNode<int> NodeSize { get; set; } = new RangeNode<int>(20, 10, 100);


        [Menu("View Distance", "How close must we be to consider objects as visible. Used to explore maps.")]
        public RangeNode<int> ViewDistance { get; set; } = new RangeNode<int>(80, 10, 500);

        [Menu("Simulacrum Wave Delay", "Minimum time (in seconds) after wave ends before starting a new one.")]
        public RangeNode<int> Simulacrum_MinimumWaveDelay { get; set; } = new RangeNode<int>(5, 1, 20);


        [Menu("Use Incubators", "Should we apply incubators (from stash) to our equipment?")]
        public ToggleNode UseIncubators { get; set; } = new ToggleNode(true);


        [Menu("Hide/Show Loot (Unstuck)", "Should the bot double press the Z key to reposition loot icons? Can help if loot icons are off screen or unable to be clicked.")]
        public ToggleNode LootItemsUnstick { get; set; } = new ToggleNode(false);

        [Menu("Store Item Threshold", "How many items before we dump to stash.")]
        public RangeNode<int> StoreItemThreshold { get; set; } = new RangeNode<int>(20, 5, 60);


        [Menu("Detonate Mines Key")]
        public HotkeyNode DetonateMinesKey { get; set; } = (HotkeyNode)Keys.D;
        public ToggleNode ShouldDetonateMines { get; set; } = new ToggleNode(false);

        public CombatSettings Combat { get; set; } = new CombatSettings();

        public Skill Skill1 { get; set; } = new Skill { Hotkey = (HotkeyNode)Keys.Q };
        public Skill Skill2 { get; set; } = new Skill { Hotkey = (HotkeyNode)Keys.W };
        public Skill Skill3 { get; set; } = new Skill { Hotkey = (HotkeyNode)Keys.E };
        public Skill Skill4 { get; set; } = new Skill { Hotkey = (HotkeyNode)Keys.R };
        public Skill Skill5 { get; set; } = new Skill { Hotkey = (HotkeyNode)Keys.T };
        public Skill Skill6 { get; set; } = new Skill { Hotkey = (HotkeyNode)Keys.F };

        public DebugSettings Debug { get; set; } = new DebugSettings();
        public CalibrationSettings Calibration { get; set; } = new CalibrationSettings();
        public ScarabTrader Trader { get; set; } = new ScarabTrader();

        /// <summary>
        /// Finds an available movement skill that is ready to be cast.
        /// </summary>
        public Keys GetNextMovementSkill()
        {
            var usableSkillNames = GetUsableSkillNames();
            var allSkills = new List<Skill> { Skill1, Skill2, Skill3, Skill4, Skill5, Skill6 };

            var validMovementSkills = allSkills
                .Where(s => s.IsMovementKey.Value && usableSkillNames.Contains(s.SkillName.Value) && DateTime.Now > s.NextCast)
                .ToList();

            if (!validMovementSkills.Any()) return Keys.E;

            var selected = validMovementSkills[_random.Next(validMovementSkills.Count)];
            selected.NextCast = DateTime.Now.AddMilliseconds(selected.MinimumDelay.Value);
            return selected.Hotkey.Value;
        }

        /// <summary>
        /// Gets skills available for a specific role, sorted by strategy priority.
        /// </summary>
        public List<Skill> GetSkillsByRole(Skill.SkillRoleSort role)
        {
            var usableSkillNames = GetUsableSkillNames();
            var allSkills = new List<Skill> { Skill1, Skill2, Skill3, Skill4, Skill5, Skill6 };

            return allSkills
                .Where(s => s.SkillRole.Value == role.ToString() &&
                           usableSkillNames.Contains(s.SkillName.Value) &&
                           DateTime.Now > s.NextCast &&
                           (string.IsNullOrEmpty(s.Buff.Value) || !HasBuff(s.Buff.Value)))
                .OrderByDescending(s => s.StrategyPriority.Value)
                .ThenBy(s => s.MinimumDelay.Value)
                .ToList();
        }

        /// <summary>
        /// Gets the best skill for a target based on strategy and target priority.
        /// </summary>
        public Skill? GetBestSkillForTarget(Skill.TargetPrioritySort targetPriority, Skill.SkillRoleSort? preferredRole = null)
        {
            var usableSkillNames = GetUsableSkillNames();
            var allSkills = new List<Skill> { Skill1, Skill2, Skill3, Skill4, Skill5, Skill6 };

            var validSkills = allSkills
                .Where(s => s.TargetPriority.Value == targetPriority.ToString() &&
                           usableSkillNames.Contains(s.SkillName.Value) &&
                           DateTime.Now > s.NextCast &&
                           s.CastType.Value != Skill.CastTypeSort.DoNotUse.ToString() &&
                           (string.IsNullOrEmpty(s.Buff.Value) || !HasBuff(s.Buff.Value)))
                .ToList();

            // Filter by preferred role if specified
            if (preferredRole.HasValue)
            {
                var roleSkills = validSkills.Where(s => s.SkillRole.Value == preferredRole.Value.ToString()).ToList();
                if (roleSkills.Any())
                    validSkills = roleSkills;
            }

            var bestSkill = validSkills
                .OrderByDescending(s => s.StrategyPriority.Value)
                .ThenBy(s => s.MinimumDelay.Value)
                .FirstOrDefault();

            if (bestSkill != null)
            {
                bestSkill.NextCast = DateTime.Now.AddMilliseconds(bestSkill.MinimumDelay.Value);
            }

            return bestSkill;
        }

        /// <summary>
        /// Checks if player has a specific buff.
        /// </summary>
        private bool HasBuff(string buffName)
        {
            return Core.GameController.Player.Buffs.Any(buff => buff.Name == buffName);
        }

        /// <summary>
        /// Gets all available skills sorted by strategy priority.
        /// </summary>
        public List<Skill> GetAvailableSkillsByPriority()
        {
            var usableSkillNames = GetUsableSkillNames();
            var allSkills = new List<Skill> { Skill1, Skill2, Skill3, Skill4, Skill5, Skill6 };

            return allSkills
                .Where(s => usableSkillNames.Contains(s.SkillName.Value) &&
                           DateTime.Now > s.NextCast &&
                           s.CastType.Value != Skill.CastTypeSort.DoNotUse.ToString() &&
                           (string.IsNullOrEmpty(s.Buff.Value) || !HasBuff(s.Buff.Value)))
                .OrderByDescending(s => s.StrategyPriority.Value)
                .ThenBy(s => s.MinimumDelay.Value)
                .ToList();
        }

        /// <summary>
        /// Finds an available combat skill for a specific target type.
        /// </summary>
        public Skill? GetNextCombatSkill(Skill.CastTypeSort targetType)
        {
            var usableSkillNames = GetUsableSkillNames();
            var allSkills = new List<Skill> { Skill1, Skill2, Skill3, Skill4, Skill5, Skill6 };

            var validCombatSkills = allSkills
                .Where(s => s.CastType.Value == targetType.ToString() &&
                        usableSkillNames.Contains(s.SkillName.Value) &&
                        DateTime.Now > s.NextCast &&
                        (string.IsNullOrEmpty(s.Buff.Value) || Core.GameController.Player.Buffs.FirstOrDefault(buff => buff.Name == s.Buff.Value) == null))
                .ToList();

            if (!validCombatSkills.Any()) return null;

            var selected = validCombatSkills[_random.Next(validCombatSkills.Count)];
            selected.NextCast = DateTime.Now.AddMilliseconds(selected.MinimumDelay.Value);
            return selected;
        }

        private HashSet<string> GetUsableSkillNames()
        {
            return Core.GameController.Player.GetComponent<Actor>()?.ActorSkills
                .Where(s => s.CanBeUsed && !string.IsNullOrEmpty(s.Name))
                .Select(s => s.Name)
                .ToHashSet() ?? new HashSet<string>();
        }
        /// <summary>
        /// Set all skill slots to the proper spell name. Lets us look it up later easily. 
        /// </summary>
        public void ConfigureSkills()
        {
            var skillOptions = Core.GameController.Player.GetComponent<Actor>()?.ActorSkills?
                .Where(I => I.Name.Trim().Length > 1 && !I.Name.StartsWith("?") && I.IsOnSkillBar)
                .Select(I => I.Name)
                .Distinct()
                .ToList();

            Skill1.SkillName.SetListValues(skillOptions);
            Skill2.SkillName.SetListValues(skillOptions);
            Skill3.SkillName.SetListValues(skillOptions);
            Skill4.SkillName.SetListValues(skillOptions);
            Skill5.SkillName.SetListValues(skillOptions);
            Skill6.SkillName.SetListValues(skillOptions);

            var allSkills = new List<Skill> { Skill1, Skill2, Skill3, Skill4, Skill5, Skill6 };

            foreach (var skill in allSkills.Where(I => I.SkillName == "Move"))
                skill.MinimumDelay.Value = 100;


            foreach (var skill in allSkills.Where(I => I.SkillName == "Righteous Fire"))
                skill.Buff.Value = "righteous_fire";
        }


        [Submenu(CollapsedByDefault = true)]
        public class Skill
        {
            public Skill()
            {
                CastType.SetListValues([.. Enum.GetNames(typeof(CastTypeSort))]);
                SkillRole.SetListValues([.. Enum.GetNames(typeof(SkillRoleSort))]);
                TargetPriority.SetListValues([.. Enum.GetNames(typeof(TargetPrioritySort))]);
            }

            [Menu("Hotkey", "Physical key that the skill is bound to.")]
            public HotkeyNode Hotkey { get; set; } = (HotkeyNode)Keys.Q;

            [Menu("Skill Name", "Name of skill.")]
            public ListNode SkillName { get; set; } = new ListNode() { Value = "None" };

            [Menu("Skill Role", "PrimaryDamage: Main attack skill. AreaDamage: For groups. SingleTarget: For rares/bosses. Defensive: Guard skills. Buff: Auras/buffs.")]
            public ListNode SkillRole { get; set; } = new ListNode() { Value = SkillRoleSort.PrimaryDamage.ToString() };

            [Menu("Target Priority", "HighestThreat: Rare/Unique first. ClosestEnemy: Nearest target. RareFirst: Only rare monsters. MostEnemies: Center of groups.")]
            public ListNode TargetPriority { get; set; } = new ListNode() { Value = TargetPrioritySort.HighestThreat.ToString() };

            [Menu("Cast Type", "Targeting behavior for skill casting.")]
            public ListNode CastType { get; set; } = new ListNode() { Value = CastTypeSort.TargetMonster.ToString() };

            [Menu("Is Movement Skill", "Should this hotkey be used for navigation")]
            public ToggleNode IsMovementKey { get; set; } = new ToggleNode(false);

            [Menu("Strategy Priority", "Higher priority skills are used first (1-10, 10 = highest).")]
            public RangeNode<int> StrategyPriority { get; set; } = new RangeNode<int>(5, 1, 10);

            [Menu("Minimum Delay", "Minimum time (in milliseconds) between casting")]
            public RangeNode<int> MinimumDelay { get; set; } = new RangeNode<int>(1000, 100, 60000);

            [Menu("Buff Name", "Name of buff to block skill from re-casting (optional).")]
            public TextNode Buff { get; set; } = "";

            /// <summary>
            /// Used to track when the next skill can be used (from minimum delay).
            /// </summary>
            public DateTime NextCast = DateTime.MinValue;

            public enum CastTypeSort
            {
                DoNotUse,
                TargetMonster,
                TargetSelf,
                TargetMercenary,
                TargetGround
            }

            public enum SkillRoleSort
            {
                PrimaryDamage,      // Main attack skill
                AreaDamage,         // AOE/clear skills  
                SingleTarget,       // Single target/boss skills
                Defensive,          // Guard skills, panic buttons
                Buff,               // Auras, buffs, toggles
                Movement            // Movement/traversal skills
            }

            public enum TargetPrioritySort
            {
                HighestThreat,      // Rare/Unique first, then closest
                ClosestEnemy,       // Always target nearest enemy
                RareFirst,          // Prioritize rare monsters
                UniqueFirst,        // Prioritize unique monsters  
                MostEnemies,        // Target center of largest group
                SelfTarget,         // For self-cast skills (buffs, etc)
                AllyTarget          // For mercenary/minion buffs
            }
        }

        [Submenu]
        public class CombatSettings
        {
            public CombatSettings()
            {
                Strategy.SetListValues(new List<string> { "Standard", "Aggressive" });
                SkillRotation.SetListValues(new List<string> { "Priority" });
            }

            [Menu("Combat Strategy", "Standard: Targets highest rarity monsters first. Aggressive: Prioritizes groups and fast clearing.")]
            public ListNode Strategy { get; set; } = new ListNode() { Value = "Standard" };

            [Menu("Skill Usage Pattern", "Priority: Uses highest priority skills first (recommended).")]
            public ListNode SkillRotation { get; set; } = new ListNode() { Value = "Priority" };

            [Menu("Defensive Threshold", "Health percentage to trigger defensive skills.")]
            public RangeNode<int> DefensiveThreshold { get; set; } = new RangeNode<int>(50, 10, 90);

            [Menu("Buff Maintenance", "Automatically maintain important buffs.")]
            public ToggleNode MaintainBuffs { get; set; } = new ToggleNode(true);

            [Menu("Focus Fire", "Concentrate damage on single targets vs spread damage.")]
            public ToggleNode FocusFire { get; set; } = new ToggleNode(true);
        }

        [Submenu(CollapsedByDefault = true)]
        public class DebugSettings
        {
            [Menu("Enable Debug Mode", "Show debug information overlay on screen.")]
            public ToggleNode EnableDebugMode { get; set; } = new ToggleNode(false);

            [Menu("Draw Inventory Items", "Draw boxes around items and equipment slots.")]
            public ToggleNode DrawInventory { get; set; } = new ToggleNode(false);

            [Menu("Show Item Details", "Show Path and Metadata for first items in stash/inventory.")]
            public ToggleNode ShowItemDetails { get; set; } = new ToggleNode(false);

            [Menu("Show Stash Debug", "Show detailed stash information when stash is open.")]
            public ToggleNode ShowStashDebug { get; set; } = new ToggleNode(true);

            [Menu("Show Inventory Debug", "Show detailed inventory information.")]
            public ToggleNode ShowInventoryDebug { get; set; } = new ToggleNode(true);

            [Menu("Show Combat Debug", "Show combat strategy and targeting information.")]
            public ToggleNode ShowCombatDebug { get; set; } = new ToggleNode(true);

            [Menu("Show Exploration Debug", "Show exploration pathfinding and area information.")]
            public ToggleNode ShowExplorationDebug { get; set; } = new ToggleNode(true);

            [Menu("Force Apply Incubators", "Manually trigger incubator application (requires stash and inventory open).")]
            public ButtonNode ForceApplyIncubators { get; set; } = new ButtonNode();
        }

        [Submenu(CollapsedByDefault = true)]
        public class CalibrationSettings
        {
            public CalibrationSettings()
            {
                CalibrationSlot.SetListValues(new List<string>
                {
                    "Helm1", "BodyArmour1", "Weapon1", "Offhand1", "Amulet1",
                    "Ring1", "Ring2", "Gloves1", "Boots1", "Belt1"
                });
            }

            [Menu("Calibrate Equipment Slots", "Enable calibration mode. Use Arrow Keys (hold Shift for faster) or sliders to position.")]
            public ToggleNode CalibrateEquipment { get; set; } = new ToggleNode(false);

            [Menu("Equipment Slot", "Which equipment slot to calibrate.")]
            public ListNode CalibrationSlot { get; set; } = new ListNode() { Value = "Helm1" };

            [Menu("Position X")]
            public RangeNode<int> CalibrationX { get; set; } = new RangeNode<int>(1585, 0, 3840);

            [Menu("Position Y")]
            public RangeNode<int> CalibrationY { get; set; } = new RangeNode<int>(165, 0, 2160);
        }

        [Submenu(CollapsedByDefault = true)]
        public class ScarabTrader
        {
            [Menu("Sell Items NPC", "Display name of the NPC we should use to sell items.")]
            public TextNode NpcName { get; set; } = new TextNode("Lilly Roth");

            [Menu("Scarabs to Sell", "Name of the scarabs we should sell, comma seperated for multiple.")]
            public TextNode ScarabsToSell { get; set; } = new TextNode("Influencing Scarab of the Shaper, Influencing Scarab of the Elder");
        }
    }
}


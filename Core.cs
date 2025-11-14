using AutoPOE.Logic.Actions;
using AutoPOE.Navigation;
using ExileCore;
using ExileCore.Shared.Enums;
using System.ComponentModel.Design;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using Graphics = ExileCore.Graphics;

namespace AutoPOE
{
    public static class Core
    {
        public static bool IsBotRunning = false;
        private static DateTime _nextAction = DateTime.Now;
        public static GameController GameController { get; private set; }
        public static Settings Settings { get; private set; }
        public static Graphics Graphics { get; private set; }
        public static Main Plugin { get; private set; }
        public static Map Map { get; private set; }

        public static bool HasIncubators = false;

        // Equipment slot positions - shared across the plugin
        public static Dictionary<InventorySlotE, Vector2> EquipmentSlotPositions { get; set; }

        /// <summary>
        /// Initializes the core components. This must be called once when the plugin starts.
        /// </summary>
        public static void Initialize(GameController controller, Settings settings, Graphics graphics, Main plugin)
        {
            GameController = controller;
            Settings = settings;
            Graphics = graphics;
            Plugin = plugin;

            AreaChanged();
        }


        /// <summary>
        /// Checks if the game window is currently in the foreground (focused)
        /// </summary>
        public static bool IsGameWindowForeground()
        {
            return GameController?.Window?.Process?.MainWindowHandle != IntPtr.Zero &&
                   GameController.Window.IsForeground();
        }

        /// <summary>
        /// Checks if an action can be performed (timing + window focus)
        /// </summary>
        public static bool CanUseAction => DateTime.Now > _nextAction && IsGameWindowForeground();

        public static void ActionPerformed()
        {
            _nextAction = DateTime.Now.AddMilliseconds(Settings.ActionFrequency);
        }


        public static void AreaChanged()
        {
            Map = new Map();
        }
    }
}

using AutoPOE.Logic.Sequences;
using System;

namespace AutoPOE.Logic
{
    /// <summary>
    /// Manages the lifecycle and dispatching of farm sequence implementations.
    /// Follows Single Responsibility Principle by handling only sequence orchestration.
    /// </summary>
    public class SequenceManager
    {
        private ISequence? _simulacrumSequence;
        private ISequence? _scarabTraderSequence;
        private ISequence? _debugSequence;

        /// <summary>
        /// Gets the currently active sequence based on farm method
        /// </summary>
        public ISequence? GetCurrentSequence(string farmMethod)
        {
            return farmMethod switch
            {
                "Simulacrum" => _simulacrumSequence,
                "ScarabTrader" => _scarabTraderSequence,
                "Debug" => _debugSequence,
                _ => null
            };
        }

        public SequenceManager()
        {
            InitializeSequences();
        }

        /// <summary>
        /// Initializes all available sequences.
        /// </summary>
        private void InitializeSequences()
        {
            _simulacrumSequence = new SimulacrumSequence();
            _scarabTraderSequence = new ScarabTraderSequence();
            _debugSequence = new DebugSequence();
        }

        /// <summary>
        /// Resets the scarab trader sequence. Used when toggling bot on/off.
        /// </summary>
        public void ResetScarabTraderSequence()
        {
            _scarabTraderSequence = new ScarabTraderSequence();
        }

        /// <summary>
        /// Dispatches the Tick call to the appropriate sequence based on farm method.
        /// </summary>
        /// <param name="farmMethod">The current farm method (Simulacrum, ScarabTrader, Debug)</param>
        public void Tick(string farmMethod)
        {
            if (!Core.CanUseAction)
                return;

            switch (farmMethod)
            {
                case "Simulacrum":
                    _simulacrumSequence?.Tick();
                    break;
                case "ScarabTrader":
                    _scarabTraderSequence?.Tick();
                    break;
                case "Debug":
                    _debugSequence?.Tick();
                    break;
            }
        }

        /// <summary>
        /// Dispatches the Render call to the appropriate sequence based on farm method.
        /// </summary>
        /// <param name="farmMethod">The current farm method (Simulacrum, ScarabTrader, Debug)</param>
        public void Render(string farmMethod)
        {
            switch (farmMethod)
            {
                case "Simulacrum":
                    _simulacrumSequence?.Render();
                    break;
                case "ScarabTrader":
                    _scarabTraderSequence?.Render();
                    break;
                case "Debug":
                    _debugSequence?.Render();
                    break;
            }
        }
    }
}

using System;

namespace KickLifeSupport
{
    public class KickLifeSupportSettings : GameParameters.CustomParameterNode
    {
        /// <summary>
        /// Header text
        /// </summary>
        public override string Title => "KICK Life Support Settings";

        /// <summary>
        /// Tab text
        /// </summary>
        public override string DisplaySection => "KICK Life Support";

        /// <summary>
        /// Hooks into the Difficulty window
        /// </summary>
        public override string Section => "Difficulty";

        /// <summary>
        /// Show near the top
        /// </summary>
        public override int SectionOrder => 1;

        /// <summary>
        /// Show in any game mode
        /// </summary>
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;

        /// <summary>
        /// No custom presets
        /// </summary>
        public override bool HasPresets => false;

        [GameParameters.CustomParameterUI("Use Cabin Temp System", autoPersistance = true)]
        public bool useCabinTempSystem = true;
    }
}

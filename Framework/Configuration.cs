using Microsoft.Xna.Framework;
using StardewModdingAPI;

namespace HarvestCalendar.Framework
{
    internal class Configuration
    {
        public Boolean ToggleMod { get; set; } = true;
        public int IconSize { get; set; } = 2;

        public float IconX { get; set; } = 1f;
        public float IconY { get; set; } = 0f;

    }
}

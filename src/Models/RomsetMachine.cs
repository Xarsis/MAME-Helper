namespace MAMEHelper.Models
{
    /// <summary>
    /// Represents a single machine entry parsed from mame -listxml or a DAT/XML list file.
    /// One instance per ROM name. Stored in the cache.
    /// </summary>
    public class RomsetMachine
    {
        /// <summary>ROM name / machine name, e.g. "pacman". Always lowercase.</summary>
        public string RomName { get; set; }

        /// <summary>Human-readable display name from the MAME XML description field, e.g. "Pac-Man (Midway)".</summary>
        public string Description { get; set; }

        /// <summary>
        /// Driver emulation status from the MAME XML driver element.
        /// Values: "good" | "imperfect" | "preliminary"
        /// </summary>
        public string DriverStatus { get; set; }

        /// <summary>True if this machine is a clone of another (cloneof attribute is set).</summary>
        public bool IsClone { get; set; }

        /// <summary>The parent ROM name if IsClone is true, otherwise null.</summary>
        public string CloneOf { get; set; }

        /// <summary>True if isbios="yes" in the MAME XML.</summary>
        public bool IsBios { get; set; }

        /// <summary>True if isdevice="yes" in the MAME XML.</summary>
        public bool IsDevice { get; set; }

        /// <summary>True if ismechanical="yes" in the MAME XML (pinball, slot machines, etc.).</summary>
        public bool IsMechanical { get; set; }

        /// <summary>True if the machine has a sampleof attribute (sample-only entry).</summary>
        public bool IsSample { get; set; }

        /// <summary>Release year from the MAME XML year element, e.g. "1980". May be "????" for unknown.</summary>
        public string Year { get; set; }

        /// <summary>Manufacturer name from the MAME XML manufacturer element.</summary>
        public string Manufacturer { get; set; }

        // ── Convenience helpers ──────────────────────────────────────────────

        /// <summary>True for entries that are not playable games (BIOS, device, mechanical, sample).</summary>
        public bool IsNonGame => IsBios || IsDevice || IsMechanical || IsSample;

        /// <summary>Friendly driver status for display in messages.</summary>
        public string FriendlyStatus
        {
            get
            {
                switch (DriverStatus?.ToLower())
                {
                    case "good":        return "Working";
                    case "imperfect":   return "Imperfect";
                    case "preliminary": return "Non-Working";
                    default:            return "Unknown";
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.SoftwareSpecific
{
    public class RAMReinforcement
    {
        // This is the Fy value for the main wall panel reinforcement.
        public double FyDistributed { get; set; } = 60; // Default 60 ksi

        // This is the Fy value for steel in wall boundary elements or end zones
        public double FuDistributed { get; set; } = 60; // Default 60 ksi

        // This is the Fy value for transverse reinforcement including 
        // confinement ties, stirrups, and cross-links in the wall
        public double FyTiesLinks { get; set; } = 60;   // Default 60 ksi
    }
}

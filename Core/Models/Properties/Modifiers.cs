using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Properties
{
    public class Modifiers
    {
        /// <summary>
        /// Frame element stiffness modifiers for beams, columns, and braces
        /// </summary>
        public class FrameModifiers
        {
            // Axial properties
            public double Area { get; set; } = 1.0;              // Cross-sectional area modifier

            // Shear properties  
            public double A22 { get; set; } = 1.0;             // Shear area modifier in local 2-direction
            public double A33 { get; set; } = 1.0;             // Shear area modifier in local 3-direction

            // Bending properties
            public double I22 { get; set; } = 1.0;         // Moment of inertia in local 2-direction (major axis)
            public double I33 { get; set; } = 1.0;         // Moment of inertia in local 3-direction (minor axis)

            // Torsional properties
            public double Torsion { get; set; } = 1.0;      // Torsional constant modifier

            // Mass properties
            public double Mass { get; set; } = 1.0;                   // Mass modifier
            public double Weight { get; set; } = 1.0;                 // Weight modifier
        }

        /// <summary>
        /// Shell element stiffness modifiers for floors, walls, and slabs
        /// </summary>
        public class ShellModifiers
        {
            // Membrane (in-plane) stiffness modifiers
            public double F11 { get; set; } = 1.0;            // In-plane stiffness in local 1-direction
            public double F22 { get; set; } = 1.0;            // In-plane stiffness in local 2-direction
            public double F12 { get; set; } = 1.0;            // In-plane shear stiffness

            // Bending (out-of-plane) stiffness modifiers
            public double M11 { get; set; } = 1.0;             // Bending stiffness in local 1-direction
            public double M22 { get; set; } = 1.0;             // Bending stiffness in local 2-direction
            public double M12 { get; set; } = 1.0;             // Twisting stiffness

            // Transverse shear stiffness modifiers
            public double V13 { get; set; } = 1.0;               // Out-of-plane shear in 1-3 plane
            public double V23 { get; set; } = 1.0;               // Out-of-plane shear in 2-3 plane

            // Mass properties
            public double Mass { get; set; } = 1.0;                   // Mass modifier
            public double Weight { get; set; } = 1.0;                 // Weight modifier
        }


    }
}
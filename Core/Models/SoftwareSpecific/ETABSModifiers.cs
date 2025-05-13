using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.SoftwareSpecific
{
    public class ETABSModifiers
    {
        public class ETABSFrameModifiers
        {
            public double Area { get; set; } = 1.0;
            public double A22 { get; set; } = 1.0;  // Shear area modifier in local 2-direction
            public double A33 { get; set; } = 1.0;  // Shear area modifier in local 3-direction
            public double I22 { get; set; } = 1.0;  // Moment of Inertia in local 2-direction
            public double I33 { get; set; } = 1.0;  // Moment of Inertia in local 3-direction
            public double Torsion { get; set; } = 1.0;
            public double Mass { get; set; } = 1.0;
            public double Weight { get; set; } = 1.0;
        }

        public class ETABSShellModifiers
        {
            public double F11 { get; set; } = 1.0;  // Membrane area modifier in local 1-direction
            public double F22 { get; set; } = 1.0;  // Membrane area modifier in local 2-direction
            public double F12 { get; set; } = 1.0;  // In-plane Shear stiffness
            public double M11 { get; set; } = 1.0;  // Bending area modifier in local 1-direction
            public double M22 { get; set; } = 1.0;  // Bending area modifier in local 2-direction
            public double M12 { get; set; } = 1.0;  // Twisitn stiffness
            public double V13 { get; set; } = 1.0;  // Out-of-plane shear area modifier in local 1-direction
            public double V23 { get; set; } = 1.0;  // Out-of-plane shear area modifier in local 2-direction
            public double Mass { get; set; } = 1.0; 
            public double Weight { get; set; } = 1.0;
        }
    }
}

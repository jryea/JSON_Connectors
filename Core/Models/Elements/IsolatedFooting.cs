using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Properties;
using Core.Models.ModelLayout;
using Core.Models.Loads;
using Core.Models.Metadata;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents an isolated footing element in the structural model
    /// </summary>
    public class IsolatedFooting
    {
        /// <summary>
        /// Location point of the isolated footing in 3D space
        /// </summary>
        public Point3D Point { get; set; }
    }
}
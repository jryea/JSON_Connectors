using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Properties
{
    /// <summary>
    /// Represents a diaphragm property in the structural model
    /// </summary>
    public class Diaphragm
    {
        /// <summary>
        /// Name of the diaphragm
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Type of diaphragm (e.g., "Rigid", "Semi-Rigid", "Flexible")
        /// </summary>
        public string Type { get; set; }
    }
}
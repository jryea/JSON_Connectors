using System.Collections.Generic;

namespace Core.Models.Properties.Floors
{
    /// <summary>
    /// Base class for floor type specific properties
    /// </summary>
    public class FloorTypeProperties
    {
        /// <summary>
        /// Additional properties specific to different software applications
        /// </summary>
        public Dictionary<string, object> SoftwareSpecificProperties { get; set; } = new Dictionary<string, object>();
    }
}
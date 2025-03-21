using Core.Models.Elements;
using Core.Models.Loads;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Metadata;
using System;
using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// Root model representing a complete building structure
    /// </summary>
    public class BaseModel
    {
        /// <summary>
        /// Unique identifier for the model
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Container for all structural elements
        /// </summary>
        public ElementContainer Elements { get; set; } = new ElementContainer();

        /// <summary>
        /// Container for all load definitions and combinations
        /// </summary>
        public LoadContainer Loads { get; set; } = new LoadContainer();

        /// <summary>
        /// Container for all property definitions
        /// </summary>
        public PropertiesContainer Properties { get; set; } = new PropertiesContainer();

        /// <summary>
        /// Analysis results from structural analysis
        /// </summary>
        public Dictionary<string, object> AnalysisResults { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Model layout components (grids, levels, floor types)
        /// </summary>
        public ModelLayoutContainer ModelLayout { get; set; } = new ModelLayoutContainer();

        /// <summary>
        /// Version control information
        /// </summary>
        public Dictionary<string, object> VersionControl { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Project metadata information
        /// </summary>
        public MetadataContainer Metadata { get; set; } = new MetadataContainer();
    }
    
}
using Core.Models.Elements;
using Core.Models.Loads;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Metadata;
using System;
using System.Collections.Generic;
using Core.Utilities;

namespace Core.Models
{
    // Root model representing a complete building structure
    public class BaseModel
    {
        // Unique identifier for the model
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Container for all structural elements
        public ElementContainer Elements { get; set; } = new ElementContainer();

        // Container for all load definitions and combinations
        public LoadContainer Loads { get; set; } = new LoadContainer();

        // Container for all property definitions
        public PropertiesContainer Properties { get; set; } = new PropertiesContainer();

        // Analysis results from structural analysis
        public Dictionary<string, object> AnalysisResults { get; set; } = new Dictionary<string, object>();

        // Model layout components (grids, levels, floor types)
        public ModelLayoutContainer ModelLayout { get; set; } = new ModelLayoutContainer();

        // Version control information
        public Dictionary<string, object> VersionControl { get; set; } = new Dictionary<string, object>();

        // Project metadata information
        public MetadataContainer Metadata { get; set; } = new MetadataContainer();

        // OOP delegate pattern
        // Removes duplicate geometry elements from the model
        public void RemoveDuplicates()
        {
            // Process the entire model, removing duplicates and updating all references
            ModelDuplicateHandler.RemoveDuplicates(this); ;
        }
    }
    
}
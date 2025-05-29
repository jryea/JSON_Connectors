using Core.Utilities;
using Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using static Core.Models.Properties.Modifiers;

namespace Core.Models.Properties
{
    
    public class FrameProperties : IIdentifiable
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string MaterialId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FrameMaterialType Type { get; set; }

        public ConcreteFrameProperties ConcreteProps { get; set; }
        public SteelFrameProperties SteelProps { get; set; }

        // ETABS-specific properties
        public ETABSFrameModifiers ETABSModifiers { get; set; } = new ETABSFrameModifiers();

        public FrameProperties()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES);
        }

        public FrameProperties(string name, string materialId, FrameMaterialType materialType) : this()
        {
            Name = name;
            MaterialId = materialId;
            Type = materialType;
        }
    }

    public class SteelFrameProperties
    {

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SteelSectionType SectionType { get; set; }
        public string SectionName { get; set; }
}

    public class ConcreteFrameProperties
    {

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ConcreteSectionType SectionType { get; set; }

        public string SectionName { get; set; }

        public double Depth = 12.0;

        public double Width = 12.0; 

        public Dictionary<string, string> Dimensions = new Dictionary<string, string>();
    }
    
}
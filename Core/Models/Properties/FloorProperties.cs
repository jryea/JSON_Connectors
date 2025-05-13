using Core.Models.SoftwareSpecific;
using Core.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using static Core.Models.SoftwareSpecific.ETABSModifiers;

namespace Core.Models.Properties
{
    public class FloorProperties : IIdentifiable
    {
        public string Id { get; set; }
        public string Name { get; set; }

        // Concrete Material Id
        public string MaterialId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StructuralFloorType Type { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ModelingType ModelingType { get; set; } = ModelingType.Membrane;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SlabType SlabType { get; set; } = SlabType.Slab;

        public double Thickness { get; set; }

        public DeckProperties DeckProperties { get; set; } = new DeckProperties();
        public ShearStudProperties ShearStudProperties { get; set; } = new ShearStudProperties();
        public ETABSShellModifiers ETABSModifiers { get; set; } = new ETABSShellModifiers();

        public FloorProperties()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES);
        }

        public FloorProperties(string name, StructuralFloorType type, double thickness, string materialId) : this()
        {
            Name = name;
            Type = type;
            Thickness = thickness;
            MaterialId = materialId;
        }
    }

    public class ShearStudProperties
    {
        public double ShearStudDiameter { get; set; }
        public double ShearStudHeight { get; set; }
        public double ShearStudTensileStrength { get; set; }

        public ShearStudProperties() 
        {
            ShearStudDiameter = 0.75; // Default diameter in inches
            ShearStudHeight = 6.0; // Default height in inches      
            ShearStudTensileStrength = 65000; // Default tensile strength in psi  
        }
    }

    public class DeckProperties
    {
        public string DeckType { get; set; } = "VULCRAFT 2VL"; 

        // Deck Material ID for ETABS
        public string MaterialID { get; set; }  

        public double RibDepth { get; set; }

        public double RibWidthTop { get; set; }  
        public double RibWidthBottom { get; set; }
        public double RibSpacing { get; set; }
        public double DeckShearThickness { get; set; }
        public double DeckUnitWeight { get; set; }

        public DeckProperties()
        {
            RibDepth = 3.0; // Default rib depth in inches
            RibWidthTop = 7.0; // Default top width in inches
            RibWidthBottom = 5.0; // Default bottom width in inches
            RibSpacing = 12.0; // Default spacing in inches
            DeckShearThickness = 0.035; // Default shear thickness in inches
            DeckUnitWeight = 2.3; // Default unit weight in pcf
        }
    }

    public enum StructuralFloorType
    {
        Slab,
        FilledDeck,
        UnfilledDeck,
        SolidSlabDeck
    }

    public enum ModelingType
    {
        ShellThin,
        ShellThick,
        Membrane,
        Layered
    }

    public enum  SlabType
    {
        Slab,
        Drop,
        Stiff,
        Ribbed,
        Waffle,
        Mat,
        Footing
    }
}
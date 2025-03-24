using System.Collections.Generic;
using Core.Utilities;

namespace Core.Models.Properties
{
    /// <summary>
    /// Represents properties for frame elements (beams, columns, braces) in the structural model
    /// </summary>
    public class FrameProperties : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the frame properties
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the frame properties
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// ID of the material for this frame element
        /// </summary>
        public string MaterialId { get; set; }

        /// <summary>
        /// Shape of the frame section (e.g., "W", "HSS", "Pipe")
        /// </summary>
        public string Shape { get; set; }

        /// <summary>
        /// Dimensions of the frame section
        /// </summary>
        public Dictionary<string, double> Dimensions { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Modifiers for section properties
        /// </summary>
        public Dictionary<string, object> Modifiers { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Reinforcement details
        /// </summary>
        public Dictionary<string, object> Rebar { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a new FrameProperties with a generated ID
        /// </summary>
        public FrameProperties()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES);
            Dimensions = new Dictionary<string, double>
            {
                { "depth", 0 },
                { "width", 0 }
            };
            Modifiers = new Dictionary<string, object>();
            Rebar = new Dictionary<string, object>();
        }

        /// <summary>
        /// Creates a new FrameProperties with specified properties
        /// </summary>
        /// <param name="name">Name of the frame properties</param>
        /// <param name="materialId">ID of the material for this frame element</param>
        /// <param name="shape">Shape of the frame section</param>
        public FrameProperties(string name, string materialId, string shape) : this()
        {
            Name = name;
            MaterialId = materialId;
            Shape = shape;

            // Initialize default dimensions based on shape
            InitializeDefaultDimensions();
        }

        /// <summary>
        /// Initializes default dimensions based on the frame shape
        /// </summary>
        private void InitializeDefaultDimensions()
        {
            switch (Shape?.ToUpper())
            {
                case "W":
                    Dimensions["depth"] = 12.0; // inches
                    Dimensions["width"] = 6.0; // inches
                    Dimensions["webThickness"] = 0.375; // inches
                    Dimensions["flangeThickness"] = 0.625; // inches
                    break;

                case "HSS":
                    Dimensions["depth"] = 6.0; // inches
                    Dimensions["width"] = 6.0; // inches
                    Dimensions["wallThickness"] = 0.25; // inches
                    break;

                case "PIPE":
                    Dimensions["outerDiameter"] = 6.0; // inches
                    Dimensions["wallThickness"] = 0.25; // inches
                    break;

                case "C":
                    Dimensions["depth"] = 12.0; // inches
                    Dimensions["width"] = 3.0; // inches
                    Dimensions["webThickness"] = 0.375; // inches
                    Dimensions["flangeThickness"] = 0.5; // inches
                    break;

                case "L":
                    Dimensions["depth"] = 6.0; // inches
                    Dimensions["width"] = 6.0; // inches
                    Dimensions["thickness"] = 0.5; // inches
                    break;
            }
        }
    }
}
using Core.Models.Geometry;
using Core.Utilities;
using System;
using static Core.Models.Properties.Modifiers;

namespace Core.Models.Elements
{
    // Represents a column element in the structural model
    public class Column : IIdentifiable, ITransformable
    {
        // Unique identifier for the column
        public string Id { get; set; }

        // Starting point of the column in 3D space
        public Point2D StartPoint { get; set; }

        // Ending point of the column in 3D space
        public Point2D EndPoint { get; set; }

        // ID of the base level for this column
        public string BaseLevelId { get; set; }

        // ID of the top level for this column
        public string TopLevelId { get; set; }

        public double Orientation { get; set; } = 0.0; // Orientation of the column in degrees   

        // ID of the section properties for this column
        public string FramePropertiesId { get; set; }

        // Lateral flag for RAM
        public bool IsLateral { get; set; } = false;

        // ETABS-specific properties
        public ETABSFrameModifiers ETABSModifiers { get; set; } = new ETABSFrameModifiers();

        // Creates a new Column with a generated ID
        public Column()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.COLUMN);
        }

        // Creates a new Column with the specified properties
        public Column(Point2D startPoint, Point2D endPoint, string baseLevelId, string topLevelId, string sectionId) : this()
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            BaseLevelId = baseLevelId;
            TopLevelId = topLevelId;
            FramePropertiesId = sectionId;
        }

        // ITransformable implementation
        public void Rotate(double angleDegrees, Point2D center)
        {
            StartPoint?.Rotate(angleDegrees, center);
            EndPoint?.Rotate(angleDegrees, center);

            // Rotate orientation
            Orientation += angleDegrees;
            // Normalize to 0-180 range (columns have 180-degree symmetry)
            while (Orientation >= 180.0) Orientation -= 180.0;
            while (Orientation < 0.0) Orientation += 180.0;
        }

        public void Translate(Point3D offset)
        {
            StartPoint?.Translate(offset);
            EndPoint?.Translate(offset);
        }
    }
}
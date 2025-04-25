using Core.Models.Geometry;
using Core.Utilities;

namespace Core.Models.Elements
{
    // Represents a column element in the structural model
    public class Column : IIdentifiable
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

        // ID of the section properties for this column
        public string FramePropertiesId { get; set; }

        // Lateral flag for RAM
        public bool IsLateral { get; set; }

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
    }
}
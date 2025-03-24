using Core.Utilities;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents a column element in the structural model
    /// </summary>
    public class Column : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the column
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Starting point of the column in 3D space
        /// </summary>
        public Point2D StartPoint { get; set; }

        /// <summary>
        /// Ending point of the column in 3D space
        /// </summary>
        public Point2D EndPoint { get; set; }

        /// <summary>
        /// ID of the base level for this column
        /// </summary>
        public string BaseLevelId { get; set; }

        /// <summary>
        /// ID of the top level for this column
        /// </summary>
        public string TopLevelId { get; set; }

        /// <summary>
        /// ID of the section properties for this column
        /// </summary>
        public string FramePropertiesId { get; set; }

        /// <summary>
        /// Analysis properties
        /// </summary>
        public ColumnAnalysisProperties Analysis { get; set; }

        /// <summary>
        /// Creates a new Column with a generated ID
        /// </summary>
        public Column()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.COLUMN);
            Analysis = new ColumnAnalysisProperties();
        }

        /// <summary>
        /// Creates a new Column with the specified properties
        /// </summary>
        /// <param name="startPoint">Starting point</param>
        /// <param name="endPoint">Ending point</param>
        /// <param name="baseLevelId">Base level ID</param>
        /// <param name="topLevelId">Top level ID</param>
        /// <param name="sectionId">Section properties ID</param>
        public Column(Point2D startPoint, Point2D endPoint, string baseLevelId, string topLevelId, string sectionId) : this()
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            BaseLevelId = baseLevelId;
            TopLevelId = topLevelId;
            FramePropertiesId = sectionId;
        }
    }

    /// <summary>
    /// Represents analysis properties for a column
    /// </summary>
    public class ColumnAnalysisProperties
    {
        /// <summary>
        /// Lateral or gravity designation
        /// </summary>
        public string LateralOrGravity { get; set; } = "Both";
    }
}
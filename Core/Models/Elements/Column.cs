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
        /// Lateral flag for RAM
        /// </summary>
        public bool IsLateral { get; set; }

        /// <summary>
        /// Creates a new Column with a generated ID
        /// </summary>
        public Column()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.COLUMN);
        }

        /// <summary>
        /// Creates a new Column with the specified properties
        /// </summary>
        /// <param name="StartPoint">Starting point</param>
        /// <param name="EndPoint">Ending point</param>
        /// <param name="BaseLevelId">Base level ID</param>
        /// <param name="TopLevelId">Top level ID</param>
        /// <param name="FramePropertiesId">Frame Section properties ID</param>
        /// <param name="IsLateral">Is Lateral flag</param>
        
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
using System;
using System.Text.Json.Serialization;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Utilities;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents a beam element in the structural model
    /// </summary>
    public class Beam : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the beam
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Starting point of the beam in 2D plan view
        /// </summary>
        public Point2D StartPoint { get; set; }

        /// <summary>
        /// Ending point of the beam in 2D plan view
        /// </summary>
        public Point2D EndPoint { get; set; }

        /// <summary>
        /// Level this beam belongs to
        /// </summary>
        public Level Level { get; set; }

        /// <summary>
        /// ID of the level (for serialization purposes)
        /// </summary>
        [JsonIgnore]
        public string LevelId
        {
            get => Level?.Id;
            set
            {
                // This setter would be used during deserialization
                // You would need to look up the actual Level object based on the ID
                // This would be handled by a model resolver after deserialization
            }
        }

        /// <summary>
        /// Properties for this beam
        /// </summary>
        public FrameProperties Properties { get; set; }

        /// <summary>
        /// ID of the properties (for serialization purposes)
        /// </summary>
        [JsonIgnore]
        public string PropertiesId
        {
            get => Properties?.Id;
            set
            {
                // This setter would be used during deserialization
                // You would need to look up the actual FrameProperties object based on the ID
                // This would be handled by a model resolver after deserialization
            }
        }

        /// <summary>
        /// Indicates if this beam is part of the lateral system
        /// </summary>
        public bool IsLateral { get; set; }

        /// <summary>
        /// Indicates if this beam is a joist
        /// </summary>
        public bool IsJoist { get; set; }

        /// <summary>
        /// Creates a new Beam with a generated ID
        /// </summary>
        public Beam()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.BEAM);
        }

        /// <summary>
        /// Creates a new Beam with the specified properties
        /// </summary>
        /// <param name="startPoint">Starting point</param>
        /// <param name="endPoint">Ending point</param>
        /// <param name="level">Level</param>
        /// <param name="properties">Properties</param>
        public Beam(Point2D startPoint, Point2D endPoint, Level level, FrameProperties properties) : this()
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            Level = level;
            Properties = properties;
        }

        /// <summary>
        /// Gets the length of the beam
        /// </summary>
        /// <returns>The length of the beam</returns>
        public double GetLength()
        {
            if (StartPoint == null || EndPoint == null)
                return 0;

            double dx = EndPoint.X - StartPoint.X;
            double dy = EndPoint.Y - StartPoint.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
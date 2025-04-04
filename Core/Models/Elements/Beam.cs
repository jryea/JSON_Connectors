﻿using System;
using Core.Models.Geometry;
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
        /// ID of the level this beam belongs to
        /// </summary>
        public string LevelId { get; set; }

        /// <summary>
        /// ID of the properties for this beam
        /// </summary>
        public string FramePropertiesId { get; set; }

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
        
    }
}
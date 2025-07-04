﻿using System;
using Core.Models.Geometry;
using Core.Utilities;
using static Core.Models.Properties.Modifiers;

namespace Core.Models.Elements
{
    // Represents a beam element in the structural model
    public class Beam : IIdentifiable, ITransformable
    {
        // Unique identifier for the beam
        public string Id { get; set; }

        // Starting point of the beam in 2D plan view
        public Point2D StartPoint { get; set; }

        // Ending point of the beam in 2D plan view
        public Point2D EndPoint { get; set; }

        // ID of the level this beam belongs to
        public string LevelId { get; set; }

        // ID of the properties for this beam
        public string FramePropertiesId { get; set; }

        // Indicates if this beam is part of the lateral system
        public bool IsLateral { get; set; }

        /// Indicates if this beam is a joist
        public bool IsJoist { get; set; }

        // ETABS-specific properties
        public FrameModifiers FrameModifiers { get; set; } = new FrameModifiers();

        // Creates a new Beam with a generated ID
        public Beam()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.BEAM);
        }

        // ITransformable implementation
        public void Rotate(double angleDegrees, Point2D center)
        {
            StartPoint?.Rotate(angleDegrees, center);
            EndPoint?.Rotate(angleDegrees, center);
        }

        public void Translate(Point3D offset)
        {
            StartPoint?.Translate(offset);
            EndPoint?.Translate(offset);
        }
    }
}
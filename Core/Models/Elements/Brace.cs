﻿using System.Collections.Generic;
using Core.Models.Geometry;
using Core.Utilities;
using static Core.Models.Properties.Modifiers;

namespace Core.Models.Elements
{
    // Represents a brace element in the structural model
    public class Brace : IIdentifiable, ITransformable
    {
        // Unique identifier for the brace
        public string Id { get; set; }

        // Material ID
        public string MaterialId { get; set; }

        // Frame Section Id
        public string FramePropertiesId { get; set; }

        // BaseLevel ID
        public string BaseLevelId { get; set; }

        // BaseLevel ID
        public string TopLevelId { get; set; }

        // Start point
        public Point2D StartPoint { get; set; }

        // End point
        public Point2D EndPoint { get; set; }

        // ETABS-specific properties
        public FrameModifiers FrameModifiers { get; set; } = new FrameModifiers();

        // Creates a new Brace with a generated ID
        public Brace()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.BRACE);
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
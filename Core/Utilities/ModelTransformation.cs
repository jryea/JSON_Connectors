using System;
using System.Collections.Generic;
using Core.Models;
using Core.Models.Geometry;

namespace Core.Utilities
{
    // Utility for applying geometric transformations to entire structural models
    public static class ModelTransformation
    {
        // Rotates all geometry in the model around a center point
        public static void RotateModel(BaseModel model, double angleDegrees, Point2D center)
        {
            if (model == null) return;
            if (Math.Abs(angleDegrees) < 1e-6) return; // Skip if no rotation

            // Transform all ITransformable objects in the model
            TransformAll(model, t => t.Rotate(angleDegrees, center));
        }

        // Translates all geometry in the model by the specified offset
        public static void TranslateModel(BaseModel model, Point3D offset)
        {
            if (model == null) return;
            if (Math.Abs(offset.X) < 1e-6 && Math.Abs(offset.Y) < 1e-6 && Math.Abs(offset.Z) < 1e-6) return;

            // Transform all ITransformable objects in the model
            TransformAll(model, t => t.Translate(offset));
        }

        // Applies both rotation and translation to the model
        public static void TransformModel(BaseModel model, double angleDegrees, Point2D rotationCenter, Point3D translation)
        {
            if (model == null) return;

            // Apply rotation first, then translation
            RotateModel(model, angleDegrees, rotationCenter);
            TranslateModel(model, translation);
        }

        // Visits all ITransformable objects in the model and applies the transformation
        private static void TransformAll(BaseModel model, Action<ITransformable> transform)
        {
            // Transform metadata coordinates
            if (model.Metadata?.Coordinates != null)
            {
                var coords = model.Metadata.Coordinates;
                if (coords is ITransformable transformableCoords)
                {
                    transform(transformableCoords);
                }
            }

            // Transform model layout components
            if (model.ModelLayout != null)
            {
                TransformCollection(model.ModelLayout.Grids, transform);
                TransformCollection(model.ModelLayout.Levels, transform);
            }

            // Transform structural elements
            if (model.Elements != null)
            {
                TransformCollection(model.Elements.Walls, transform);
                TransformCollection(model.Elements.Floors, transform);
                TransformCollection(model.Elements.Columns, transform);
                TransformCollection(model.Elements.Beams, transform);
                TransformCollection(model.Elements.Braces, transform);
                TransformCollection(model.Elements.IsolatedFootings, transform);
                TransformCollection(model.Elements.ContinuousFootings, transform);
                TransformCollection(model.Elements.Piles, transform);
                TransformCollection(model.Elements.Piers, transform);
                TransformCollection(model.Elements.DrilledPiers, transform);
                TransformCollection(model.Elements.Joints, transform);
            }
        }

        // Transforms all ITransformable objects in a collection
        private static void TransformCollection<T>(IEnumerable<T> collection, Action<ITransformable> transform)
        {
            if (collection == null) return;

            foreach (var item in collection)
            {
                if (item is ITransformable transformable)
                {
                    transform(transformable);
                }
            }
        }
    }
}
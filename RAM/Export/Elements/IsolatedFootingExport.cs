using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Elements
{
    public class IsolatedFootingExport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;

        public IsolatedFootingExport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
        }

        public List<IsolatedFooting> Export()
        {
            var isolatedFootings = new List<IsolatedFooting>();

            try
            {
                // Get concrete material ID
                string concreteMaterialId = _materialProvider.GetConcreteMaterialId();

                // Get all stories from RAM
                IStories ramStories = _model.GetStories();
                if (ramStories == null || ramStories.GetCount() == 0)
                    return isolatedFootings;

                // Find the foundation story (lowest story)
                IStory foundationStory = null;
                double lowestElevation = double.MaxValue;

                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory story = ramStories.GetAt(i);
                    if (story != null && story.dElevation < lowestElevation)
                    {
                        lowestElevation = story.dElevation;
                        foundationStory = story;
                    }
                }

                if (foundationStory == null)
                    return isolatedFootings;

                // Find the corresponding level ID for the foundation story
                string foundationLevelId = ModelMappingUtility.GetLevelIdForStoryUid(foundationStory.lUID.ToString());

                // If no level mapping found, use the ground level
                if (string.IsNullOrEmpty(foundationLevelId))
                {
                    foundationLevelId = ModelMappingUtility.GetGroundLevelId();
                    if (string.IsNullOrEmpty(foundationLevelId))
                    {
                        Console.WriteLine("No foundation level found for isolated footings");
                        return isolatedFootings;
                    }
                }

                // Get isolated footings for the foundation story
                IIsolatedFnds foundations = foundationStory.GetIsolatedFnds();
                if (foundations == null || foundations.GetCount() == 0)
                    return isolatedFootings;

                // Process each isolated footing
                for (int i = 0; i < foundations.GetCount(); i++)
                {
                    IIsolatedFnd foundation = foundations.GetAt(i);
                    if (foundation == null)
                        continue;

                    // Get footing coordinates
                    SCoordinate location = new SCoordinate();
                    foundation.GetCoordinate(ref location);

                    // Get footing dimensions
                    double width = foundation.dRight + foundation.dLeft;
                    double length = foundation.dTop + foundation.dBottom;
                    double thickness = foundation.dThickness;

                    Console.WriteLine($"Isolated Footing: {foundation.lLabel}, Width: {width}, Length: {length}, Thickness: {thickness}");

                    // Create isolated footing from RAM data
                    IsolatedFooting isolatedFooting = new IsolatedFooting
                    {
                        Id = IdGenerator.Generate(IdGenerator.Elements.ISOLATED_FOOTING),
                        Point = new Point3D(
                            UnitConversionUtils.ConvertFromInches(location.dXLoc, _lengthUnit),
                            UnitConversionUtils.ConvertFromInches(location.dYLoc, _lengthUnit),
                            UnitConversionUtils.ConvertFromInches(location.dZLoc, _lengthUnit)
                        ),
                        Width = UnitConversionUtils.ConvertFromInches(width, _lengthUnit),
                        Length = UnitConversionUtils.ConvertFromInches(length, _lengthUnit),
                        Thickness = UnitConversionUtils.ConvertFromInches(thickness, _lengthUnit),
                        LevelId = foundationLevelId,
                        MaterialId = concreteMaterialId  // Assign the concrete material ID
                    };

                    isolatedFootings.Add(isolatedFooting);
                }

                return isolatedFootings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting isolated footings from RAM: {ex.Message}");
                return isolatedFootings;
            }
        }
    }
}
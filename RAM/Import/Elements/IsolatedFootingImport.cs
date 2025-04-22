// IsolatedFootingImport.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Geometry;
using RAM.Utilities;
using Core.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Elements
{
    public class IsolatedFootingImport
    {
        private IModel _model;
        private string _lengthUnit;

        public IsolatedFootingImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<IsolatedFooting> isolatedFootings, IEnumerable<Level> levels)
        {
            try
            {
                if (isolatedFootings == null || !isolatedFootings.Any() || levels == null || !levels.Any())
                    return 0;

                // Get RAM floor types
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                    return 0;

                // Sort levels by elevation to identify foundation level (lowest level)
                var sortedLevels = levels.OrderBy(l => l.Elevation).ToList();
                Level foundationLevel = sortedLevels.FirstOrDefault();

                if (foundationLevel == null)
                {
                    Console.WriteLine("No foundation level found for isolated footings");
                    return 0;
                }

                // Map floor types to their "master" level (first level with that floor type)
                Dictionary<string, Level> masterLevelByFloorTypeId = new Dictionary<string, Level>();

                // Find the first (lowest) level for each floor type - this will be the "master" level
                foreach (var level in sortedLevels)
                {
                    if (!string.IsNullOrEmpty(level.FloorTypeId) && !masterLevelByFloorTypeId.ContainsKey(level.FloorTypeId))
                    {
                        masterLevelByFloorTypeId[level.FloorTypeId] = level;
                        Console.WriteLine($"Floor type {level.FloorTypeId} has master level {level.Name} at elevation {level.Elevation}");
                    }
                }

                // Map Core floor types to RAM floor types
                Dictionary<string, IFloorType> ramFloorTypeByFloorTypeId = new Dictionary<string, IFloorType>();

                // Assign RAM floor types to Core floor types
                for (int i = 0; i < ramFloorTypes.GetCount() && i < masterLevelByFloorTypeId.Count; i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    string coreFloorTypeId = masterLevelByFloorTypeId.Keys.ElementAt(i);
                    ramFloorTypeByFloorTypeId[coreFloorTypeId] = ramFloorType;
                    Console.WriteLine($"Mapped Core floor type {coreFloorTypeId} to RAM floor type {ramFloorType.strLabel}");
                }

                // Use foundation level's floor type ID
                string foundationFloorTypeId = foundationLevel.FloorTypeId;
                if (string.IsNullOrEmpty(foundationFloorTypeId))
                {
                    Console.WriteLine("Foundation level has no floor type ID");
                    return 0;
                }

                // Get RAM floor type for foundation
                if (!ramFloorTypeByFloorTypeId.TryGetValue(foundationFloorTypeId, out IFloorType ramFoundationFloorType))
                {
                    Console.WriteLine($"No RAM floor type found for foundation floor type {foundationFloorTypeId}");
                    return 0;
                }

                // Get the layout isolated foundations from the RAM floor type
                ILayoutIsolatedFnds layoutIsolatedFnds = ramFoundationFloorType.GetLayoutIsolatedFnds();
                if (layoutIsolatedFnds == null)
                {
                    Console.WriteLine("Failed to get layout isolated foundations from RAM floor type");
                    return 0;
                }

                // Track processed footings to avoid duplicates
                HashSet<string> processedFootings = new HashSet<string>();

                // Import isolated footings
                int count = 0;
                foreach (var footing in isolatedFootings)
                {
                    if (footing.Point == null)
                    {
                        Console.WriteLine($"Skipping isolated footing {footing.Id}: Invalid point");
                        continue;
                    }

                    // Convert coordinates to inches (RAM's unit)
                    double x = UnitConversionUtils.ConvertToInches(footing.Point.X, _lengthUnit);
                    double y = UnitConversionUtils.ConvertToInches(footing.Point.Y, _lengthUnit);
                    double z = UnitConversionUtils.ConvertToInches(footing.Point.Z, _lengthUnit);

                    // Create a unique key for this footing
                    string footingKey = $"{x:F2}_{y:F2}";

                    // Skip if we've already processed this footing
                    if (processedFootings.Contains(footingKey))
                    {
                        Console.WriteLine($"Skipping duplicate isolated footing at ({x}, {y})");
                        continue;
                    }

                    // Add to processed set
                    processedFootings.Add(footingKey);

                    try
                    {
                        // Add the isolated footing to RAM
                        ILayoutIsolatedFnd ramFooting = layoutIsolatedFnds.Add(
                            EIsolatedFndType.eIFndSpread, // Concrete material
                            x, y, // Location
                            0); // Offset

                        if (ramFooting != null)
                        {
                            ramFooting.dTop = footing.Width/2;
                            ramFooting.dLeft = footing.Length/2;
                            ramFooting.dThickness = footing.Thickness;  

                            count++;
                            Console.WriteLine($"Added isolated footing at ({x}, {y})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating isolated footing: {ex.Message}");
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing isolated footings: {ex.Message}");
                throw;
            }
        }
    }
}
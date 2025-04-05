// BeamImport.cs
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Elements
{
    // Imports beam elements to RAM from the Core model
    public class BeamImport
    {
        private IModel _model;
        private string _lengthUnit;
        private Dictionary<string, IFloorType> _floorTypeMap = new Dictionary<string, IFloorType>();

        public BeamImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
            InitializeFloorTypeMap();
        }

        // Initializes the floor type mapping
        private void InitializeFloorTypeMap()
        {
            try
            {
                _floorTypeMap.Clear();
                IFloorTypes floorTypes = _model.GetFloorTypes();

                for (int i = 0; i < floorTypes.GetCount(); i++)
                {
                    IFloorType floorType = floorTypes.GetAt(i);
                    _floorTypeMap[floorType.strLabel] = floorType;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing floor type map: {ex.Message}");
            }
        }

        // Imports beams to RAM model for each floor type
       
        public int Import(IEnumerable<Beam> beams, IEnumerable<Level> levels, IEnumerable<FrameProperties> frameProperties)
        {
            try
            {
                int count = 0;
                Dictionary<string, Level> levelMap = new Dictionary<string, Level>();
                Dictionary<string, EMATERIALTYPES> materialsMap = new Dictionary<string, EMATERIALTYPES>();

                // Build level mapping
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level.Id))
                    {
                        levelMap[level.Id] = level;
                    }
                }

                // Build materials mapping
                foreach (var frameProp in frameProperties)
                {
                    if (!string.IsNullOrEmpty(frameProp.Id))
                    {
                        EMATERIALTYPES materialType = EMATERIALTYPES.ESteelMat; // Default to steel

                        // Determine if it's a joist by name
                        bool isJoist = frameProp.Name?.ToLower().Contains("joist") == true ||
                                      frameProp.Shape?.ToLower().Contains("joist") == true;

                        if (isJoist)
                        {
                            materialType = EMATERIALTYPES.ESteelJoistMat;
                        }
                        else if (frameProp.MaterialId != null)
                        {
                            // Try to determine material from material ID
                            // This would need additional logic to map MaterialId to EMATERIALTYPES
                            // For now we'll just check the material name for concrete vs steel
                            string materialName = frameProp.MaterialId?.ToLower() ?? "";
                            if (materialName.Contains("concrete"))
                            {
                                materialType = EMATERIALTYPES.EConcreteMat;
                            }
                        }

                        materialsMap[frameProp.Id] = materialType;
                    }
                }

                // Process all beams
                foreach (var beam in beams)
                {
                    if (beam.StartPoint == null || beam.EndPoint == null || string.IsNullOrEmpty(beam.LevelId))
                    {
                        continue;
                    }

                    // Get the level and floor type
                    if (!levelMap.TryGetValue(beam.LevelId, out Level level) ||
                        level.FloorTypeId == null ||
                        !GetRamFloorTypeByFloorTypeId(level.FloorTypeId, out IFloorType floorType))
                    {
                        continue;
                    }

                    // Get beam material
                    EMATERIALTYPES beamMaterial = EMATERIALTYPES.ESteelMat; // Default
                    if (beam.FramePropertiesId != null &&
                        materialsMap.TryGetValue(beam.FramePropertiesId, out EMATERIALTYPES material))
                    {
                        beamMaterial = material;
                    }

                    // Convert coordinates to inches
                    double beamX1 = Helpers.ConvertToInches(beam.StartPoint.X, _lengthUnit);
                    double beamY1 = Helpers.ConvertToInches(beam.StartPoint.Y, _lengthUnit);
                    double beamZ1 = 0; // Z coordinate is typically determined by the level
                    double beamX2 = Helpers.ConvertToInches(beam.EndPoint.X, _lengthUnit);
                    double beamY2 = Helpers.ConvertToInches(beam.EndPoint.Y, _lengthUnit);
                    double beamZ2 = 0;

                    // Create the beam in RAM
                    ILayoutBeams layoutBeams = floorType.GetLayoutBeams();
                    ILayoutBeam ramBeam = layoutBeams.Add(beamMaterial, beamX1, beamY1, beamZ1, beamX2, beamY2, beamZ2);

                    if (ramBeam != null)
                    {
                        count++;
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing beams: {ex.Message}");
                throw;
            }
        }

        // Gets a RAM floor type by floor type ID from our model
        private bool GetRamFloorTypeByFloorTypeId(string floorTypeId, out IFloorType floorType)
        {
            floorType = null;

            try
            {
                // Try to find a floor type with a matching ID
                // In a real implementation, we would have a mapping from our model's floor type IDs
                // to RAM floor type names, but for simplicity, we'll just use the first floor type

                if (_floorTypeMap.Count > 0)
                {
                    floorType = _floorTypeMap.Values.GetEnumerator().Current;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
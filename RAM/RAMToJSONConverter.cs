using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.ModelLayout;
using Core.Models.Elements;
using Core.Models.Properties;
using Core.Models.Geometry;
using Core.Models.Metadata;
using Core.Models.Loads;
using Core.Converters;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;
using System.Reflection;

namespace RAM
{
    public class RAMToJSONConverter
    {
        // Main method to convert RAM model to JSON
        public (string JsonOutput, string Message, bool Success) ConvertRAMToJSON(string ramFilePath)
        {
            try
            {
                // Open the RAM model
                using (var modelManager = new RAMModelManager())
                {
                    bool isOpen = modelManager.OpenModel(ramFilePath);
                    if (!isOpen)
                    {
                        return (null, "Failed to open RAM model file.", false);
                    }

                    // Create the base model structure
                    BaseModel model = new BaseModel
                    {
                        Metadata = new MetadataContainer
                        {
                            ProjectInfo = ExtractProjectInfo(modelManager.Model),
                            Units = new Units { Length = "inches", Force = "kips", Temperature = "fahrenheit" }
                        },
                        ModelLayout = new ModelLayoutContainer(),
                        Properties = new PropertiesContainer(),
                        Elements = new ElementContainer(),
                        Loads = new LoadContainer()
                    };

                    try
                    {
                        // Extract floor types
                        ExtractFloorTypes(modelManager.Model, model.ModelLayout);

                        // Extract stories/levels
                        ExtractStories(modelManager.Model, model.ModelLayout);

                        // Extract grids
                        ExtractGrids(modelManager.Model, model.ModelLayout);

                        // Extract materials and properties
                        ExtractMaterials(modelManager.Model, model.Properties);
                        ExtractFrameProperties(modelManager.Model, model.Properties);
                        ExtractFloorProperties(modelManager.Model, model.Properties);
                        ExtractWallProperties(modelManager.Model, model.Properties);

                        // Extract structural elements
                        ExtractBeams(modelManager.Model, model);
                        ExtractColumns(modelManager.Model, model);
                        ExtractWalls(modelManager.Model, model);

                        // Extract loads
        private void ExtractLoads(IModel ramModel, LoadContainer loadContainer)
        {
            try
            {
                // Extract load definitions
                var deadLoad = new LoadDefinition
                {
                    Name = "Dead",
                    Type = "Dead",
                    SelfWeight = 1.0
                };

                var liveLoad = new LoadDefinition
                {
                    Name = "Live",
                    Type = "Live",
                    SelfWeight = 0.0
                };

                loadContainer.LoadDefinitions.Add(deadLoad);
                loadContainer.LoadDefinitions.Add(liveLoad);

                // Extract surface loads
                try
                {
                    ISurfaceLoadSets surfaceLoadSets = ramModel.GetSurfaceLoadSets();
                    if (surfaceLoadSets != null)
                    {
                        for (int i = 0; i < surfaceLoadSets.GetCount(); i++)
                        {
                            ISurfaceLoadSet loadSet = surfaceLoadSets.GetAt(i);
                            if (loadSet != null)
                            {
                                var surfaceLoad = new SurfaceLoad
                                {
                                    DeadLoadId = deadLoad.Id,
                                    LiveLoadId = liveLoad.Id
                                };

                                loadContainer.SurfaceLoads.Add(surfaceLoad);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting surface loads: {ex.Message}");
                }

                // Add default load combinations
                var loadCombo = new LoadCombination
                {
                    LoadDefinitionIds = new List<string> { deadLoad.Id, liveLoad.Id }
                };

                loadContainer.LoadCombinations.Add(loadCombo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting loads: {ex.Message}");
            }
        }
        ExtractLoads(modelManager.Model, model.Loads);
    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during extraction: {ex.Message}");
                        // Continue with partial model
                    }

// Serialize the model to JSON
string jsonString = JsonConverter.Serialize(model);

return (jsonString, "Successfully converted RAM model to JSON.", true);
                }
            }
            catch (Exception ex)
            {
                return (null, $"Error converting RAM to JSON: {ex.Message}", false);
            }
        }

        #region Extraction Methods

        // Helper methods for mapping objects
        private Dictionary<int, string> CreateFloorTypeToLevelMapping(BaseModel model)
{
    var mapping = new Dictionary<int, string>();

    if (model.ModelLayout.FloorTypes.Count == 0 || model.ModelLayout.Levels.Count == 0)
        return mapping;

    // Find levels with each floor type ID
    foreach (var level in model.ModelLayout.Levels)
    {
        if (string.IsNullOrEmpty(level.FloorTypeId))
            continue;

        var floorType = model.ModelLayout.FloorTypes.Find(ft => ft.Id == level.FloorTypeId);
        if (floorType != null && floorType.Description != null && floorType.Description.Contains("ID: "))
        {
            string idStr = floorType.Description.Substring(floorType.Description.IndexOf("ID: ") + 4);
            if (int.TryParse(idStr, out int uid))
            {
                mapping[uid] = level.Id;
            }
        }
    }

    return mapping;
}

private Dictionary<string, string> CreateSectionToFramePropsMapping(BaseModel model)
{
    var mapping = new Dictionary<string, string>();

    if (model.Properties.FrameProperties.Count == 0)
        return mapping;

    foreach (var frameProp in model.Properties.FrameProperties)
    {
        if (!string.IsNullOrEmpty(frameProp.Name))
        {
            mapping[frameProp.Name] = frameProp.Id;
        }
    }

    return mapping;
}

private string GetLevelIdForFloorType(int floorTypeUID, Dictionary<int, string> mapping)
{
    if (mapping.TryGetValue(floorTypeUID, out string levelId))
        return levelId;

    // Return the first level ID if available as fallback
    return mapping.Values.FirstOrDefault();
}

private string GetFramePropertiesId(string sectionName, Dictionary<string, string> mapping)
{
    if (!string.IsNullOrEmpty(sectionName) && mapping.TryGetValue(sectionName, out string framePropsId))
        return framePropsId;

    // Return the first frame properties ID if available as fallback
    return mapping.Values.FirstOrDefault();
}

// Extract project information
private ProjectInfo ExtractProjectInfo(IModel ramModel)
{
    var projectInfo = new ProjectInfo
    {
        ProjectName = "Imported from RAM",
        ProjectId = Guid.NewGuid().ToString(),
        CreationDate = DateTime.Now,
        SchemaVersion = "1.0"
    };

    try
    {
        // Try to get project information from RAM
        IProjectInfo ramProjectInfo = ramModel.GetProjectInfo();
        if (ramProjectInfo != null)
        {
            projectInfo.ProjectName = ramProjectInfo.strProjectName ?? projectInfo.ProjectName;

            // If the project has a job number, use it as the ID
            if (!string.IsNullOrEmpty(ramProjectInfo.strJobNum))
            {
                projectInfo.ProjectId = ramProjectInfo.strJobNum;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting project info: {ex.Message}");
    }

    return projectInfo;
}

// Extract floor types
private void ExtractFloorTypes(IModel ramModel, ModelLayoutContainer layoutContainer)
{
    try
    {
        IFloorTypes ramFloorTypes = ramModel.GetFloorTypes();
        if (ramFloorTypes == null || ramFloorTypes.GetCount() == 0)
            return;

        for (int i = 0; i < ramFloorTypes.GetCount(); i++)
        {
            IFloorType ramFloorType = ramFloorTypes.GetAt(i);
            if (ramFloorType != null)
            {
                var floorType = new FloorType
                {
                    Name = ramFloorType.strLabel,
                    Description = $"Floor type from RAM (ID: {ramFloorType.lUID})"
                };
                layoutContainer.FloorTypes.Add(floorType);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting floor types: {ex.Message}");
    }
}

// Extract stories/levels
private void ExtractStories(IModel ramModel, ModelLayoutContainer layoutContainer)
{
    try
    {
        IStories ramStories = ramModel.GetStories();
        if (ramStories == null || ramStories.GetCount() == 0)
            return;

        // Make sure we have at least one floor type
        if (layoutContainer.FloorTypes.Count == 0)
        {
            layoutContainer.FloorTypes.Add(new FloorType
            {
                Name = "Default",
                Description = "Default floor type"
            });
        }

        string defaultFloorTypeId = layoutContainer.FloorTypes[0].Id;

        // Create mapping from RAM floor type UIDs to Core floor type IDs
        Dictionary<int, string> floorTypeMapping = new Dictionary<int, string>();
        foreach (var floorType in layoutContainer.FloorTypes)
        {
            // Extract UID from description if available
            if (floorType.Description != null && floorType.Description.Contains("ID: "))
            {
                string idStr = floorType.Description.Substring(floorType.Description.IndexOf("ID: ") + 4);
                if (int.TryParse(idStr, out int uid))
                {
                    floorTypeMapping[uid] = floorType.Id;
                }
            }
        }

        double previousElevation = 0;

        for (int i = 0; i < ramStories.GetCount(); i++)
        {
            IStory ramStory = ramStories.GetAt(i);
            if (ramStory != null)
            {
                string floorTypeId = defaultFloorTypeId;

                // Try to get the actual floor type ID
                if (floorTypeMapping.TryGetValue(ramStory.lFloorTypeUID, out string mappedId))
                {
                    floorTypeId = mappedId;
                }

                var level = new Level
                {
                    Name = ramStory.strStoryLabel,
                    FloorTypeId = floorTypeId,
                    Elevation = previousElevation + ramStory.dHeight
                };

                layoutContainer.Levels.Add(level);
                previousElevation += ramStory.dHeight;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting stories: {ex.Message}");
    }
}

// Extract grids
private void ExtractGrids(IModel ramModel, ModelLayoutContainer layoutContainer)
{
    try
    {
        IGridSystems gridSystems = ramModel.GetGridSystems();
        if (gridSystems == null || gridSystems.GetCount() == 0)
            return;

        // Use only the first grid system for simplicity
        IGridSystem gridSystem = gridSystems.GetAt(0);
        if (gridSystem == null)
            return;

        IModelGrids modelGrids = gridSystem.GetGrids();
        if (modelGrids == null)
            return;

        for (int i = 0; i < modelGrids.GetCount(); i++)
        {
            IModelGrid modelGrid = modelGrids.GetAt(i);
            if (modelGrid != null)
            {
                // Determine start and end points based on grid axis
                double coordinate = modelGrid.dCoord;
                GridPoint startPoint, endPoint;

                if (modelGrid.eAxis == EGridAxis.eGridXorRadialAxis)
                {
                    // X grid (vertical line with constant X)
                    startPoint = new GridPoint(coordinate, -1000, 0, true);
                    endPoint = new GridPoint(coordinate, 1000, 0, true);
                }
                else
                {
                    // Y grid (horizontal line with constant Y)
                    startPoint = new GridPoint(-1000, coordinate, 0, true);
                    endPoint = new GridPoint(1000, coordinate, 0, true);
                }

                var grid = new Grid
                {
                    Name = modelGrid.strLabel,
                    StartPoint = startPoint,
                    EndPoint = endPoint
                };

                layoutContainer.Grids.Add(grid);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting grids: {ex.Message}");
    }
}

// Extract materials
private void ExtractMaterials(IModel ramModel, PropertiesContainer properties)
{
    try
    {
        // Extract steel materials
        ExtractSteelMaterials(ramModel, properties);

        // Extract concrete materials
        ExtractConcreteMaterials(ramModel, properties);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting materials: {ex.Message}");
    }
}

private void ExtractSteelMaterials(IModel ramModel, PropertiesContainer properties)
{
    try
    {
        var material = new Material("Steel", "Steel")
        {
            DesignData =
                    {
                        ["fy"] = 50000.0, // Default Fy = 50 ksi
                        ["fu"] = 65000.0, // Default Fu = 65 ksi
                        ["elasticModulus"] = 29000000.0, // E = 29000 ksi
                        ["weightDensity"] = 490.0, // 490 pcf
                        ["poissonsRatio"] = 0.3
                    }
        };

        properties.Materials.Add(material);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting steel materials: {ex.Message}");
    }
}

private void ExtractConcreteMaterials(IModel ramModel, PropertiesContainer properties)
{
    try
    {
        var material = new Material("Concrete", "Concrete")
        {
            DesignData =
                    {
                        ["fc"] = 4000.0, // Default f'c = 4 ksi
                        ["elasticModulus"] = 3600000.0, // Default E = 3600 ksi
                        ["weightDensity"] = 150.0, // 150 pcf
                        ["poissonsRatio"] = 0.2
                    }
        };

        properties.Materials.Add(material);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting concrete materials: {ex.Message}");
    }
}

// Extract frame properties
private void ExtractFrameProperties(IModel ramModel, PropertiesContainer properties)
{
    if (properties.Materials.Count == 0)
        return;

    try
    {
        // Default material IDs
        string steelMaterialId = properties.Materials.Find(m => m.Type.ToLower() == "steel")?.Id;
        string concreteMaterialId = properties.Materials.Find(m => m.Type.ToLower() == "concrete")?.Id;

        // Extract beam sections
        ExtractSteelBeamSections(ramModel, properties, steelMaterialId);

        // Extract column sections
        ExtractColumnSections(ramModel, properties, steelMaterialId, concreteMaterialId);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting frame properties: {ex.Message}");
    }
}

private void ExtractSteelBeamSections(IModel ramModel, PropertiesContainer properties, string materialId)
{
    try
    {
        ISteelBeamTypes steelBeamTypes = ramModel.GetSteelBeamTypes();
        if (steelBeamTypes == null)
            return;

        for (int i = 0; i < steelBeamTypes.GetCount(); i++)
        {
            ISteelBeamType beamType = steelBeamTypes.GetAt(i);
            if (beamType != null)
            {
                string name = beamType.strSectionLabel;
                string shape = "W"; // Default to W shape

                // Try to determine shape type from name
                if (name.StartsWith("W"))
                    shape = "W";
                else if (name.StartsWith("HSS"))
                    shape = "HSS";
                else if (name.StartsWith("C"))
                    shape = "C";
                else if (name.StartsWith("L"))
                    shape = "L";

                var frameProperties = new FrameProperties
                {
                    Name = name,
                    MaterialId = materialId,
                    Shape = shape
                };

                // Add dimensions based on section properties
                frameProperties.Dimensions["depth"] = beamType.dDepth;
                frameProperties.Dimensions["width"] = beamType.dWidth;

                if (shape == "W")
                {
                    frameProperties.Dimensions["webThickness"] = beamType.dWebThickness;
                    frameProperties.Dimensions["flangeThickness"] = beamType.dFlangeThickness;
                }

                properties.FrameProperties.Add(frameProperties);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting steel beam sections: {ex.Message}");
    }
}

private void ExtractColumnSections(IModel ramModel, PropertiesContainer properties, string steelMaterialId, string concreteMaterialId)
{
    try
    {
        // Extract steel columns
        IColumnTypes columnTypes = ramModel.GetColumnTypes();
        if (columnTypes != null)
        {
            for (int i = 0; i < columnTypes.GetCount(); i++)
            {
                IColumnType columnType = columnTypes.GetAt(i);
                if (columnType != null)
                {
                    string name = columnType.strSectionLabel;
                    bool isConcrete = columnType.eMaterialType == EMATERIALTYPES.EConcreteMat;
                    string materialId = isConcrete ? concreteMaterialId : steelMaterialId;
                    string shape = isConcrete ? "RECT" : "W";

                    var frameProperties = new FrameProperties
                    {
                        Name = name,
                        MaterialId = materialId,
                        Shape = shape
                    };

                    // Add dimensions
                    frameProperties.Dimensions["depth"] = columnType.dDepth;
                    frameProperties.Dimensions["width"] = columnType.dWidth;

                    if (shape == "W")
                    {
                        // Try to get web and flange thickness if available
                        try
                        {
                            var steelColumn = columnType as ISteelColumnType;
                            if (steelColumn != null)
                            {
                                frameProperties.Dimensions["webThickness"] = steelColumn.dWebThickness;
                                frameProperties.Dimensions["flangeThickness"] = steelColumn.dFlangeThickness;
                            }
                        }
                        catch
                        {
                            // Use default values if not available
                            frameProperties.Dimensions["webThickness"] = 0.375;
                            frameProperties.Dimensions["flangeThickness"] = 0.625;
                        }
                    }

                    properties.FrameProperties.Add(frameProperties);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting column sections: {ex.Message}");
    }
}

// Extract floor properties
private void ExtractFloorProperties(IModel ramModel, PropertiesContainer properties)
{
    if (properties.Materials.Count == 0)
        return;

    try
    {
        string concreteMaterialId = properties.Materials.Find(m => m.Type.ToLower() == "concrete")?.Id;
        if (string.IsNullOrEmpty(concreteMaterialId))
            return;

        // Extract concrete slab properties
        ExtractConcreteSlabProperties(ramModel, properties, concreteMaterialId);

        // Extract composite deck properties
        ExtractCompositeDeckProperties(ramModel, properties, concreteMaterialId);

        // Extract non-composite deck properties
        ExtractNonCompositeDeckProperties(ramModel, properties);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting floor properties: {ex.Message}");
    }
}

private void ExtractConcreteSlabProperties(IModel ramModel, PropertiesContainer properties, string materialId)
{
    try
    {
        IConcSlabProps concSlabProps = ramModel.GetConcreteSlabProps();
        if (concSlabProps == null)
            return;

        for (int i = 0; i < concSlabProps.GetCount(); i++)
        {
            IConcSlabProp slabProp = concSlabProps.GetAt(i);
            if (slabProp != null)
            {
                var floorProperties = new FloorProperties
                {
                    Name = slabProp.strLabel,
                    Type = "Slab",
                    Thickness = slabProp.dThickness,
                    MaterialId = materialId
                };

                // Add slab-specific properties
                floorProperties.SlabProperties["isRibbed"] = false;
                floorProperties.SlabProperties["isWaffle"] = false;
                floorProperties.SlabProperties["isTwoWay"] = true;

                properties.FloorProperties.Add(floorProperties);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting concrete slab properties: {ex.Message}");
    }
}

private void ExtractCompositeDeckProperties(IModel ramModel, PropertiesContainer properties, string materialId)
{
    try
    {
        ICompDeckProps compDeckProps = ramModel.GetCompositeDeckProps();
        if (compDeckProps == null)
            return;

        for (int i = 0; i < compDeckProps.GetCount(); i++)
        {
            ICompDeckProp deckProp = compDeckProps.GetAt(i);
            if (deckProp != null)
            {
                double totalThickness = deckProp.dToppingThickness + 1.5; // Assume 1.5" deck depth if not available

                var floorProperties = new FloorProperties
                {
                    Name = deckProp.strLabel,
                    Type = "Composite",
                    Thickness = totalThickness,
                    MaterialId = materialId
                };

                // Add deck-specific properties
                floorProperties.DeckProperties["deckType"] = "Composite";
                floorProperties.DeckProperties["deckDepth"] = 1.5; // Default deck depth
                floorProperties.DeckProperties["deckGage"] = 22; // Default gage
                floorProperties.DeckProperties["toppingThickness"] = deckProp.dToppingThickness;

                properties.FloorProperties.Add(floorProperties);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting composite deck properties: {ex.Message}");
    }
}

private void ExtractNonCompositeDeckProperties(IModel ramModel, PropertiesContainer properties)
{
    try
    {
        INonCompDeckProps nonCompDeckProps = ramModel.GetNonCompDeckProps();
        if (nonCompDeckProps == null)
            return;

        string steelMaterialId = properties.Materials.Find(m => m.Type.ToLower() == "steel")?.Id;
        if (string.IsNullOrEmpty(steelMaterialId))
            return;

        for (int i = 0; i < nonCompDeckProps.GetCount(); i++)
        {
            INonCompDeckProp deckProp = nonCompDeckProps.GetAt(i);
            if (deckProp != null)
            {
                var floorProperties = new FloorProperties
                {
                    Name = deckProp.strLabel,
                    Type = "NonComposite",
                    Thickness = deckProp.dEffectiveThickness,
                    MaterialId = steelMaterialId
                };

                // Add deck-specific properties
                floorProperties.DeckProperties["deckType"] = "MetalDeck";
                floorProperties.DeckProperties["deckDepth"] = deckProp.dEffectiveThickness;
                floorProperties.DeckProperties["deckGage"] = 22; // Default gage

                properties.FloorProperties.Add(floorProperties);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting non-composite deck properties: {ex.Message}");
    }
}

// Extract wall properties
private void ExtractWallProperties(IModel ramModel, PropertiesContainer properties)
{
    if (properties.Materials.Count == 0)
        return;

    try
    {
        string concreteMaterialId = properties.Materials.Find(m => m.Type.ToLower() == "concrete")?.Id;
        if (string.IsNullOrEmpty(concreteMaterialId))
            return;

        IWallTypes wallTypes = ramModel.GetWallTypes();
        if (wallTypes == null)
            return;

        for (int i = 0; i < wallTypes.GetCount(); i++)
        {
            IWallType wallType = wallTypes.GetAt(i);
            if (wallType != null)
            {
                var wallProperties = new WallProperties
                {
                    Name = wallType.strLabel,
                    MaterialId = concreteMaterialId,
                    Thickness = wallType.dThickness
                };

                properties.WallProperties.Add(wallProperties);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting wall properties: {ex.Message}");
    }
}

// Extract beams
private void ExtractBeams(IModel ramModel, BaseModel model)
{
    try
    {
        // Create mappings for levels and properties
        Dictionary<int, string> floorTypeToLevelMapping = CreateFloorTypeToLevelMapping(model);
        Dictionary<string, string> sectionToFramePropsMapping = CreateSectionToFramePropsMapping(model);

        // Process each floor type in RAM
        IFloorTypes ramFloorTypes = ramModel.GetFloorTypes();
        if (ramFloorTypes == null || ramFloorTypes.GetCount() == 0)
            return;

        for (int i = 0; i < ramFloorTypes.GetCount(); i++)
        {
            IFloorType ramFloorType = ramFloorTypes.GetAt(i);
            if (ramFloorType == null)
                continue;

            // Get level ID for this floor type
            string levelId = GetLevelIdForFloorType(ramFloorType.lUID, floorTypeToLevelMapping);
            if (string.IsNullOrEmpty(levelId))
                continue;

            // Extract layout beams
            ILayoutBeams layoutBeams = ramFloorType.GetLayoutBeams();
            if (layoutBeams == null)
                continue;

            for (int j = 0; j < layoutBeams.GetCount(); j++)
            {
                ILayoutBeam layoutBeam = layoutBeams.GetAt(j);
                if (layoutBeam == null)
                    continue;

                string sectionName = layoutBeam.strSectionLabel;
                string framePropertiesId = GetFramePropertiesId(sectionName, sectionToFramePropsMapping);

                var beam = new Beam
                {
                    StartPoint = new Point2D(layoutBeam.dXStart, layoutBeam.dYStart),
                    EndPoint = new Point2D(layoutBeam.dXEnd, layoutBeam.dYEnd),
                    LevelId = levelId,
                    FramePropertiesId = framePropertiesId,
                    IsLateral = false // Default to false
                };

                model.Elements.Beams.Add(beam);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting beams: {ex.Message}");
    }
}

// Extract columns
private void ExtractColumns(IModel ramModel, BaseModel model)
{
    try
    {
        // Create mappings for levels and properties
        Dictionary<int, string> floorTypeToLevelMapping = CreateFloorTypeToLevelMapping(model);
        Dictionary<string, string> sectionToFramePropsMapping = CreateSectionToFramePropsMapping(model);

        // Find lowest and highest levels
        string lowestLevelId = null;
        string highestLevelId = null;

        if (model.ModelLayout.Levels.Count > 0)
        {
            var sortedLevels = model.ModelLayout.Levels.OrderBy(l => l.Elevation).ToList();
            lowestLevelId = sortedLevels.First().Id;
            highestLevelId = sortedLevels.Last().Id;
// FloorExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class RAMToFloor : IRAMExporter
    {
        private IModel _model;

        public RAMToFloor(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Group floors by level
            var floorsByLevel = model.Elements.Floors
                .Where(f => !string.IsNullOrEmpty(f.LevelId) && f.Points.Count >= 3)
                .GroupBy(f => f.LevelId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Map levels to floor types
            var levelToFloorType = new Dictionary<string, string>();
            foreach (var level in model.ModelLayout.Levels)
            {
                if (!string.IsNullOrEmpty(level.FloorTypeId))
                {
                    var floorType = model.ModelLayout.FloorTypes
                        .FirstOrDefault(ft => ft.Id == level.FloorTypeId);

                    if (floorType != null)
                    {
                        levelToFloorType[level.Id] = floorType.Name;
                    }
                }
            }

            // Match floor types to RAM floor types
            IFloorTypes ramFloorTypes = _model.GetFloorTypes();
            var floorTypeMap = new Dictionary<string, IFloorType>();

            for (int i = 0; i < ramFloorTypes.GetCount(); i++)
            {
                IFloorType floorType = ramFloorTypes.GetAt(i);
                floorTypeMap[floorType.strLabel] = floorType;
            }

            // Create mapping of floor properties and property types
            var propertyTypeMap = new Dictionary<string, string>();
            var propertyIdMap = new Dictionary<string, int>();

            // Map slab properties
            IConcSlabProps concSlabProps = _model.GetConcreteSlabProps();
            for (int i = 0; i < concSlabProps.GetCount(); i++)
            {
                IConcSlabProp slabProp = concSlabProps.GetAt(i);
                propertyTypeMap[slabProp.strLabel] = "Slab";
                propertyIdMap[slabProp.strLabel] = slabProp.lUID;
            }

            // Map composite deck properties
            ICompDeckProps compDeckProps = _model.GetCompositeDeckProps();
            for (int i = 0; i < compDeckProps.GetCount(); i++)
            {
                ICompDeckProp deckProp = compDeckProps.GetAt(i);
                propertyTypeMap[deckProp.strLabel] = "CompositeDeck";
                propertyIdMap[deckProp.strLabel] = deckProp.lUID;
            }

            // Map non-composite deck properties
            INonCompDeckProps nonCompDeckProps = _model.GetNonCompDeckProps();
            for (int i = 0; i < nonCompDeckProps.GetCount(); i++)
            {
                INonCompDeckProp nonCompDeckProp = nonCompDeckProps.GetAt(i);
                propertyTypeMap[nonCompDeckProp.strLabel] = "NonCompositeDeck";
                propertyIdMap[nonCompDeckProp.strLabel] = nonCompDeckProp.lUID;
            }

            // Export floors
            foreach (var levelId in floorsByLevel.Keys)
            {
                // Find corresponding floor type
                if (!levelToFloorType.TryGetValue(levelId, out string floorTypeName) ||
                    !floorTypeMap.TryGetValue(floorTypeName, out IFloorType floorType))
                {
                    Console.WriteLine($"Could not find floor type for level {levelId}");
                    continue;
                }

                // Export floors for this level
                foreach (var floor in floorsByLevel[levelId])
                {
                    try
                    {
                        // Find floor property
                        string propertyName = null;
                        string propertyType = "Slab"; // Default to slab
                        int propertyId = -1;

                        if (!string.IsNullOrEmpty(floor.FloorPropertiesId))
                        {
                            var floorProp = model.Properties.FloorProperties
                                .FirstOrDefault(fp => fp.Id == floor.FloorPropertiesId);

                            if (floorProp != null)
                            {
                                propertyName = floorProp.Name;

                                if (propertyTypeMap.ContainsKey(propertyName))
                                {
                                    propertyType = propertyTypeMap[propertyName];
                                    propertyId = propertyIdMap[propertyName];
                                }
                            }
                        }

                        // Skip if property not found
                        if (propertyId < 0)
                        {
                            Console.WriteLine($"Could not find property for floor {floor.Id}");
                            continue;
                        }

                        // Get surface loads
                        ISurfaceLoadPropertySets surfaceLoadProps = _model.GetSurfaceLoadPropertySets();
                        int surfaceLoadId = -1;

                        if (!string.IsNullOrEmpty(floor.SurfaceLoadId))
                        {
                            var surfaceLoad = model.Loads.SurfaceLoads
                                .FirstOrDefault(sl => sl.Id == floor.SurfaceLoadId);

                            if (surfaceLoad != null)
                            {
                                // Generate name for surface load
                                string loadName = $"SurfLoad_{surfaceLoad.Id.Split('-').Last()}";

                                // Find surface load ID
                                for (int i = 0; i < surfaceLoadProps.GetCount(); i++)
                                {
                                    ISurfaceLoadPropertySet loadProp = surfaceLoadProps.GetAt(i);
                                    if (loadProp.strLabel == loadName)
                                    // FloorExporter.cs (continued)
                                    {
                                        surfaceLoadId = loadProp.lUID;
                                        break;
                                    }
                                }
                            }
                        }

                        // Create RAM floor based on property type
                        if (propertyType == "Slab")
                        {
                            // Get layout slabs
                            ILayoutSlabs slabs = floorType.GetLayoutSlabs();

                            // Convert points to polygon
                            DAArray polygon = new DAArray();
                            foreach (var point in floor.Points)
                            {
                                polygon.Add(point.X * 12, polygon.GetCount()); // Convert to inches
                                polygon.Add(point.Y * 12, polygon.GetCount());
                            }

                            // Add slab with appropriate properties
                            ILayoutSlab slab = slabs.AddWithPolygon(propertyId, polygon, surfaceLoadId, false);
                        }
                        else if (propertyType == "CompositeDeck" || propertyType == "NonCompositeDeck")
                        {
                            // Get layout decks
                            ILayoutDecks decks = floorType.GetLayoutDecks();

                            // Convert points to polygon
                            DAArray polygon = new DAArray();
                            foreach (var point in floor.Points)
                            {
                                polygon.Add(point.X * 12, polygon.GetCount()); // Convert to inches
                                polygon.Add(point.Y * 12, polygon.GetCount());
                            }

                            // Add deck with appropriate properties
                            ILayoutDeck deck = null;
                            if (propertyType == "CompositeDeck")
                            {
                                deck = decks.AddCompositeWithPolygon(propertyId, polygon, surfaceLoadId, false);
                            }
                            else
                            {
                                deck = decks.AddNonCompositeWithPolygon(propertyId, polygon, surfaceLoadId, false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error exporting floor {floor.Id}: {ex.Message}");
                    }
                }
            }
        }
    }
}
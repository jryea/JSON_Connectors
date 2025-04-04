// FloorImporter.cs
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Utilities;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.ToRAM.Elements
{
    public class FloorToRAM : IRAMImporter<List<Floor>>
    {
        private IModel _model;
        private Dictionary<int, string> _floorTypeIdMap;
        private Dictionary<int, string> _floorPropertyIdMap;

        public FloorToRAM(IModel model, Dictionary<int, string> floorTypeIdMap, Dictionary<int, string> floorPropertyIdMap)
        {
            _model = model;
            _floorTypeIdMap = floorTypeIdMap;
            _floorPropertyIdMap = floorPropertyIdMap;
        }

        public List<Floor> Import()
        {
            var floors = new List<Floor>();

            try
            {
                // Get floor types
                IFloorTypes floorTypes = _model.GetFloorTypes();

                for (int i = 0; i < floorTypes.GetCount(); i++)
                {
                    IFloorType floorType = floorTypes.GetAt(i);

                    // Skip if we don't have a mapping for this floor type
                    if (!_floorTypeIdMap.ContainsKey(floorType.lUID))
                        continue;

                    string floorTypeId = _floorTypeIdMap[floorType.lUID];

                    // Import layout slabs
                    ImportLayoutSlabs(floors, floorType, floorTypeId);

                    // Import layout decks
                    ImportLayoutDecks(floors, floorType, floorTypeId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing floors: {ex.Message}");
            }

            return floors;
        }

        private void ImportLayoutSlabs(List<Floor> floors, IFloorType floorType, string floorTypeId)
        {
            // Get layout slabs
            ILayoutSlabs layoutSlabs = floorType.GetLayoutSlabs();

            for (int j = 0; j < layoutSlabs.GetCount(); j++)
            {
                ILayoutSlab layoutSlab = layoutSlabs.GetAt(j);

                try
                {
                    // Get floor polygon points
                    var points = ExtractPolygonPoints(layoutSlab.GetPolygon());

                    if (points.Count < 3)
                    {
                        Console.WriteLine($"Skipping slab with less than 3 points in floor type {floorType.strLabel}");
                        continue;
                    }

                    // Get floor property ID
                    string floorPropertiesId = null;
                    if (_floorPropertyIdMap.ContainsKey(layoutSlab.lSlabPropId))
                    {
                        floorPropertiesId = _floorPropertyIdMap[layoutSlab.lSlabPropId];
                    }

                    // Create a floor
                    var floor = new Floor
                    {
                        Id = IdGenerator.Generate(IdGenerator.Elements.FLOOR),
                        Points = points,
                        LevelId = floorTypeId,
                        FloorPropertiesId = floorPropertiesId
                    };

                    // Add diaphragm information (assume rigid for now)
                    floor.DiaphragmId = GetRigidDiaphragmId();

                    // Add surface load information if available
                    if (layoutSlab.lSurfaceLoadId > 0)
                    {
                        floor.SurfaceLoadId = IdGenerator.Generate(IdGenerator.Loads.SURFACE_LOAD);
                    }

                    floors.Add(floor);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error importing layout slab {j} from floor type {floorType.strLabel}: {ex.Message}");
                }
            }
        }

        private void ImportLayoutDecks(List<Floor> floors, IFloorType floorType, string floorTypeId)
        {
            // Get layout decks
            ILayoutDecks layoutDecks = floorType.GetLayoutDecks();

            for (int j = 0; j < layoutDecks.GetCount(); j++)
            {
                ILayoutDeck layoutDeck = layoutDecks.GetAt(j);

                try
                {
                    // Get floor polygon points
                    var points = ExtractPolygonPoints(layoutDeck.GetPolygon());

                    if (points.Count < 3)
                    {
                        Console.WriteLine($"Skipping deck with less than 3 points in floor type {floorType.strLabel}");
                        continue;
                    }

                    // Get floor property ID based on deck type
                    string floorPropertiesId = null;
                    if (layoutDeck.bIsComposite)
                    {
                        // Use composite deck property
                        if (_floorPropertyIdMap.ContainsKey(layoutDeck.lCompositeDeckPropId))
                        {
                            floorPropertiesId = _floorPropertyIdMap[layoutDeck.lCompositeDeckPropId];
                        }
                    }
                    else
                    {
                        // Use non-composite deck property
                        if (_floorPropertyIdMap.ContainsKey(layoutDeck.lNonCompositeDeckPropId))
                        {
                            floorPropertiesId = _floorPropertyIdMap[layoutDeck.lNonCompositeDeckPropId];
                        }
                    }

                    // Create a floor
                    var floor = new Floor
                    {
                        Id = IdGenerator.Generate(IdGenerator.Elements.FLOOR),
                        Points = points,
                        LevelId = floorTypeId,
                        FloorPropertiesId = floorPropertiesId
                    };

                    // Add diaphragm information (assume rigid for now)
                    floor.DiaphragmId = GetRig
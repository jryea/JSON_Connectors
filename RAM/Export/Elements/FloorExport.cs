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
    public class FloorExport
    {
        private IModel _model;
        private string _lengthUnit;

        public FloorExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public List<Floor> Export()
        {
            var floors = new List<Floor>();
            Console.WriteLine("Starting Floor export from RAM");

            try
            {
                IFloorTypes floorTypes = _model.GetFloorTypes();
                if (floorTypes == null || floorTypes.GetCount() == 0)
                {
                    Console.WriteLine("ERROR: No floor types found in RAM model");
                    return floors;
                }

                Console.WriteLine($"Found {floorTypes.GetCount()} floor types in RAM model");

                // Process each floor type
                for (int i = 0; i < floorTypes.GetCount(); i++)
                {
                    IFloorType floorType = floorTypes.GetAt(i);
                    if (floorType == null)
                    {
                        Console.WriteLine($"ERROR: Floor type at index {i} is null");
                        continue;
                    }

                    Console.WriteLine($"Processing floor type: {floorType.strLabel} (UID: {floorType.lUID})");

                    // Get decks for this floor type
                    IDecks decks = floorType.GetDecks();
                    if (decks == null || decks.GetCount() == 0)
                    {
                        Console.WriteLine($"No decks found for floor type {floorType.strLabel}");
                        continue;
                    }

                    Console.WriteLine($"Found {decks.GetCount()} deck(s) for floor type {floorType.strLabel}");

                    // Find stories that use this floor type
                    IStories ramStories = _model.GetStories();
                    if (ramStories == null || ramStories.GetCount() == 0)
                    {
                        Console.WriteLine("No stories found in RAM model");
                        continue;
                    }

                    bool foundMatchingStory = false;

                    // Check each story to see if it uses this floor type
                    for (int storyIdx = 0; storyIdx < ramStories.GetCount(); storyIdx++)
                    {
                        IStory ramStory = ramStories.GetAt(storyIdx);
                        if (ramStory == null || ramStory.GetFloorType() == null)
                            continue;

                        if (ramStory.GetFloorType().lUID != floorType.lUID)
                            continue;

                        Console.WriteLine($"Found matching story: {ramStory.strLabel} (UID: {ramStory.lUID}) using floor type {floorType.strLabel}");
                        foundMatchingStory = true;

                        // Find the corresponding level ID for this story using ModelMappingUtility
                        string levelId = ModelMappingUtility.GetLevelIdForStoryUid(ramStory.lUID.ToString());
                        if (string.IsNullOrEmpty(levelId))
                        {
                            Console.WriteLine($"ERROR: No level mapping found for story {ramStory.strLabel} (UID: {ramStory.lUID})");
                            continue;
                        }

                        Console.WriteLine($"Found level ID for story {ramStory.strLabel}: {levelId}");

                        // Process each deck in this floor type
                        for (int deckIdx = 0; deckIdx < decks.GetCount(); deckIdx++)
                        {
                            IDeck deck = decks.GetAt(deckIdx);
                            if (deck == null)
                            {
                                Console.WriteLine($"ERROR: Deck at index {deckIdx} is null");
                                continue;
                            }

                            Console.WriteLine($"Processing deck with property ID: {deck.lUID}");

                            // Use ModelMappingUtility to get FloorProperties ID from deck property UID
                            string floorPropertiesId = ModelMappingUtility.GetFloorPropertiesIdForUid(deck.lPropID.ToString());


                            if (string.IsNullOrEmpty(floorPropertiesId))
                            {
                                Console.WriteLine($"ERROR: No FloorProperties mapping found for deck property UID {deck.lUID}");
                                continue;
                            }

                            Console.WriteLine($"Found FloorProperties ID: {floorPropertiesId}");

                            // Get deck geometry points
                            IPoints deckPoints = deck.GetPoints();
                            if (deckPoints == null || deckPoints.GetCount() < 3)
                            {
                                Console.WriteLine($"Deck has insufficient points ({deckPoints?.GetCount() ?? 0})");
                                continue;
                            }

                            // Convert deck points to model coordinates
                            var floorPoints = new List<Point2D>();
                            for (int ptIdx = 0; ptIdx < deckPoints.GetCount(); ptIdx++)
                            {
                                IPoint deckPoint = deckPoints.GetAt(ptIdx);

                                // Get the coordinates
                                SCoordinate coord = new SCoordinate();
                                deckPoint.GetCoordinate(ref coord);

                                var point = new Point2D(
                                    UnitConversionUtils.ConvertFromInches(coord.dXLoc, _lengthUnit),
                                    UnitConversionUtils.ConvertFromInches(coord.dYLoc, _lengthUnit)
                                );
                                floorPoints.Add(point);
                            }

                            // Create floor object
                            var floor = new Floor
                            {
                                Id = IdGenerator.Generate(IdGenerator.Elements.FLOOR),
                                LevelId = levelId,
                                FloorPropertiesId = floorPropertiesId,
                                Points = floorPoints,
                                SpanDirection = deck.dAngle
                            };

                            floors.Add(floor);
                            Console.WriteLine($"Created floor {floor.Id} with {floorPoints.Count} points on level {levelId}");
                        }
                    }

                    if (!foundMatchingStory)
                    {
                        Console.WriteLine($"No stories found using floor type {floorType.strLabel}");
                    }
                }

                Console.WriteLine($"Successfully created {floors.Count} floors.");
                return floors;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR exporting floors from RAM: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return floors;
            }
        }
    }
}
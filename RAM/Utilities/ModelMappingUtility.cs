using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.ModelLayout;
using RAMDATAACCESSLib;

namespace RAM.Utilities
{
    /// <summary>
    /// Static utility for managing mappings between RAM and Core model entities
    /// </summary>
    public static class ModelMappingUtility
    {
        // Static mapping collections
        private static Dictionary<string, string> _levelIdToStoryUid = new Dictionary<string, string>();
        private static Dictionary<string, string> _storyUidToLevelId = new Dictionary<string, string>();
        private static Dictionary<string, string> _levelNameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _storyNameToUid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _floorTypeUidToId = new Dictionary<string, string>();
        private static Dictionary<string, string> _sectionLabelToFramePropId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _floorPropUidToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static string _groundLevelId = null;

        /// <summary>
        /// Initialize all mappings between RAM and Core models
        /// </summary>
        public static void InitializeMappings(IModel ramModel, BaseModel coreModel)
        {
            try
            {
                // Clear existing mappings
                ClearAllMappings();

                // Build level/story mappings
                if (coreModel?.ModelLayout?.Levels != null)
                {
                    BuildLevelMappings(ramModel, coreModel.ModelLayout.Levels);
                }

                // Build floor type mappings
                if (coreModel?.ModelLayout?.FloorTypes != null)
                {
                    BuildFloorTypeMappings(ramModel, coreModel.ModelLayout.FloorTypes, coreModel.ModelLayout.Levels);
                }

                // Set frame property mappings if available
                if (coreModel?.Properties?.FrameProperties != null)
                {
                    Dictionary<string, string> framePropMappings = new Dictionary<string, string>();
                    foreach (var prop in coreModel.Properties.FrameProperties)
                    {
                        if (!string.IsNullOrEmpty(prop.Name) && !string.IsNullOrEmpty(prop.Id))
                        {
                            framePropMappings[prop.Name] = prop.Id;
                        }
                    }
                    SetFramePropertyMappings(framePropMappings);
                }

                // Set floor properties mappings if available
                if (coreModel?.Properties?.FloorProperties != null)
                {
                    Dictionary<string, string> floorPropMappings = new Dictionary<string, string>();
                    foreach (var prop in coreModel.Properties.FloorProperties)
                    {
                        if (!string.IsNullOrEmpty(prop.Name) && !string.IsNullOrEmpty(prop.Id))
                        {
                            floorPropMappings[prop.Name] = prop.Id;
                        }
                    }
                    SetFloorPropertiesMappings(floorPropMappings);
                }

                Console.WriteLine("Mappings initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing mappings: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear all mappings
        /// </summary>
        public static void ClearAllMappings()
        {
            _levelIdToStoryUid.Clear();
            _storyUidToLevelId.Clear();
            _levelNameToId.Clear();
            _storyNameToUid.Clear();
            _floorTypeUidToId.Clear();
            _sectionLabelToFramePropId.Clear();
            _groundLevelId = null;

            Console.WriteLine("All mappings cleared");
        }

        /// <summary>
        /// Build mappings between RAM stories and Core levels
        /// </summary>
        private static void BuildLevelMappings(IModel ramModel, IEnumerable<Level> levels)
        {
            var ramStories = ramModel.GetStories();
            Console.WriteLine("Building level mappings...");

            // Build name-to-ID mappings for quick lookup
            foreach (var level in levels)
            {
                if (!string.IsNullOrEmpty(level.Name) && !string.IsNullOrEmpty(level.Id))
                {
                    _levelNameToId[level.Name] = level.Id;
                    _levelNameToId[NormalizeName(level.Name)] = level.Id;

                    // Store ground level ID (elevation near 0)
                    if (Math.Abs(level.Elevation) < 0.01 && string.IsNullOrEmpty(_groundLevelId))
                    {
                        _groundLevelId = level.Id;
                        Console.WriteLine($"Found ground level: {level.Name} (ID: {level.Id})");
                    }
                }
            }

            // Build story name mappings
            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory story = ramStories.GetAt(i);
                if (story != null)
                {
                    _storyNameToUid[story.strLabel] = story.lUID.ToString();
                    _storyNameToUid[NormalizeName(story.strLabel)] = story.lUID.ToString();
                }
            }

            // Primary mapping approach: Map by elevation (most reliable)
            foreach (var level in levels)
            {
                bool mapped = false;

                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory story = ramStories.GetAt(i);
                    if (Math.Abs(level.Elevation - story.dElevation) < 0.01)
                    {
                        _levelIdToStoryUid[level.Id] = story.lUID.ToString();
                        _storyUidToLevelId[story.lUID.ToString()] = level.Id;
                        Console.WriteLine($"Mapped level {level.Name} to story {story.strLabel} by elevation");
                        mapped = true;
                        break;
                    }
                }

                // If not mapped by elevation, try by name
                if (!mapped)
                {
                    string normalizedLevelName = NormalizeName(level.Name);
                    for (int i = 0; i < ramStories.GetCount(); i++)
                    {
                        IStory story = ramStories.GetAt(i);
                        string normalizedStoryName = NormalizeName(story.strLabel);

                        if (normalizedLevelName == normalizedStoryName)
                        {
                            _levelIdToStoryUid[level.Id] = story.lUID.ToString();
                            _storyUidToLevelId[story.lUID.ToString()] = level.Id;
                            Console.WriteLine($"Mapped level {level.Name} to story {story.strLabel} by name");
                            mapped = true;
                            break;
                        }
                    }
                }

                if (!mapped)
                {
                    Console.WriteLine($"Warning: No mapping found for level {level.Name}");
                }
            }

            // If ground level not found by elevation, try to find by name
            if (string.IsNullOrEmpty(_groundLevelId))
            {
                SetGroundLevelByName(levels);
            }

            LogLevelMappings();
        }

        /// <summary>
        /// Build mappings between RAM floor types and Core floor types
        /// </summary>
        private static void BuildFloorTypeMappings(IModel ramModel, IEnumerable<FloorType> floorTypes, IEnumerable<Level> levels = null)
        {
            var ramFloorTypes = ramModel.GetFloorTypes();
            _floorTypeUidToId.Clear();

            Console.WriteLine("Building floor type mappings...");

            // 1. First try to match by name (most reliable approach)
            Dictionary<string, IFloorType> matchedRamFloorTypes = new Dictionary<string, IFloorType>();

            for (int i = 0; i < ramFloorTypes.GetCount(); i++)
            {
                IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                if (ramFloorType != null && !string.IsNullOrEmpty(ramFloorType.strLabel))
                {
                    // Try to find matching core floor type by name
                    var matchingFloorType = floorTypes.FirstOrDefault(ft =>
                        string.Equals(ft.Name, ramFloorType.strLabel, StringComparison.OrdinalIgnoreCase));

                    if (matchingFloorType != null)
                    {
                        _floorTypeUidToId[ramFloorType.lUID.ToString()] = matchingFloorType.Id;
                        matchedRamFloorTypes[ramFloorType.strLabel] = ramFloorType;
                        Console.WriteLine($"Matched floor type by name: {matchingFloorType.Name} (ID: {matchingFloorType.Id}) -> RAM ID: {ramFloorType.lUID}");
                    }
                }
            }

            // 2. For remaining unmatched floor types, try matching by elevation order if levels are provided
            if (levels != null && levels.Any())
            {
                // Find floor types that haven't been matched yet
                var unmatchedFloorTypes = floorTypes.Where(ft =>
                    !_floorTypeUidToId.Values.Contains(ft.Id)).ToList();

                if (unmatchedFloorTypes.Any())
                {
                    // Find RAM floor types that haven't been matched yet
                    var unmatchedRamFloorTypes = new List<IFloorType>();
                    for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                    {
                        IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                        if (ramFloorType != null && !matchedRamFloorTypes.Values.Contains(ramFloorType))
                        {
                            unmatchedRamFloorTypes.Add(ramFloorType);
                        }
                    }

                    // Get average elevation for each floor type
                    var floorTypeElevations = new Dictionary<string, double>();
                    var floorTypeLevelCount = new Dictionary<string, int>();

                    foreach (var level in levels)
                    {
                        if (!string.IsNullOrEmpty(level.FloorTypeId))
                        {
                            if (!floorTypeElevations.ContainsKey(level.FloorTypeId))
                            {
                                floorTypeElevations[level.FloorTypeId] = 0;
                                floorTypeLevelCount[level.FloorTypeId] = 0;
                            }

                            floorTypeElevations[level.FloorTypeId] += level.Elevation;
                            floorTypeLevelCount[level.FloorTypeId]++;
                        }
                    }

                    // Calculate average elevations
                    foreach (var ftId in floorTypeElevations.Keys.ToList())
                    {
                        if (floorTypeLevelCount[ftId] > 0)
                        {
                            floorTypeElevations[ftId] /= floorTypeLevelCount[ftId];
                        }
                    }

                    // Sort unmatched floor types by elevation
                    var sortedUnmatchedFloorTypes = unmatchedFloorTypes
                        .Where(ft => floorTypeElevations.ContainsKey(ft.Id))
                        .OrderBy(ft => floorTypeElevations[ft.Id])
                        .ToList();

                    // Match remaining floor types in elevation order
                    int count = Math.Min(sortedUnmatchedFloorTypes.Count, unmatchedRamFloorTypes.Count);
                    for (int i = 0; i < count; i++)
                    {
                        var floorType = sortedUnmatchedFloorTypes[i];
                        var ramFloorType = unmatchedRamFloorTypes[i];

                        _floorTypeUidToId[ramFloorType.lUID.ToString()] = floorType.Id;
                        Console.WriteLine($"Matched floor type by elevation: {floorType.Name} (ID: {floorType.Id}) -> RAM ID: {ramFloorType.lUID}");
                    }
                }
            }
        }

        // Get the RAM Floor Type UID for a Core Floor Type ID
        public static string GetRamFloorTypeUidForFloorTypeId(string floorTypeId)
        {
            if (string.IsNullOrEmpty(floorTypeId))
                return null;

            // Search for the floor type ID in our existing mapping
            foreach (var kvp in _floorTypeUidToId)
            {
                if (kvp.Value == floorTypeId)
                    return kvp.Key;
            }

            return null;
        }

        /// <summary>
        /// Set frame property mappings
        /// </summary>
        public static void SetFramePropertyMappings(Dictionary<string, string> sectionToFramePropId)
        {
            _sectionLabelToFramePropId.Clear();
            foreach (var mapping in sectionToFramePropId)
            {
                _sectionLabelToFramePropId[mapping.Key] = mapping.Value;
            }
            Console.WriteLine($"Set {_sectionLabelToFramePropId.Count} frame property mappings");
        }

        /// <summary>
        /// Try to find the ground level from level names
        /// </summary>
        private static void SetGroundLevelByName(IEnumerable<Level> levels)
        {
            // Look for level with name containing "ground", "base", or "0"
            foreach (var level in levels)
            {
                string name = level.Name.ToLower();
                if (name.Contains("ground") || name.Contains("base") || name == "0")
                {
                    _groundLevelId = level.Id;
                    Console.WriteLine($"Found ground level by name: {level.Name} (ID: {level.Id})");
                    return;
                }
            }

            // If still not found, use the lowest level
            if (string.IsNullOrEmpty(_groundLevelId) && levels.Any())
            {
                var lowestLevel = levels.OrderBy(l => l.Elevation).First();
                _groundLevelId = lowestLevel.Id;
                Console.WriteLine($"Using lowest level as ground level: {lowestLevel.Name} (ID: {lowestLevel.Id})");
            }
        }

        /// <summary>
        /// Normalize a name by removing "Story" prefix and trimming
        /// </summary>
        public static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            name = name.Trim();

            if (name.StartsWith("Story ", StringComparison.OrdinalIgnoreCase))
                return name.Substring(6).Trim();

            if (name.StartsWith("Story", StringComparison.OrdinalIgnoreCase))
                return name.Substring(5).Trim();

            return name;
        }

        /// <summary>
        /// Log all level mappings to console for debugging
        /// </summary>
        private static void LogLevelMappings()
        {
            Console.WriteLine("=== Level to Story Mappings ===");
            foreach (var mapping in _levelIdToStoryUid)
            {
                Console.WriteLine($"Level ID: {mapping.Key} -> Story UID: {mapping.Value}");
            }

            Console.WriteLine($"Ground Level ID: {_groundLevelId ?? "Not Found"}");
        }

        #region Public Mapping Methods

        /// <summary>
        /// Get the RAM Story UID for a Core Level ID
        /// </summary>
        public static string GetStoryUidForLevelId(string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
                return null;

            return _levelIdToStoryUid.TryGetValue(levelId, out string storyUid) ? storyUid : null;
        }

        /// <summary>
        /// Get the Core Level ID for a RAM Story UID
        /// </summary>
        public static string GetLevelIdForStoryUid(string storyUid)
        {
            if (string.IsNullOrEmpty(storyUid))
                return null;

            return _storyUidToLevelId.TryGetValue(storyUid, out string levelId) ? levelId : null;
        }

        /// <summary>
        /// Get the Core Level ID for a RAM Story name
        /// </summary>
        public static string GetLevelIdForStoryName(string storyName)
        {
            if (string.IsNullOrEmpty(storyName))
                return null;

            // Try direct lookup
            if (_storyNameToUid.TryGetValue(storyName, out string storyUid))
            {
                return GetLevelIdForStoryUid(storyUid);
            }

            // Try normalized name
            string normalizedName = NormalizeName(storyName);
            if (_storyNameToUid.TryGetValue(normalizedName, out storyUid))
            {
                return GetLevelIdForStoryUid(storyUid);
            }

            return null;
        }

        /// <summary>
        /// Get the Core Level ID for a level name
        /// </summary>
        public static string GetLevelIdForName(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                return null;

            return _levelNameToId.TryGetValue(levelName, out string levelId) ? levelId : null;
        }

        /// <summary>
        /// Get the ground/foundation level ID
        /// </summary>
        public static string GetGroundLevelId()
        {
            return _groundLevelId;
        }

        /// <summary>
        /// Get the Core Level ID for the level below the specified level
        /// </summary>
        public static string GetBaseLevelIdForTopLevelId(string topLevelId, IModel ramModel)
        {
            if (string.IsNullOrEmpty(topLevelId))
                return _groundLevelId; // Default to ground level

            // Get the story UID for the top level
            string topStoryUid = GetStoryUidForLevelId(topLevelId);
            if (string.IsNullOrEmpty(topStoryUid))
                return _groundLevelId; // Default to ground level

            // Find the story
            IStories ramStories = ramModel.GetStories();
            IStory topStory = null;
            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory story = ramStories.GetAt(i);
                if (story != null && story.lUID.ToString() == topStoryUid)
                {
                    topStory = story;
                    break;
                }
            }

            if (topStory == null)
                return _groundLevelId; // Default to ground level

            // Find the story below this one
            IStory belowStory = null;
            double maxElevation = double.MinValue;

            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory story = ramStories.GetAt(i);
                if (story != null && story.dElevation < topStory.dElevation &&
                    story.dElevation > maxElevation)
                {
                    belowStory = story;
                    maxElevation = story.dElevation;
                }
            }

            // Get the level ID for the story below
            if (belowStory != null)
            {
                string baseLevelId = GetLevelIdForStoryUid(belowStory.lUID.ToString());
                if (!string.IsNullOrEmpty(baseLevelId))
                    return baseLevelId;
            }

            // If no level below found, return the ground level
            return _groundLevelId;
        }

        /// <summary>
        /// Get the Core Floor Type ID for a RAM Floor Type UID
        /// </summary>
        public static string GetFloorTypeIdForUid(string floorTypeUid)
        {
            if (string.IsNullOrEmpty(floorTypeUid))
                return null;

            return _floorTypeUidToId.TryGetValue(floorTypeUid, out string floorTypeId) ? floorTypeId : null;
        }

        /// <summary>
        /// Get the Core Frame Property ID for a RAM Section Label
        /// </summary>
        public static string GetFramePropertyIdForSectionLabel(string sectionLabel)
        {
            if (string.IsNullOrEmpty(sectionLabel))
                return _sectionLabelToFramePropId.Values.FirstOrDefault();

            // Try direct lookup
            if (_sectionLabelToFramePropId.TryGetValue(sectionLabel, out string framePropertyId))
            {
                return framePropertyId;
            }

            // Return first frame property ID as fallback
            return _sectionLabelToFramePropId.Values.FirstOrDefault();
        }

        public static Dictionary<string, string> CreateLevelToFloorTypeMapping(IEnumerable<Level> levels)
        {
            var mapping = new Dictionary<string, string>();

            foreach (var level in levels)
            {
                if (!string.IsNullOrEmpty(level.Id) && !string.IsNullOrEmpty(level.FloorTypeId))
                {
                    mapping[level.Id] = level.FloorTypeId;
                }
            }

            return mapping;
        }

        // Get the Core Level ID for the wall's top level
        public static string FindTopLevelIdForWall(IWall wall, IStory currentStory)
        {
            if (wall == null || currentStory == null)
                return null;

            // Get the current story's level ID
            string storyUid = currentStory.lUID.ToString();
            return GetLevelIdForStoryUid(storyUid);
        }

        // Get the Core Wall Property ID for a RAM Wall
        public static string FindWallPropertiesId(IWall wall, Dictionary<string, string> wallPropMappings)
        {
            if (wall == null || wallPropMappings == null || wallPropMappings.Count == 0)
                return null;

            // Try to find wall property by thickness
            double thickness = wall.dThickness;

            // Look for a wall property with matching thickness
            foreach (var entry in wallPropMappings)
            {
                // This is a simplified approach - in a real implementation,
                // you would need to retrieve the actual wall properties and compare
                if (entry.Key.Contains(thickness.ToString("0.##")))
                    return entry.Value;
            }

            // Return first wall property ID as fallback
            return wallPropMappings.Values.FirstOrDefault();
        }

        public static void SetWallPropertyMappings(Dictionary<string, string> wallPropMappings)
        {
            if (wallPropMappings == null)
                return;

            var _wallPropMappings = new Dictionary<string, string>();
            foreach (var mapping in wallPropMappings)
            {
                _wallPropMappings[mapping.Key] = mapping.Value;
            }

            Console.WriteLine($"Set {_wallPropMappings.Count} wall property mappings");
        }

        // Get a mapping of RAM Sections to Core Frame Properties
        public static Dictionary<string, string> GetFramePropertyMappings(IModel ramModel, IEnumerable<Core.Models.Properties.FrameProperties> frameProperties)
        {
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (frameProperties == null)
                return mappings;

            foreach (var frameProp in frameProperties)
            {
                if (!string.IsNullOrEmpty(frameProp.Name) && !string.IsNullOrEmpty(frameProp.Id))
                {
                    mappings[frameProp.Name] = frameProp.Id;
                }
            }

            return mappings;
        }

        // Add to ModelMappingUtility class
        private static Dictionary<double, string> _wallThicknessToId = new Dictionary<double, string>();

        // Set mapping from wall thickness to wall property ID
        public static void SetWallThicknessMapping(Dictionary<double, string> thicknessToIdMapping)
        {
            _wallThicknessToId.Clear();
            foreach (var mapping in thicknessToIdMapping)
            {
                _wallThicknessToId[mapping.Key] = mapping.Value;
            }
            Console.WriteLine($"Set {_wallThicknessToId.Count} wall thickness mappings");
        }

        // Get wall property ID for a given thickness
        public static string GetWallPropertyIdForThickness(double thickness, double tolerance = 0.01)
        {
            // Try to find an exact match first
            if (_wallThicknessToId.TryGetValue(thickness, out string propId))
                return propId;

            // If no exact match, look for one within tolerance
            foreach (var kvp in _wallThicknessToId)
            {
                if (Math.Abs(kvp.Key - thickness) < tolerance)
                    return kvp.Value;
            }

            // Return first available as fallback
            return _wallThicknessToId.Values.FirstOrDefault();
        }

        // Get default frame property ID when no specific match is found
        public static string GetDefaultFramePropertyId()
        {
            // Return first frame property ID if available
            return _sectionLabelToFramePropId.Values.FirstOrDefault();
        }

        public static void SetFloorPropertiesMappings(Dictionary<string, string> uidToFloorPropId)
        {
            _floorPropUidToId.Clear();
            foreach (var mapping in uidToFloorPropId)
            {
                _floorPropUidToId[mapping.Key] = mapping.Value;
            }
            Console.WriteLine($"Set {_floorPropUidToId.Count} floor properties mappings");
        }

        /// <summary>
        /// Get the Core FloorProperties ID for a RAM deck property UID
        /// </summary>
        public static string GetFloorPropertiesIdForUid(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return _floorPropUidToId.Values.FirstOrDefault();

            // Try direct lookup
            if (_floorPropUidToId.TryGetValue(uid, out string floorPropertiesId))
            {
                return floorPropertiesId;
            }

            // Return first floor properties ID as fallback
            return _floorPropUidToId.Values.FirstOrDefault();
        }

        public static void DumpMappingState()
        {
            Console.WriteLine("\n=== MAPPING UTILITY STATE ===");

            // Dump level to story mappings
            Console.WriteLine("\nLevel ID to Story UID Mappings:");
            Console.WriteLine("{0,-40} {1,-10}", "Level ID", "Story UID");
            Console.WriteLine(new string('-', 50));
            foreach (var mapping in _levelIdToStoryUid)
            {
                Console.WriteLine("{0,-40} {1,-10}", mapping.Key, mapping.Value);
            }

            // Dump story to level mappings
            Console.WriteLine("\nStory UID to Level ID Mappings:");
            Console.WriteLine("{0,-10} {1,-40}", "Story UID", "Level ID");
            Console.WriteLine(new string('-', 50));
            foreach (var mapping in _storyUidToLevelId)
            {
                Console.WriteLine("{0,-10} {1,-40}", mapping.Key, mapping.Value);
            }

            // Dump level name mappings
            Console.WriteLine("\nLevel Name to ID Mappings:");
            Console.WriteLine("{0,-30} {1,-40}", "Level Name", "Level ID");
            Console.WriteLine(new string('-', 70));
            foreach (var mapping in _levelNameToId)
            {
                Console.WriteLine("{0,-30} {1,-40}", mapping.Key, mapping.Value);
            }

            // Dump floor type mappings
            Console.WriteLine("\nFloor Type UID to ID Mappings:");
            Console.WriteLine("{0,-10} {1,-40}", "Floor UID", "Floor Type ID");
            Console.WriteLine(new string('-', 50));
            foreach (var mapping in _floorTypeUidToId)
            {
                Console.WriteLine("{0,-10} {1,-40}", mapping.Key, mapping.Value);
            }

            // Dump frame property mappings
            Console.WriteLine("\nSection Label to Frame Property ID Mappings:");
            Console.WriteLine("{0,-30} {1,-40}", "Section Label", "Frame Prop ID");
            Console.WriteLine(new string('-', 70));
            foreach (var mapping in _sectionLabelToFramePropId)
            {
                Console.WriteLine("{0,-30} {1,-40}", mapping.Key, mapping.Value);
            }

            // Dump wall thickness mappings
            Console.WriteLine("\nWall Thickness to Property ID Mappings:");
            Console.WriteLine("{0,-15} {1,-40}", "Thickness", "Wall Prop ID");
            Console.WriteLine(new string('-', 55));
            foreach (var mapping in _wallThicknessToId)
            {
                Console.WriteLine("{0,-15} {1,-40}", mapping.Key, mapping.Value);
            }

            Console.WriteLine("\nGround Level ID: " + (_groundLevelId ?? "Not set"));
            Console.WriteLine("=== END MAPPING STATE ===\n");
        }

        #endregion
    }
}
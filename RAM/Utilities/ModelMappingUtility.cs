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
        #region Private Static Fields

        // Static mapping collections
        private static Dictionary<string, string> _levelIdToStoryUid = new Dictionary<string, string>();
        private static Dictionary<string, string> _storyUidToLevelId = new Dictionary<string, string>();
        private static Dictionary<string, string> _levelNameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _storyNameToUid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _floorTypeUidToId = new Dictionary<string, string>();
        private static Dictionary<string, string> _sectionLabelToFramePropId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _floorPropUidToId = new Dictionary<string, string>();
        private static Dictionary<double, string> _wallThicknessToId = new Dictionary<double, string>();
        private static string _groundLevelId = null;

        #endregion

        #region Public Initialization Methods

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
            _floorPropUidToId.Clear();
            _wallThicknessToId.Clear();
            _groundLevelId = null;

            Console.WriteLine("All mappings cleared");
        }

        #endregion

        #region Level/Story Mapping Methods

        /// <summary>
        /// Build mappings between RAM stories and Core levels
        /// </summary>
        private static void BuildLevelMappings(IModel ramModel, IEnumerable<Level> levels)
        {
            var ramStories = ramModel.GetStories();
            Console.WriteLine("Building level mappings...");

            // Build name-to-ID mappings for levels
            foreach (var level in levels)
            {
                if (!string.IsNullOrEmpty(level.Name) && !string.IsNullOrEmpty(level.Id))
                {
                    _levelNameToId[level.Name] = level.Id;
                    Console.WriteLine($"Level name mapping: '{level.Name}' -> {level.Id}");
                }
            }

            // Build name-to-UID mappings for stories
            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory story = ramStories.GetAt(i);
                if (story != null && !string.IsNullOrEmpty(story.strLabel))
                {
                    _storyNameToUid[story.strLabel] = story.lUID.ToString();
                    Console.WriteLine($"Story name mapping: '{story.strLabel}' -> {story.lUID}");
                }
            }

            // Create cross-mappings between stories and levels
            foreach (var level in levels)
            {
                string levelName = level.Name;
                if (_storyNameToUid.TryGetValue(levelName, out string storyUid))
                {
                    _levelIdToStoryUid[level.Id] = storyUid;
                    _storyUidToLevelId[storyUid] = level.Id;
                    Console.WriteLine($"Cross mapping: Level '{levelName}' ({level.Id}) <-> Story UID {storyUid}");
                }
            }

            // Set ground level
            SetGroundLevelByName(levels);

            Console.WriteLine($"Level mappings built: {_levelIdToStoryUid.Count} level-story pairs");
        }

        /// <summary>
        /// Get the Core Level ID for a RAM Story UID
        /// </summary>
        public static string GetLevelIdForStoryUid(string storyUid)
        {
            return _storyUidToLevelId.TryGetValue(storyUid, out string levelId) ? levelId : null;
        }

        /// <summary>
        /// Get the RAM Story UID for a Core Level ID
        /// </summary>
        public static string GetStoryUidForLevelId(string levelId)
        {
            return _levelIdToStoryUid.TryGetValue(levelId, out string storyUid) ? storyUid : null;
        }

        /// <summary>
        /// Get the Core Level ID for a level name
        /// </summary>
        public static string GetLevelIdForName(string levelName)
        {
            return _levelNameToId.TryGetValue(levelName, out string levelId) ? levelId : null;
        }

        public static string GetGroundLevelId()
        {
            return _groundLevelId;
        }

        /// <summary>
        /// Get the RAM Story UID for a story name
        /// </summary>
        public static string GetStoryUidForName(string storyName)
        {
            return _storyNameToUid.TryGetValue(storyName, out string storyUid) ? storyUid : null;
        }

        #endregion

        #region Floor Type Mapping Methods

        /// <summary>
        /// Build mappings between RAM floor types and Core floor types
        /// </summary>
        private static void BuildFloorTypeMappings(IModel ramModel, IEnumerable<FloorType> floorTypes, IEnumerable<Level> levels)
        {
            try
            {
                IFloorTypes ramFloorTypes = ramModel.GetFloorTypes();
                Console.WriteLine("Building floor type mappings...");

                // First pass: map by name
                Dictionary<string, string> floorTypeNameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var floorType in floorTypes)
                {
                    if (!string.IsNullOrEmpty(floorType.Name) && !string.IsNullOrEmpty(floorType.Id))
                    {
                        floorTypeNameToId[floorType.Name] = floorType.Id;
                    }
                }

                // Map RAM floor type UIDs to Core floor type IDs
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    if (ramFloorType != null && !string.IsNullOrEmpty(ramFloorType.strLabel))
                    {
                        if (floorTypeNameToId.TryGetValue(ramFloorType.strLabel, out string floorTypeId))
                        {
                            _floorTypeUidToId[ramFloorType.lUID.ToString()] = floorTypeId;
                            Console.WriteLine($"Floor type mapping: RAM UID {ramFloorType.lUID} ('{ramFloorType.strLabel}') -> Core ID {floorTypeId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error building floor type mappings: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the Core Floor Type ID for a RAM Floor Type UID
        /// </summary>
        public static string GetFloorTypeIdForUid(string uid)
        {
            return _floorTypeUidToId.TryGetValue(uid, out string floorTypeId) ? floorTypeId : null;
        }

        /// <summary>
        /// Get the RAM Floor Type UID for a Core Floor Type ID
        /// </summary>
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

        #endregion

        #region Frame Properties Mapping Methods

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

        /// <summary>
        /// Get default frame property ID when no specific match is found
        /// </summary>
        public static string GetDefaultFramePropertyId()
        {
            // Return first frame property ID if available
            return _sectionLabelToFramePropId.Values.FirstOrDefault();
        }

        #endregion

        #region Floor Properties Mapping Methods

        /// <summary>
        /// Set floor properties mappings (RAM UID -> FloorProperties ID)
        /// </summary>
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

        #endregion

        #region Wall Properties Mapping Methods

        /// <summary>
        /// Set mapping from wall thickness to wall property ID
        /// </summary>
        public static void SetWallThicknessMapping(Dictionary<double, string> thicknessToIdMapping)
        {
            _wallThicknessToId.Clear();
            foreach (var mapping in thicknessToIdMapping)
            {
                _wallThicknessToId[mapping.Key] = mapping.Value;
            }
            Console.WriteLine($"Set {_wallThicknessToId.Count} wall thickness mappings");
        }

        /// <summary>
        /// Get wall property ID for a given thickness
        /// </summary>
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

        /// <summary>
        /// Set wall property mappings
        /// </summary>
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

        /// <summary>
        /// Find the Core Wall Property ID for a RAM Wall
        /// </summary>
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

        #endregion

        #region Utility Methods

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
                    Console.WriteLine($"Set ground level ID: {_groundLevelId} ('{level.Name}')");
                    break;
                }
            }
        }

        /// <summary>
        /// Create level-to-floor-type mapping
        /// </summary>
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

        /// <summary>
        /// Get the Core Level ID for the wall's top level
        /// </summary>
        public static string FindTopLevelIdForWall(IWall wall, IStory currentStory)
        {
            if (wall == null || currentStory == null)
                return null;

            // Get the current story's level ID
            string storyUid = currentStory.lUID.ToString();
            return GetLevelIdForStoryUid(storyUid);
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
        /// Get a mapping of RAM Sections to Core Frame Properties
        /// </summary>
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

        #endregion

        #region Debug Methods

        /// <summary>
        /// Dump the current state of all mappings for debugging
        /// </summary>
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

            // Dump floor properties mappings
            Console.WriteLine("\nFloor Property UID to ID Mappings:");
            Console.WriteLine("{0,-10} {1,-40}", "Property UID", "Floor Prop ID");
            Console.WriteLine(new string('-', 50));
            foreach (var mapping in _floorPropUidToId)
            {
                Console.WriteLine("{0,-10} {1,-40}", mapping.Key, mapping.Value);
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Models.Loads;
using Core.Utilities;

namespace ETABS.Export.Loads
{
  
    public class SurfaceLoadExport
    {
        // Dictionary to map load pattern names to load definition IDs
        private Dictionary<string, string> _loadDefIdsByName = new Dictionary<string, string>();

        // Store the load definitions collection
        private IEnumerable<LoadDefinition> _loadDefinitions;

        // Dictionary to map floor type names to floor type IDs
        private Dictionary<string, string> _floorTypeIdsByName = new Dictionary<string, string>();

        // Sets the load definition name to ID mapping for reference when creating surface loads
        public void SetLoadDefinitions(IEnumerable<LoadDefinition> loadDefinitions)
        {
            _loadDefIdsByName.Clear();
            foreach (var loadDef in loadDefinitions)
            {
                if (!string.IsNullOrEmpty(loadDef.Name))
                {
                    _loadDefIdsByName[loadDef.Name] = loadDef.Id;
                }
            }
        }

        // Sets the floor type name to ID mapping for reference when creating surface loads
        public void SetFloorTypes(Dictionary<string, string> floorTypeIdMapping)
        {
            _floorTypeIdsByName = new Dictionary<string, string>(floorTypeIdMapping);
        }

        // Imports surface loads from E2K SHELL UNIFORM LOAD SETS and SHELL OBJECT LOADS sections
        public List<SurfaceLoad> Export(string shellUniformLoadSetsSection, string shellObjectLoadsSection)
        {
            var surfaceLoads = new Dictionary<string, SurfaceLoad>();
            var loadSetToSurfaceLoadMap = new Dictionary<string, SurfaceLoad>();
                
            if (string.IsNullOrWhiteSpace(shellUniformLoadSetsSection) &&
                string.IsNullOrWhiteSpace(shellObjectLoadsSection))
            {
                return new List<SurfaceLoad>();
            }

            // First, parse all load sets
            if (!string.IsNullOrWhiteSpace(shellUniformLoadSetsSection))
            {
                ParseShellUniformLoadSets(shellUniformLoadSetsSection, surfaceLoads, loadSetToSurfaceLoadMap);
            }

            // Then, parse area load assignments
            if (!string.IsNullOrWhiteSpace(shellObjectLoadsSection))
            {
                ParseShellObjectLoads(shellObjectLoadsSection, surfaceLoads, loadSetToSurfaceLoadMap);
            }

            return new List<SurfaceLoad>(surfaceLoads.Values);
        }

        // Parses the SHELL UNIFORM LOAD SETS section to create surface load definitions
        private void ParseShellUniformLoadSets(
            string shellUniformLoadSetsSection,
            Dictionary<string, SurfaceLoad> surfaceLoads,
            Dictionary<string, SurfaceLoad> loadSetToSurfaceLoadMap)
        {
            // Regular expression to match shell uniform load set
            // Format: SHELLUNIFORMLOADSET "LS_2c71c0e0" LOADPAT "Live" VALUE 0.1388889
            var loadSetPattern = new Regex(@"^\s*SHELLUNIFORMLOADSET\s+""([^""]+)""\s+LOADPAT\s+""([^""]+)""\s+VALUE\s+([\d\.E\+\-]+)",
                RegexOptions.Multiline);

            var loadSetMatches = loadSetPattern.Matches(shellUniformLoadSetsSection);

            // Group matches by load set name
            var loadSetsGrouped = new Dictionary<string, List<Tuple<string, double>>>();

            foreach (Match match in loadSetMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string loadSetName = match.Groups[1].Value;
                    string loadPatName = match.Groups[2].Value;
                    double loadValue = Convert.ToDouble(match.Groups[3].Value);

                    // Add to grouped dictionary
                    if (!loadSetsGrouped.ContainsKey(loadSetName))
                    {
                        loadSetsGrouped[loadSetName] = new List<Tuple<string, double>>();
                    }

                    loadSetsGrouped[loadSetName].Add(new Tuple<string, double>(loadPatName, loadValue));
                }
            }

            // Create surface loads from grouped load sets
            foreach (var loadSetGroup in loadSetsGrouped)
            {
                string loadSetName = loadSetGroup.Key;
                var loadPatterns = loadSetGroup.Value;

                // Create a new surface load
                var surfaceLoad = new SurfaceLoad
                {
                    Id = IdGenerator.Generate(IdGenerator.Loads.SURFACE_LOAD)
                };

                // Assign default floor type (can be updated in shell object loads section)
                surfaceLoad.LayoutTypeId = GetDefaultFloorTypeId();

                // Assign load definition IDs
                foreach (var loadPattern in loadPatterns)
                {
                    string loadPatName = loadPattern.Item1;

                    // Look up load definition ID
                    if (_loadDefIdsByName.TryGetValue(loadPatName, out string loadDefId))
                    {
                        if (IsLiveLoadPattern(loadPatName))
                        {
                            surfaceLoad.LiveLoadId = loadDefId;
                        }
                        else if (IsDeadLoadPattern(loadPatName))
                        {
                            surfaceLoad.DeadLoadId = loadDefId;
                        }
                        // Additional load types could be handled here if the SurfaceLoad class is extended
                    }
                }

                // Add to dictionaries
                surfaceLoads[surfaceLoad.Id] = surfaceLoad;
                loadSetToSurfaceLoadMap[loadSetName] = surfaceLoad;
            }
        }

        // Parses the SHELL OBJECT LOADS section to assign surface loads to floor areas
        private void ParseShellObjectLoads(
            string shellObjectLoadsSection,
            Dictionary<string, SurfaceLoad> surfaceLoads,
            Dictionary<string, SurfaceLoad> loadSetToSurfaceLoadMap)
        {
            // Regular expression to match shell object load
            // Format: AREALOAD "F1" "Story1" TYPE "UNIFLOADSET" "LS_2c71c0e0"
            var areaLoadPattern = new Regex(@"^\s*AREALOAD\s+""([^""]+)""\s+""([^""]+)""\s+TYPE\s+""UNIFLOADSET""\s+""([^""]+)""",
                RegexOptions.Multiline);

            var areaLoadMatches = areaLoadPattern.Matches(shellObjectLoadsSection);

            foreach (Match match in areaLoadMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string areaId = match.Groups[1].Value;
                    string storyName = match.Groups[2].Value;
                    string loadSetName = match.Groups[3].Value;

                    // Find the corresponding surface load for this load set
                    if (loadSetToSurfaceLoadMap.TryGetValue(loadSetName, out SurfaceLoad surfaceLoad))
                    {
                        // Here we would typically assign the surface load to a floor area
                        // This would require an extension to our data model to track this relationship
                        // For now, we'll just update the floor type if available
                        string floorTypeName = DeriveFloorTypeFromAreaAndStory(areaId, storyName);
                        if (!string.IsNullOrEmpty(floorTypeName) &&
                            _floorTypeIdsByName.TryGetValue(floorTypeName, out string floorTypeId))
                        {
                            surfaceLoad.LayoutTypeId = floorTypeId;
                        }
                    }
                }
            }
        }

        // Gets the default floor type ID
        private string GetDefaultFloorTypeId()
        {
            // Return the first available floor type ID, or null if none available
            if (_floorTypeIdsByName.Count > 0)
            {
                return _floorTypeIdsByName.Values.First();
            }
            return null;
        }

        // Derives a floor type name from an area ID and story name
        private string DeriveFloorTypeFromAreaAndStory(string areaId, string storyName)
        {
            // In a real implementation, this would use the area and story to determine the floor type
            // For this simplified implementation, we'll just return a default value
            return "typical";
        }

        // Determines if a load pattern name represents a live load
        private bool IsLiveLoadPattern(string loadPatName)
        {
            // Try to find the load definition by name
            if (_loadDefIdsByName.TryGetValue(loadPatName, out string loadDefId))
            {
                // Check if it's in the load definitions collection
                var loadDef = _loadDefinitions?.FirstOrDefault(ld => ld.Id == loadDefId);
                if (loadDef != null)
                {
                    // Use the enum type directly
                    return loadDef.Type == LoadType.Live;
                }
            }

            // Fallback to string analysis if not found
            string name = loadPatName.ToLower();
            return name.Contains("live") || name == "ll" || name.Contains("reducible");
        }

        // Determines if a load pattern name represents a dead load
        private bool IsDeadLoadPattern(string loadPatName)
        {
            // Try to find the load definition by name
            if (_loadDefIdsByName.TryGetValue(loadPatName, out string loadDefId))
            {
                // Check if it's in the load definitions collection
                var loadDef = _loadDefinitions?.FirstOrDefault(ld => ld.Id == loadDefId);
                if (loadDef != null)
                {
                    // Use the enum type directly
                    return loadDef.Type == LoadType.Dead;
                }
            }

            // Fallback to string analysis if not found
            string name = loadPatName.ToLower();
            return name.Contains("dead") || name == "dl" || name == "sw" ||
                   name.Contains("self") || name.Contains("weight") || name.Contains("sdl");
        }
    }
}
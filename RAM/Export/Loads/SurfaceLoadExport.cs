using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Loads;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Loads
{
    public class SurfaceLoadExport
    {
        private IModel _model;
        private Dictionary<string, string> _deadLoadMappings = new Dictionary<string, string>();
        private Dictionary<string, string> _liveLoadMappings = new Dictionary<string, string>();
        private Dictionary<string, string> _floorTypeMappings = new Dictionary<string, string>();

        public SurfaceLoadExport(IModel model)
        {
            _model = model;
        }

        public void SetLoadMappings(Dictionary<string, string> deadLoadMappings, Dictionary<string, string> liveLoadMappings)
        {
            _deadLoadMappings = deadLoadMappings ?? new Dictionary<string, string>();
            _liveLoadMappings = liveLoadMappings ?? new Dictionary<string, string>();
        }

        public void SetFloorTypeMappings(Dictionary<string, string> floorTypeMappings)
        {
            _floorTypeMappings = floorTypeMappings ?? new Dictionary<string, string>();
        }

        public List<SurfaceLoad> Export()
        {
            var surfaceLoads = new List<SurfaceLoad>();

            try
            {
                // Get surface load sets from RAM
                ISurfaceLoadSets loadSets = _model.GetSurfaceLoadSets();
                if (loadSets == null || loadSets.GetCount() == 0)
                    return surfaceLoads;

                // Process each surface load set
                for (int i = 0; i < loadSets.GetCount(); i++)
                {
                    ISurfaceLoadSet loadSet = loadSets.GetAt(i);
                    if (loadSet == null)
                        continue;

                    // Create surface load
                    SurfaceLoad surfaceLoad = new SurfaceLoad
                    {
                        Id = IdGenerator.Generate(IdGenerator.Loads.SURFACE_LOAD)
                    };

                    // Set load definition IDs (dead and live)
                    try
                    {
                        // Try to get the dead load definition ID
                        string deadLoadId = GetDeadLoadId(loadSet);
                        if (!string.IsNullOrEmpty(deadLoadId))
                        {
                            surfaceLoad.DeadLoadId = deadLoadId;
                        }

                        // Try to get the live load definition ID
                        string liveLoadId = GetLiveLoadId(loadSet);
                        if (!string.IsNullOrEmpty(liveLoadId))
                        {
                            surfaceLoad.LiveLoadId = liveLoadId;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error extracting load definition IDs: {ex.Message}");
                    }

                    // Try to get floor type ID
                    try
                    {
                        string floorTypeId = GetFloorTypeId(loadSet);
                        if (!string.IsNullOrEmpty(floorTypeId))
                        {
                            surfaceLoad.LayoutTypeId = floorTypeId;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error extracting floor type ID: {ex.Message}");
                    }

                    surfaceLoads.Add(surfaceLoad);
                }

                return surfaceLoads;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting surface loads from RAM: {ex.Message}");
                return surfaceLoads;
            }
        }

        private string GetDeadLoadId(ISurfaceLoadSet loadSet)
        {
            // Try to get a matching dead load ID from the mappings
            // This could be based on any attribute of the load set

            // For simplicity, we'll use the first available dead load ID
            if (_deadLoadMappings.Count > 0)
            {
                return _deadLoadMappings.Values.First();
            }

            // If no mapping exists, create a new ID
            return IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION);
        }

        private string GetLiveLoadId(ISurfaceLoadSet loadSet)
        {
            // Try to get a matching live load ID from the mappings
            // This could be based on any attribute of the load set

            // For simplicity, we'll use the first available live load ID
            if (_liveLoadMappings.Count > 0)
            {
                return _liveLoadMappings.Values.First();
            }

            // If no mapping exists, create a new ID
            return IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION);
        }

        private string GetFloorTypeId(ISurfaceLoadSet loadSet)
        {
            // Try to determine the floor type associated with this load set
            // In a real implementation, this would require examining the load set's properties
            // and any floor type associations

            // For simplicity, we'll use the first available floor type ID
            if (_floorTypeMappings.Count > 0)
            {
                return _floorTypeMappings.Values.First();
            }

            return null;
        }
    }
}
// SurfaceLoadImporter.cs
using System;
using System.Collections.Generic;
using Core.Models.Loads;
using Core.Utilities;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Import
{
    public class SurfaceLoadImporter : IRAMImporter<List<SurfaceLoad>>
    {
        private IModel _model;
        private Dictionary<int, string> _loadCaseIdMap;

        public SurfaceLoadImporter(IModel model, Dictionary<int, string> loadCaseIdMap)
        {
            _model = model;
            _loadCaseIdMap = loadCaseIdMap;
        }

        public List<SurfaceLoad> Import()
        {
            var surfaceLoads = new List<SurfaceLoad>();

            try
            {
                // Import surface load property sets from RAM
                ISurfaceLoadPropertySets surfaceLoadSets = _model.GetSurfaceLoadPropertySets();

                for (int i = 0; i < surfaceLoadSets.GetCount(); i++)
                {
                    ISurfaceLoadPropertySet surfaceLoadSet = surfaceLoadSets.GetAt(i);

                    try
                    {
                        // Create a new surface load
                        var surfaceLoad = new SurfaceLoad
                        {
                            Id = IdGenerator.Generate(IdGenerator.Loads.SURFACE_LOAD)
                        };

                        // Extract uniform loads
                        double constDeadLoad, constLiveLoad, deadLoad, liveLoad, massDeadLoad, partitionLoad;
                        ELoadCaseType liveLoadType;

                        surfaceLoadSet.GetUniformLoads(out constDeadLoad, out constLiveLoad, out deadLoad, out liveLoad,
                                                      out massDeadLoad, out partitionLoad, out liveLoadType);

                        // Assign load definition IDs based on the load cases found
                        AssignLoadDefinitionIds(surfaceLoad, deadLoad, liveLoad, liveLoadType);

                        // Only add the surface load if it has at least one load assigned
                        if (!string.IsNullOrEmpty(surfaceLoad.DeadLoadId) || !string.IsNullOrEmpty(surfaceLoad.LiveLoadId))
                        {
                            surfaceLoads.Add(surfaceLoad);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error importing surface load {surfaceLoadSet.strLabel}: {ex.Message}");
                    }
                }

                // If no surface loads were found, create a default one
                if (surfaceLoads.Count == 0)
                {
                    CreateDefaultSurfaceLoad(surfaceLoads);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing surface loads: {ex.Message}");
                CreateDefaultSurfaceLoad(surfaceLoads);
            }

            return surfaceLoads;
        }

        private void AssignLoadDefinitionIds(SurfaceLoad surfaceLoad, double deadLoad, double liveLoad, ELoadCaseType liveLoadType)
        {
            // Find appropriate load definition IDs based on load cases
            ILoadCases loadCases = _model.GetLoadCases();

            // Find dead load case
            if (deadLoad > 0)
            {
                for (int i = 0; i < loadCases.GetCount(); i++)
                {
                    ILoadCase loadCase = loadCases.GetAt(i);
                    if (loadCase.eType == ELoadCaseType.DeadLCa || loadCase.eType == ELoadCaseType.SuperDeadLCa)
                    {
                        if (_loadCaseIdMap.ContainsKey(loadCase.lUID))
                        {
                            surfaceLoad.DeadLoadId = _loadCaseIdMap[loadCase.lUID];
                            break;
                        }
                    }
                }
            }

            // Find live load case
            if (liveLoad > 0)
            {
                for (int i = 0; i < loadCases.GetCount(); i++)
                {
                    ILoadCase loadCase = loadCases.GetAt(i);
                    if ((loadCase.eType == ELoadCaseType.LiveReducibleLCa && liveLoadType == ELoadCaseType.LiveReducibleLCa) ||
                        (loadCase.eType == ELoadCaseType.LiveNonReducibleLCa && liveLoadType == ELoadCaseType.LiveNonReducibleLCa))
                    {
                        if (_loadCaseIdMap.ContainsKey(loadCase.lUID))
                        {
                            surfaceLoad.LiveLoadId = _loadCaseIdMap[loadCase.lUID];
                            break;
                        }
                    }
                }

                // If no exact match found, use any live load
                if (string.IsNullOrEmpty(surfaceLoad.LiveLoadId))
                {
                    for (int i = 0; i < loadCases.GetCount(); i++)
                    {
                        ILoadCase loadCase = loadCases.GetAt(i);
                        if (loadCase.eType == ELoadCaseType.LiveReducibleLCa || loadCase.eType == ELoadCaseType.LiveNonReducibleLCa)
                        {
                            if (_loadCaseIdMap.ContainsKey(loadCase.lUID))
                            {
                                surfaceLoad.LiveLoadId = _loadCaseIdMap[loadCase.lUID];
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void CreateDefaultSurfaceLoad(List<SurfaceLoad> surfaceLoads)
        {
            // Create a default surface load with typical dead and live load IDs
            var surfaceLoad = new SurfaceLoad
            {
                Id = IdGenerator.Generate(IdGenerator.Loads.SURFACE_LOAD)
            };

            // Find first available dead and live load IDs
            foreach (var entry in _loadCaseIdMap)
            {
                // Get load case
                ILoadCases loadCases = _model.GetLoadCases();
                for (int i = 0; i < loadCases.GetCount(); i++)
                {
                    ILoadCase loadCase = loadCases.GetAt(i);
                    if (loadCase.lUID == entry.Key)
                    {
                        if ((loadCase.eType == ELoadCaseType.DeadLCa || loadCase.eType == ELoadCaseType.SuperDeadLCa) &&
                            string.IsNullOrEmpty(surfaceLoad.DeadLoadId))
                        {
                            surfaceLoad.DeadLoadId = entry.Value;
                        }
                        else if ((loadCase.eType == ELoadCaseType.LiveReducibleLCa || loadCase.eType == ELoadCaseType.LiveNonReducibleLCa) &&
                                 string.IsNullOrEmpty(surfaceLoad.LiveLoadId))
                        {
                            surfaceLoad.LiveLoadId = entry.Value;
                        }
                    }
                }

                // Break if we have both loads
                if (!string.IsNullOrEmpty(surfaceLoad.DeadLoadId) && !string.IsNullOrEmpty(surfaceLoad.LiveLoadId))
                {
                    break;
                }
            }

            surfaceLoads.Add(surfaceLoad);
        }
    }
}
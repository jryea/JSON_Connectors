// LoadDefinitionImporter.cs
using System;
using System.Collections.Generic;
using Core.Models.Loads;
using Core.Utilities;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Import
{
    public class LoadDefinitionImporter : IRAMImporter<List<LoadDefinition>>
    {
        private IModel _model;

        public LoadDefinitionImporter(IModel model)
        {
            _model = model;
        }

        public List<LoadDefinition> Import()
        {
            var loadDefinitions = new List<LoadDefinition>();

            try
            {
                // Import load cases from RAM
                ILoadCases loadCases = _model.GetLoadCases();

                for (int i = 0; i < loadCases.GetCount(); i++)
                {
                    ILoadCase loadCase = loadCases.GetAt(i);

                    try
                    {
                        // Create a load definition
                        var loadDefinition = new LoadDefinition
                        {
                            Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION),
                            Name = loadCase.strLabel,
                            Type = ConvertLoadCaseTypeToString(loadCase.eType),
                            SelfWeight = loadCase.dSelfWeightMultiplier
                        };

                        loadDefinitions.Add(loadDefinition);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error importing load case {loadCase.strLabel}: {ex.Message}");
                    }
                }

                // If no load definitions were found, create defaults
                if (loadDefinitions.Count == 0)
                {
                    CreateDefaultLoadDefinitions(loadDefinitions);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing load definitions: {ex.Message}");
                CreateDefaultLoadDefinitions(loadDefinitions);
            }

            return loadDefinitions;
        }

        private string ConvertLoadCaseTypeToString(ELoadCaseType loadCaseType)
        {
            switch (loadCaseType)
            {
                case ELoadCaseType.DeadLCa:
                    return "Dead";
                case ELoadCaseType.SuperDeadLCa:
                    return "SuperimposedDead";
                case ELoadCaseType.LiveReducibleLCa:
                    return "Live";
                case ELoadCaseType.LiveNonReducibleLCa:
                    return "LiveNonReducible";
                case ELoadCaseType.RoofLiveLCa:
                    return "RoofLive";
                case ELoadCaseType.SnowLCa:
                    return "Snow";
                case ELoadCaseType.WindLCa:
                    return "Wind";
                case ELoadCaseType.EarthquakeLCa:
                    return "Seismic";
                default:
                    return "Other";
            }
        }

        private void CreateDefaultLoadDefinitions(List<LoadDefinition> loadDefinitions)
        {
            // Create dead load
            loadDefinitions.Add(new LoadDefinition
            {
                Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION),
                Name = "DEAD",
                Type = "Dead",
                SelfWeight = 1.0
            });

            // Create superimposed dead load
            loadDefinitions.Add(new LoadDefinition
            {
                Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION),
                Name = "SDL",
                Type = "SuperimposedDead",
                SelfWeight = 0.0
            });

            // Create live load
            loadDefinitions.Add(new LoadDefinition
            {
                Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION),
                Name = "LIVE",
                Type = "Live",
                SelfWeight = 0.0
            });

            // Create seismic load in X direction
            loadDefinitions.Add(new LoadDefinition
            {
                Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION),
                Name = "EQX",
                Type = "Seismic",
                SelfWeight = 0.0
            });

            // Create seismic load in Y direction
            loadDefinitions.Add(new LoadDefinition
            {
                Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION),
                Name = "EQY",
                Type = "Seismic",
                SelfWeight = 0.0
            });
        }
    }
}
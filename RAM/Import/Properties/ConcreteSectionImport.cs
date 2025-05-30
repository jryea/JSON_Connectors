using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;
using RAMCONCRETECOLUMNLib;

namespace RAM.Import.Properties
{
    // Imports concrete frame sections from Core model to RAM
    public class ConcreteSectionImport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;

        public ConcreteSectionImport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
        }

        // Imports concrete frame sections to RAM
        public List<(FrameProperties CoreProperty, int RamUid)> Import(IEnumerable<FrameProperties> frameProperties)
        {
            var importedSections = new List<(FrameProperties, int)>();

            try
            {
                // Get concrete section properties interface from RAM
                IConcSectProps concSectProps = _model.GetConcreteSectionProps();
                if (concSectProps == null)
                {
                    Console.WriteLine("Failed to get concrete section properties from RAM model");
                    return importedSections;
                }

                // Filter to only concrete frame properties
                var concreteFrameProperties = frameProperties?.Where(fp =>
                    fp.Type == FrameMaterialType.Concrete &&
                    fp.ConcreteProps != null) ?? Enumerable.Empty<FrameProperties>();

                Console.WriteLine($"Found {concreteFrameProperties.Count()} concrete frame properties to import");

                foreach (var frameProp in concreteFrameProperties)
                {
                    if (string.IsNullOrEmpty(frameProp.Name) || frameProp.ConcreteProps == null)
                    {
                        Console.WriteLine($"Skipping frame property with missing name or concrete properties");
                        continue;
                    }

                    try
                    {
                        int ramUid = ImportConcreteSection(concSectProps, frameProp);
                        if (ramUid > 0)
                        {
                            importedSections.Add((frameProp, ramUid));
                            Console.WriteLine($"Successfully imported concrete section: {frameProp.Name} (RAM UID: {ramUid})");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to import concrete section: {frameProp.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error importing concrete section {frameProp.Name}: {ex.Message}");
                    }
                }

                Console.WriteLine($"Imported {importedSections.Count} concrete sections to RAM");
                return importedSections;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing concrete sections: {ex.Message}");
                return importedSections;
            }
        }

        // Imports a single concrete section based on its type
        private int ImportConcreteSection(IConcSectProps concSectProps, FrameProperties frameProp)
        {
            var concreteProps = frameProp.ConcreteProps;
            string sectionName = frameProp.Name;

            // Convert dimensions to inches if needed
            double depth = UnitConversionUtils.ConvertToInches(concreteProps.Depth, _lengthUnit);
            double width = UnitConversionUtils.ConvertToInches(concreteProps.Width, _lengthUnit);

            switch (concreteProps.SectionType)
            {
                case ConcreteSectionType.Rectangular:
                    return ImportRectangularSection(concSectProps, sectionName, depth, width);

                case ConcreteSectionType.Circular:
                    return ImportCircularSection(concSectProps, sectionName, depth);

                case ConcreteSectionType.TShaped:
                    return ImportTeeSection(concSectProps, sectionName, depth, width);

                case ConcreteSectionType.LShaped:
                    // L-shaped sections not directly supported by the specified methods
                    // Fall back to rectangular for now
                    Console.WriteLine($"L-shaped section {sectionName} not directly supported, using rectangular");
                    return ImportRectangularSection(concSectProps, sectionName, depth, width);

                case ConcreteSectionType.Custom:
                    // Custom sections not directly supported by the specified methods
                    // Fall back to rectangular for now
                    Console.WriteLine($"Custom section {sectionName} not directly supported, using rectangular");
                    return ImportRectangularSection(concSectProps, sectionName, depth, width);

                default:
                    Console.WriteLine($"Unknown concrete section type for {sectionName}, using rectangular");
                    return ImportRectangularSection(concSectProps, sectionName, depth, width);
            }
        }

        // Imports a rectangular concrete section
        private int ImportRectangularSection(IConcSectProps conSectProps, string name, double depth, double width)
        {
            try
            {
                // Use AddRect method as specified
                IConcSectProp rectSection = conSectProps.AddRect(
                    name,
                    EUniqueMemberTypeID.eTypeColumn,
                    depth,
                    width);

                if (rectSection != null)
                {
                    Console.WriteLine($"Created rectangular concrete section: {name} ({width}\" x {depth}\")");
                    return rectSection.lUID;
                }
                else
                {
                    Console.WriteLine($"Failed to create rectangular concrete section: {name}");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating rectangular concrete section {name}: {ex.Message}");
                return 0;
            }
        }

        // Imports a circular concrete section (placeholder implementation)
        private int ImportCircularSection(IConcSectProps conSectProps, string name, double diameter)
        {
            try
            {
                // Use AddRound method as specified
                // For now, using fixed diameter of 12.0 as specified in requirements
                IConcSectProp roundSection = conSectProps.AddRound(
                    name,
                    EUniqueMemberTypeID.eTypeColumn,
                    12.0); // Fixed diameter as specified

                if (roundSection != null)
                {
                    Console.WriteLine($"Created circular concrete section: {name} (diameter: 12.0\")");
                    return roundSection.lUID;
                }
                else
                {
                    Console.WriteLine($"Failed to create circular concrete section: {name}");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating circular concrete section {name}: {ex.Message}");
                return 0;
            }
        }

        // Imports a T-shaped concrete section (placeholder implementation)
        private int ImportTeeSection(IConcSectProps conSectProps, string name, double depth, double width)
        {
            try
            {
                // Use AddTee method as specified
                IConcSectProp teeSection = conSectProps.AddTee(
                    name,
                    -1, // flangeLeftOverhang default
                    -1, // flangeRightOverhang default
                    -1, // flangeThickness default
                    depth,
                    width);

                if (teeSection != null)
                {
                    Console.WriteLine($"Created T-shaped concrete section: {name} (depth: {depth}\", width: {width}\")");
                    return teeSection.lUID;
                }
                else
                {
                    Console.WriteLine($"Failed to create T-shaped concrete section: {name}");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating T-shaped concrete section {name}: {ex.Message}");
                return 0;
            }
        }

        // Gets mapping of Core frame property IDs to RAM section UIDs
        public Dictionary<string, int> GetFramePropertyToSectionMapping(
            IEnumerable<FrameProperties> frameProperties)
        {
            var mapping = new Dictionary<string, int>();
            var importedSections = Import(frameProperties);

            foreach (var (coreProperty, ramUid) in importedSections)
            {
                mapping[coreProperty.Id] = ramUid;
            }

            return mapping;
        }
    }
}
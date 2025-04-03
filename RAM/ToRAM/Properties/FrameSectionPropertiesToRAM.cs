// FrameSectionPropertiesImporter.cs
using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Import
{
    public class FrameSectionPropertiesToRAM : IRAMImporter<List<FrameProperties>>
    {
        private IModel _model;
        private Dictionary<int, string> _materialIdMap;

        public FrameSectionPropertiesToRAM(IModel model, Dictionary<int, string> materialIdMap)
        {
            _model = model;
            _materialIdMap = materialIdMap;
        }

        public List<FrameProperties> Import()
        {
            var frameProperties = new List<FrameProperties>();

            try
            {
                // Import steel sections
                ImportSteelSections(frameProperties);

                // Import concrete sections
                ImportConcreteSections(frameProperties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing frame properties: {ex.Message}");
            }

            return frameProperties;
        }

        private void ImportSteelSections(List<FrameProperties> frameProperties)
        {
            // Get steel sections from RAM
            ISteelSections steelSections = _model.GetSteelSections();

            for (int i = 0; i < steelSections.GetCount(); i++)
            {
                ISteelSection steelSection = steelSections.GetAt(i);

                try
                {
                    // Get material ID
                    string materialId = null;
                    if (_materialIdMap.ContainsKey(steelSection.lMaterialId))
                    {
                        materialId = _materialIdMap[steelSection.lMaterialId];
                    }

                    // Determine shape and create dimensions
                    string shape = "CUSTOM";
                    var dimensions = new Dictionary<string, double>();

                    switch (steelSection.eSectionType)
                    {
                        case ESectionType.eSectWide:
                            shape = "W";
                            dimensions["depth"] = steelSection.dD;
                            dimensions["width"] = steelSection.dBf;
                            dimensions["flangeThickness"] = steelSection.dTf;
                            dimensions["webThickness"] = steelSection.dTw;
                            break;

                        case ESectionType.eSectTubeRect:
                        case ESectionType.eSectTubeSquare:
                            shape = "HSS";
                            dimensions["depth"] = steelSection.dD;
                            dimensions["width"] = steelSection.dB;
                            dimensions["wallThickness"] = steelSection.dT;
                            break;

                        case ESectionType.eSectPipe:
                            shape = "PIPE";
                            dimensions["outerDiameter"] = steelSection.dD;
                            dimensions["wallThickness"] = steelSection.dT;
                            break;

                        case ESectionType.eSectChannel:
                            shape = "C";
                            dimensions["depth"] = steelSection.dD;
                            dimensions["width"] = steelSection.dBf;
                            dimensions["flangeThickness"] = steelSection.dTf;
                            dimensions["webThickness"] = steelSection.dTw;
                            break;

                        case ESectionType.eSectAngle:
                            shape = "L";
                            dimensions["depth"] = steelSection.dD;
                            dimensions["width"] = steelSection.dB;
                            dimensions["thickness"] = steelSection.dT;
                            break;
                    }

                    // Create frame property
                    var frameProp = new FrameProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                        Name = steelSection.strLabel,
                        MaterialId = materialId,
                        Shape = shape,
                        Dimensions = dimensions
                    };

                    frameProperties.Add(frameProp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error importing steel section {steelSection.strLabel}: {ex.Message}");
                }
            }
        }

        private void ImportConcreteSections(List<FrameProperties> frameProperties)
        {
            // Get concrete sections from RAM
            IConcreteSections concreteSections = _model.GetConcreteSections();

            for (int i = 0; i < concreteSections.GetCount(); i++)
            {
                IConcreteSection concreteSection = concreteSections.GetAt(i);

                try
                {
                    // Get material ID
                    string materialId = null;
                    if (_materialIdMap.ContainsKey(concreteSection.lMaterialId))
                    {
                        materialId = _materialIdMap[concreteSection.lMaterialId];
                    }

                    // Determine shape and create dimensions
                    string shape = "RECT";
                    var dimensions = new Dictionary<string, double>();

                    switch (concreteSection.eSectType)
                    {
                        case EConcSectType.eConcSectRectangle:
                            shape = "RECT";
                            dimensions["depth"] = concreteSection.dH;
                            dimensions["width"] = concreteSection.dB;
                            break;

                        case EConcSectType.eConcSectCircle:
                            shape = "CIRCLE";
                            dimensions["diameter"] = concreteSection.dDiameter;
                            break;

                        case EConcSectType.eConcSectLShape:
                            shape = "L";
                            dimensions["depth"] = concreteSection.dH;
                            dimensions["width"] = concreteSection.dB;
                            break;

                        case EConcSectType.eConcSectTShape:
                            shape = "T";
                            dimensions["depth"] = concreteSection.dH;
                            dimensions["width"] = concreteSection.dB;
                            break;
                    }

                    // Create frame property
                    var frameProp = new FrameProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                        Name = concreteSection.strLabel,
                        MaterialId = materialId,
                        Shape = shape,
                        Dimensions = dimensions
                    };

                    frameProperties.Add(frameProp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error importing concrete section {concreteSection.strLabel}: {ex.Message}");
                }
            }
        }
    }
}
// FrameSectionPropertiesExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Properties;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class FrameSectionPropertiesExporter : IRAMExporter
    {
        private IModel _model;

        public FrameSectionPropertiesExporter(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Get frame section dictionaries from RAM
            ISteelSections steelSections = _model.GetSteelSections();
            IConcreteSections concreteSections = _model.GetConcreteSections();

            // Map materials by ID for reference
            var materialsById = model.Properties.Materials.ToDictionary(m => m.Id, m => m);

            // Group frame properties by material type
            foreach (var frameProp in model.Properties.FrameProperties)
            {
                try
                {
                    // Skip if no material ID
                    if (string.IsNullOrEmpty(frameProp.MaterialId) || !materialsById.ContainsKey(frameProp.MaterialId))
                    {
                        Console.WriteLine($"No material found for frame section {frameProp.Name}");
                        continue;
                    }

                    Material material = materialsById[frameProp.MaterialId];
                    string materialType = material.Type?.ToLower() ?? "";

                    // Export based on material type
                    if (materialType.Contains("steel"))
                    {
                        ExportSteelSection(frameProp, material, steelSections);
                    }
                    else if (materialType.Contains("concrete"))
                    {
                        ExportConcreteSection(frameProp, material, concreteSections);
                    }
                    else
                    {
                        Console.WriteLine($"Unsupported material type for frame section {frameProp.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting frame section {frameProp.Name}: {ex.Message}");
                }
            }
        }

        private void ExportSteelSection(FrameProperties frameProp, Material material, ISteelSections steelSections)
        {
            // Check if section already exists
            bool exists = false;
            for (int i = 0; i < steelSections.GetCount(); i++)
            {
                ISteelSection existingSection = steelSections.GetAt(i);
                if (existingSection.strLabel == frameProp.Name)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                // Determine section type based on shape
                string shape = frameProp.Shape?.ToUpper() ?? "";
                ESectionType sectionType = ESectionType.eSectOther;

                if (shape.StartsWith("W") || shape == "WIDE FLANGE")
                {
                    sectionType = ESectionType.eSectWide;
                }
                else if (shape.StartsWith("HSS") || shape == "TUBE")
                {
                    if (frameProp.Dimensions != null &&
                        frameProp.Dimensions.TryGetValue("width", out double width) &&
                        frameProp.Dimensions.TryGetValue("depth", out double depth))
                    {
                        if (Math.Abs(width - depth) < 0.001)
                        {
                            sectionType = ESectionType.eSectTubeSquare;
                        }
                        else
                        {
                            sectionType = ESectionType.eSectTubeRect;
                        }
                    }
                    else
                    {
                        sectionType = ESectionType.eSectTubeRect;
                    }
                }
                else if (shape.StartsWith("PIPE") || shape == "PIPE")
                {
                    sectionType = ESectionType.eSectPipe;
                }
                else if (shape.StartsWith("C") || shape == "CHANNEL")
                {
                    sectionType = ESectionType.eSectChannel;
                }
                else if (shape.StartsWith("L") || shape == "ANGLE")
                {
                    sectionType = ESectionType.eSectAngle;
                }
                else if (shape.StartsWith("WT") || shape == "TEE")
                {
                    sectionType = ESectionType.eSectTee;
                }

                // Create new steel section
                ISteelSection steelSection = steelSections.Add(frameProp.Name, sectionType);

                // Set section properties based on shape
                if (frameProp.Dimensions != null)
                {
                    switch (sectionType)
                    {
                        case ESectionType.eSectWide:
                            SetWideFlangeProperties(steelSection, frameProp);
                            break;

                        case ESectionType.eSectTubeRect:
                        case ESectionType.eSectTubeSquare:
                            SetTubeProperties(steelSection, frameProp);
                            break;

                        case ESectionType.eSectPipe:
                            SetPipeProperties(steelSection, frameProp);
                            break;

                        case ESectionType.eSectChannel:
                            SetChannelProperties(steelSection, frameProp);
                            break;

                        case ESectionType.eSectAngle:
                            SetAngleProperties(steelSection, frameProp);
                            break;
                    }
                }
            }
        }

        private void ExportConcreteSection(FrameProperties frameProp, Material material, IConcreteSections concreteSections)
        {
            // Check if section already exists
            bool exists = false;
            for (int i = 0; i < concreteSections.GetCount(); i++)
            {
                IConcreteSection existingSection = concreteSections.GetAt(i);
                if (existingSection.strLabel == frameProp.Name)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                // Determine section type based on shape
                string shape = frameProp.Shape?.ToUpper() ?? "";
                EConcSectType sectionType = EConcSectType.eConcSectRectangle;

                if (shape.Contains("CIRCLE") || shape.Contains("ROUND"))
                {
                    sectionType = EConcSectType.eConcSectCircle;
                }
                else if (shape.Contains("L") || shape.Contains("LSHAPE"))
                {
                    sectionType = EConcSectType.eConcSectLShape;
                }
                else if (shape.Contains("T") || shape.Contains("TSHAPE"))
                {
                    sectionType = EConcSectType.eConcSectTShape;
                }

                // Create new concrete section
                IConcreteSection concreteSection = concreteSections.Add(frameProp.Name, sectionType);

                // Set section properties based on shape
                if (frameProp.Dimensions != null)
                {
                    switch (sectionType)
                    {
                        case EConcSectType.eConcSectRectangle:
                            if (frameProp.Dimensions.TryGetValue("depth", out double h) &&
                                frameProp.Dimensions.TryGetValue("width", out double b))
                            {
                                concreteSection.dH = h;
                                concreteSection.dB = b;
                            }
                            break;

                        case EConcSectType.eConcSectCircle:
                            if (frameProp.Dimensions.TryGetValue("diameter", out double d))
                            {
                                concreteSection.dDiameter = d;
                            }
                            break;
                    }
                }
            }
        }

        private void SetWideFlangeProperties(ISteelSection section, FrameProperties frameProp)
        {
            if (frameProp.Dimensions.TryGetValue("depth", out double d) &&
                frameProp.Dimensions.TryGetValue("width", out double bf) &&
                frameProp.Dimensions.TryGetValue("webThickness", out double tw) &&
                frameProp.Dimensions.TryGetValue("flangeThickness", out double tf))
            {
                section.dD = d;
                section.dBf = bf;
                section.dTw = tw;
                section.dTf = tf;
            }
        }

        private void SetTubeProperties(ISteelSection section, FrameProperties frameProp)
        {
            if (frameProp.Dimensions.TryGetValue("depth", out double d) &&
                frameProp.Dimensions.TryGetValue("width", out double b) &&
                frameProp.Dimensions.TryGetValue("wallThickness", out double t))
            {
                section.dD = d;
                section.dB = b;
                section.dT = t;
            }
        }

        private void SetPipeProperties(ISteelSection section, FrameProperties frameProp)
        {
            if (frameProp.Dimensions.TryGetValue("outerDiameter", out double od) &&
                frameProp.Dimensions.TryGetValue("wallThickness", out double t))
            {
                section.dD = od;
                section.dT = t;
            }
        }

        private void SetChannelProperties(ISteelSection section, FrameProperties frameProp)
        {
            if (frameProp.Dimensions.TryGetValue("depth", out double d) &&
                frameProp.Dimensions.TryGetValue("width", out double bf) &&
                frameProp.Dimensions.TryGetValue("webThickness", out double tw) &&
                frameProp.Dimensions.TryGetValue("flangeThickness", out double tf))
            {
                section.dD = d;
                section.dBf = bf;
                section.dTw = tw;
                section.dTf = tf;
            }
        }

        private void SetAngleProperties(ISteelSection section, FrameProperties frameProp)
        {
            if (frameProp.Dimensions.TryGetValue("depth", out double d) &&
                frameProp.Dimensions.TryGetValue("width", out double b) &&
                frameProp.Dimensions.TryGetValue("thickness", out double t))
            {
                section.dD = d;
                section.dB = b;
                section.dT = t;
            }
        }
    }
}
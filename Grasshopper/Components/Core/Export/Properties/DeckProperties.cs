using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Core.Models.Properties;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class DeckPropertiesComponent : ComponentBase
    {
        public DeckPropertiesComponent()
          : base("Deck Properties", "DeckProps",
              "Creates deck properties for composite and non-composite metal deck floor systems",
              "IMEG", "Properties")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Deck Type", "DT", "Deck type designation (e.g., 'VULCRAFT 2VL')", GH_ParamAccess.item, "VULCRAFT 2VL");
            pManager.AddGenericParameter("Material", "M", "Material for the deck steel", GH_ParamAccess.item);
            pManager.AddNumberParameter("Rib Depth", "D", "Depth of the deck ribs (in inches)", GH_ParamAccess.item, 3.0);
            pManager.AddNumberParameter("Rib Width Top", "WT", "Top width of deck ribs (in inch es)", GH_ParamAccess.item, 7.0);
            pManager.AddNumberParameter("Rib Width Bottom", "WB", "Bottom width of deck ribs (in inches)", GH_ParamAccess.item, 5.0);
            pManager.AddNumberParameter("Rib Spacing", "S", "Spacing between deck ribs (in inches)", GH_ParamAccess.item, 12.0);
            pManager.AddNumberParameter("Shear Thickness", "ST", "Deck shear thickness (in inches)", GH_ParamAccess.item, 0.035);
            pManager.AddNumberParameter("Unit Weight", "W", "Deck unit weight (in pcf)", GH_ParamAccess.item, 2.3);

            // Make all parameters optional
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Deck Properties", "DP", "Deck properties for floor systems", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string deckType = "";
            object materialObj = null;
            double ribDepth = 0.0;
            double ribWidthTop = 0.0;
            double ribWidthBottom = 0.0;
            double ribSpacing = 0.0;
            double shearThickness = 0.0;
            double unitWeight = 0.0;

            DA.GetData(0, ref deckType);
            DA.GetData(1, ref materialObj);
            DA.GetData(2, ref ribDepth);
            DA.GetData(3, ref ribWidthTop);
            DA.GetData(4, ref ribWidthBottom);
            DA.GetData(5, ref ribSpacing);
            DA.GetData(6, ref shearThickness);
            DA.GetData(7, ref unitWeight);

            // Extract material
            Material material = ExtractMaterial(materialObj);

            // Create the deck properties
            DeckProperties deckProps = new DeckProperties();

            // Only override defaults if values are provided
            if (!string.IsNullOrEmpty(deckType))
                deckProps.DeckType = deckType;

            // Set material ID if material is provided
            if (material != null)
                deckProps.MaterialID = material.Id;

            if (ribDepth > 0)
                deckProps.RibDepth = ribDepth;

            if (ribWidthTop > 0)
                deckProps.RibWidthTop = ribWidthTop;

            if (ribWidthBottom > 0)
                deckProps.RibWidthBottom = ribWidthBottom;

            if (ribSpacing > 0)
                deckProps.RibSpacing = ribSpacing;

            if (shearThickness > 0)
                deckProps.DeckShearThickness = shearThickness;

            if (unitWeight > 0)
                deckProps.DeckUnitWeight = unitWeight;

            // Output the deck properties
            DA.SetData(0, new GH_DeckProperties(deckProps));
        }

        private Material ExtractMaterial(object obj)
        {
            if (obj == null) return null;

            // Direct type check
            if (obj is Material directMaterial)
                return directMaterial;

            // Using GooWrapper
            if (obj is GH_Material ghMaterial)
                return ghMaterial.Value;

            // Handle IGH_Goo objects that can be cast to Material
            if (obj is Grasshopper.Kernel.Types.IGH_Goo goo && goo.CastTo<Material>(out var castMaterial))
            {
                return castMaterial;
            }

            // Log warning and return null if we couldn't extract a material
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                $"Could not extract Material from input: {obj?.GetType().Name ?? "null"}");
            return null;
        }

        public override Guid ComponentGuid => new Guid("B1C2D3E4-F5A6-7890-1234-56789ABCDEF0");
    }
}
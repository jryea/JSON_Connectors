using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Core.Models.Properties;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class MaterialCollectorComponent : ComponentBase
    {
        public MaterialCollectorComponent()
          : base("Materials", "Materials",
              "Creates material definitions for the structural model",
              "IMEG", "Properties")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Names for each material", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Material types (Concrete, Steel)", GH_ParamAccess.list);
            pManager.AddTextParameter("Symmetry", "S", "Directional symmetry type (Isotropic, Orthotropic, Anisotropic)", GH_ParamAccess.list, "Isotropic");
            pManager.AddNumberParameter("Density", "D", "Material density (pcf)", GH_ParamAccess.list, 150.0);
            pManager.AddNumberParameter("E-Modulus", "E", "Elastic modulus (psi)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Poisson", "ν", "Poisson's ratio", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thermal Coef", "α", "Coefficient of thermal expansion (1/°F)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Shear Modulus", "G", "Shear modulus (psi)", GH_ParamAccess.list);
            pManager.AddGenericParameter("Properties", "P", "Material specific properties (ConcreteProperties or SteelProperties)", GH_ParamAccess.list);

            // Make some parameters optional
            pManager[1].Optional = true; // Types
            pManager[2].Optional = true; // Symmetry
            pManager[3].Optional = true; // Density
            pManager[4].Optional = true; // E-Modulus
            pManager[5].Optional = true; // Poisson
            pManager[6].Optional = true; // Thermal Coefficient
            pManager[7].Optional = true; // Shear Modulus
            pManager[8].Optional = true; // Properties
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Materials", "M", "Material definitions", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> names = new List<string>();
            List<string> types = new List<string>();
            List<string> symmetryTypes = new List<string>();
            List<double> densities = new List<double>();
            List<double> eModuli = new List<double>();
            List<double> poissons = new List<double>();
            List<double> thermalCoefs = new List<double>();
            List<double> shearModuli = new List<double>();
            List<object> propObjs = new List<object>();

            if (!DA.GetDataList(0, names)) return;
            DA.GetDataList(1, types);
            DA.GetDataList(2, symmetryTypes);
            DA.GetDataList(3, densities);
            DA.GetDataList(4, eModuli);
            DA.GetDataList(5, poissons);
            DA.GetDataList(6, thermalCoefs);
            DA.GetDataList(7, shearModuli);
            DA.GetDataList(8, propObjs);

            // Extend lists to match names length
            EnsureListLength(ref types, names.Count, types.Count > 0 ? types[types.Count - 1] : "Concrete");
            EnsureListLength(ref symmetryTypes, names.Count, symmetryTypes.Count > 0 ? symmetryTypes[symmetryTypes.Count - 1] : "Isotropic");
            EnsureListLength(ref densities, names.Count, densities.Count > 0 ? densities[densities.Count - 1] : 0.0);
            EnsureListLength(ref eModuli, names.Count, eModuli.Count > 0 ? eModuli[eModuli.Count - 1] : 0.0);
            EnsureListLength(ref poissons, names.Count, poissons.Count > 0 ? poissons[poissons.Count - 1] : 0.0);
            EnsureListLength(ref thermalCoefs, names.Count, thermalCoefs.Count > 0 ? thermalCoefs[thermalCoefs.Count - 1] : 0.0);
            EnsureListLength(ref shearModuli, names.Count, shearModuli.Count > 0 ? shearModuli[shearModuli.Count - 1] : 0.0);

            // Extend properties list if needed
            if (propObjs.Count > 0 && propObjs.Count < names.Count)
            {
                object lastProps = propObjs[propObjs.Count - 1];
                while (propObjs.Count < names.Count)
                    propObjs.Add(lastProps);
            }

            List<GH_Material> materials = new List<GH_Material>();
            for (int i = 0; i < names.Count; i++)
            {
                // Parse material type
                MaterialType materialType;
                if (!Enum.TryParse(types[i], true, out materialType))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Unknown material type: {types[i]}, defaulting to Concrete");
                    materialType = MaterialType.Concrete;
                }

                // Parse directional symmetry type
                DirectionalSymmetryType symmetryType;
                if (!Enum.TryParse(symmetryTypes[i], true, out symmetryType))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Unknown symmetry type: {symmetryTypes[i]}, defaulting to Isotropic");
                    symmetryType = DirectionalSymmetryType.Isotropic;
                }

                // Create base material - this initializes default properties based on type
                Material material = new Material(names[i], materialType);

                // Override properties only if non-zero values are provided
                if (densities[i] > 0)
                    material.WeightPerUnitVolume = densities[i];

                if (eModuli[i] > 0)
                    material.ElasticModulus = eModuli[i];

                if (poissons[i] > 0)
                    material.PoissonsRatio = poissons[i];

                if (thermalCoefs[i] > 0)
                    material.CoefficientOfThermalExpansion = thermalCoefs[i];

                if (shearModuli[i] > 0)
                    material.ShearModulus = shearModuli[i];

                // Set symmetry type (always override since it's an enum)
                material.DirectionalSymmetryType = symmetryType;

                // Apply material-specific properties if provided
                if (propObjs.Count > i && propObjs[i] != null)
                {
                    if (materialType == MaterialType.Concrete)
                    {
                        ConcreteProperties concreteProps = ExtractConcreteProperties(propObjs[i]);
                        if (concreteProps != null)
                        {
                            material.ConcreteProps = concreteProps;
                        }
                    }
                    else if (materialType == MaterialType.Steel)
                    {
                        SteelProperties steelProps = ExtractSteelProperties(propObjs[i]);
                        if (steelProps != null)
                        {
                            material.SteelProps = steelProps;
                        }
                    }
                }

                materials.Add(new GH_Material(material));
            }

            DA.SetDataList(0, materials);
        }

        private void EnsureListLength<T>(ref List<T> list, int targetLength, T defaultValue)
        {
            if (list.Count < targetLength)
            {
                while (list.Count < targetLength)
                    list.Add(defaultValue);
            }
        }

        private ConcreteProperties ExtractConcreteProperties(object obj)
        {
            // Direct type check
            if (obj is ConcreteProperties props)
                return props;

            // For GH_ConcreteProperties wrapper (to be created)
            if (obj is GH_ConcreteProperties ghProps)
                return ghProps.Value;

            // Try to cast from IGH_Goo
            if (obj is Grasshopper.Kernel.Types.IGH_Goo goo)
            {
                ConcreteProperties result = null;
                if (goo.CastTo(out result))
                    return result;
            }

            // If nothing worked, return null and let the calling method handle it
            return null;
        }

        private SteelProperties ExtractSteelProperties(object obj)
        {
            // Direct type check
            if (obj is SteelProperties props)
                return props;

            // For GH_SteelProperties wrapper (to be created)
            if (obj is GH_SteelProperties ghProps)
                return ghProps.Value;

            // Try to cast from IGH_Goo
            if (obj is Grasshopper.Kernel.Types.IGH_Goo goo)
            {
                SteelProperties result = null;
                if (goo.CastTo(out result))
                    return result;
            }

            // If nothing worked, return null and let the calling method handle it
            return null;
        }

        public override Guid ComponentGuid => new Guid("E8F2A9B3-D67C-45F1-BA8E-C95D30A2B714");
    }
}
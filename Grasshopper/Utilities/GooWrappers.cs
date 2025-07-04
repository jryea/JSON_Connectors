﻿using System;
using Grasshopper.Kernel.Types;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Metadata;
using Core.Models.Loads;
using Core.Models;
using Core.Models.Geometry;
using static Core.Models.Properties.Modifiers;

namespace Grasshopper.Utilities
{
    // Base class for all Grasshopper Goo wrappers in JSON Connectors
    public abstract class GH_ModelGoo<T> : GH_Goo<T> where T : class
    {
        public GH_ModelGoo() { }
        public GH_ModelGoo(T value) { Value = value; }

        public override bool IsValid => Value != null;
        public override string TypeName => $"{typeof(T).Name}";
        public override string TypeDescription => $"JSON Connectors {typeof(T).Name}";

        public override IGH_Goo Duplicate() => (IGH_Goo)Activator.CreateInstance(GetType(), Value);
        public override string ToString() => Value?.ToString() ?? "Null";

        // Standard implementation for all wrappers
        public override bool CastFrom(object source)
        {
            if (source is T value)
            {
                Value = value;
                return true;
            }
            return false;
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(T)))
            {
                target = (Q)(object)Value;
                return true;
            }
            return false;
        }
    }

    #region Layout Elements

    public class GH_Grid : GH_ModelGoo<Grid>
    {
        public GH_Grid() { }
        public GH_Grid(Grid grid) : base(grid) { }

        public override IGH_Goo Duplicate() => new GH_Grid(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Grid";
            return $"Grid: {Value.Name}";
        }
    }

    public class GH_Level : GH_ModelGoo<Level>
    {
        public GH_Level() { }
        public GH_Level(Level level) : base(level) { }

        public override IGH_Goo Duplicate() => new GH_Level(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Level";
            return $"Level: {Value.Name}, Elev: {Value.Elevation}";
        }
    }

    public class GH_FloorType : GH_ModelGoo<FloorType>
    {
        public GH_FloorType() { }
        public GH_FloorType(FloorType floorType) : base(floorType) { }

        public override IGH_Goo Duplicate() => new GH_FloorType(Value);

        public override string ToString()
        {
            if (Value == null) return "Null FloorType";
            return $"FloorType: {Value.Name}";
        }
    }

    public class GH_ModelLayoutContainer : GH_ModelGoo<ModelLayoutContainer>
    {
        public GH_ModelLayoutContainer() { }
        public GH_ModelLayoutContainer(ModelLayoutContainer container) : base(container) { }

        public override IGH_Goo Duplicate() => new GH_ModelLayoutContainer(Value);

        public override string ToString()
        {
            if (Value == null) return "Null LayoutContainer";
            return $"Layout: {Value.Grids.Count} grids, {Value.Levels.Count} levels, {Value.FloorTypes.Count} floorTypes";
        }
    }

    #endregion

    #region Structural Elements

    public class GH_Beam : GH_ModelGoo<Beam>
    {
        public GH_Beam() { }
        public GH_Beam(Beam beam) : base(beam) { }

        public override IGH_Goo Duplicate() => new GH_Beam(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Beam";
            return $"Beam: Level={Value.LevelId}, Props={Value.FramePropertiesId}, Lateral={Value.IsLateral}";
        }
    }

    public class GH_Column : GH_ModelGoo<Column>
    {
        public GH_Column() { }
        public GH_Column(Column column) : base(column) { }

        public override IGH_Goo Duplicate() => new GH_Column(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Column";
            return $"Column: Base={Value.BaseLevelId}, Top={Value.TopLevelId}, Props={Value.FramePropertiesId}";
        }
    }

    public class GH_Brace : GH_ModelGoo<Brace>
    {
        public GH_Brace() { }
        public GH_Brace(Brace brace) : base(brace) { }

        public override IGH_Goo Duplicate() => new GH_Brace(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Brace";
            return $"Brace: Base={Value.BaseLevelId}, Top={Value.TopLevelId}, Props={Value.FramePropertiesId}";
        }
    }

    public class GH_Wall : GH_ModelGoo<Wall>
    {
        public GH_Wall() { }
        public GH_Wall(Wall wall) : base(wall) { }

        public override IGH_Goo Duplicate() => new GH_Wall(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Wall";
            return $"Wall: Props={Value.PropertiesId}, Points: {Value.Points.Count}";
        }
    }

    public class GH_Floor : GH_ModelGoo<Floor>
    {
        public GH_Floor() { }
        public GH_Floor(Floor floor) : base(floor) { }

        public override IGH_Goo Duplicate() => new GH_Floor(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Floor";
            return $"Floor: Level={Value.LevelId}, Props={Value.FloorPropertiesId}, Diaphragm={Value.DiaphragmId}";
        }
    }

    public class GH_IsolatedFooting : GH_ModelGoo<IsolatedFooting>
    {
        public GH_IsolatedFooting() { }
        public GH_IsolatedFooting(IsolatedFooting footing) : base(footing) { }

        public override IGH_Goo Duplicate() => new GH_IsolatedFooting(Value);

        public override string ToString()
        {
            if (Value == null) return "Null IsolatedFooting";
            Point3D pt = Value.Point;
            return $"IsolatedFooting: Point=({pt?.X:F2}, {pt?.Y:F2}, {pt?.Z:F2}), Level Id = {Value.LevelId}, W×D×T={Value.Width:F1}×{Value.Width:F1}×{Value.Thickness:F1}";
        }
    }

    public class GH_ElementContainer : GH_ModelGoo<ElementContainer>
    {
        public GH_ElementContainer() { }
        public GH_ElementContainer(ElementContainer container) : base(container) { }

        public override IGH_Goo Duplicate() => new GH_ElementContainer(Value);

        public override string ToString()
        {
            if (Value == null) return "Null ElementContainer";
            int totalElements = Value.Beams.Count + Value.Columns.Count +
                Value.Walls.Count + Value.Floors.Count + Value.Braces.Count +
                Value.IsolatedFootings.Count;

            return $"Elements: {totalElements} total";
        }
    }

    #endregion

    #region Properties

    public class GH_Material : GH_ModelGoo<Material>
    {
        public GH_Material() { }
        public GH_Material(Material material) : base(material) { }

        public override IGH_Goo Duplicate() => new GH_Material(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Material";
            return $"Material: {Value.Name}, Type: {Value.Type}";
        }
    }

    public class GH_ConcreteProperties : GH_ModelGoo<ConcreteProperties>
    {
        public GH_ConcreteProperties() { }
        public GH_ConcreteProperties(ConcreteProperties props) : base(props) { }

        public override IGH_Goo Duplicate() => new GH_ConcreteProperties(Value);

        public override string ToString()
        {
            if (Value == null) return "Null ConcreteProperties";
            return $"ConcreteProps: f'c={Value.Fc} psi, {Value.WeightClass}";
        }
    }

    public class GH_SteelProperties : GH_ModelGoo<SteelProperties>
    {
        public GH_SteelProperties() { }
        public GH_SteelProperties(SteelProperties props) : base(props) { }

        public override IGH_Goo Duplicate() => new GH_SteelProperties(Value);

        public override string ToString()
        {
            if (Value == null) return "Null SteelProperties";
            return $"SteelProps: Fy={Value.Fy} psi, Fu={Value.Fu} psi, {Value.Grade}";
        }
    }

    public class GH_WallProperties : GH_ModelGoo<WallProperties>
    {
        public GH_WallProperties() { }
        public GH_WallProperties(WallProperties props) : base(props) { }

        public override IGH_Goo Duplicate() => new GH_WallProperties(Value);

        public override string ToString()
        {
            if (Value == null) return "Null WallProperties";
            return $"WallProps: {Value.Name}, Mat: {Value.MaterialId}, t={Value.Thickness}";
        }
    }

    public class GH_FloorProperties : GH_ModelGoo<FloorProperties>
    {
        public GH_FloorProperties() { }
        public GH_FloorProperties(FloorProperties props) : base(props) { }

        public override IGH_Goo Duplicate() => new GH_FloorProperties(Value);

        public override string ToString()
        {
            if (Value == null) return "Null FloorProperties";
            return $"FloorProps: {Value.Name}, Type: {Value.Type}, t={Value.Thickness}";
        }
    }

    public class GH_DeckProperties : GH_ModelGoo<DeckProperties>
    {
        public GH_DeckProperties() { }
        public GH_DeckProperties(DeckProperties props) : base(props) { }

        public override IGH_Goo Duplicate() => new GH_DeckProperties(Value);

        public override string ToString()
        {
            if (Value == null) return "Null DeckProperties";
            return $"DeckProps: {Value.DeckType}, Depth={Value.RibDepth}\"";
        }
    }

    public class GH_ShearStudProperties : GH_ModelGoo<ShearStudProperties>
    {
        public GH_ShearStudProperties() { }
        public GH_ShearStudProperties(ShearStudProperties props) : base(props) { }

        public override IGH_Goo Duplicate() => new GH_ShearStudProperties(Value);

        public override string ToString()
        {
            if (Value == null) return "Null ShearStudProperties";
            return $"ShearStudProps: Ø{Value.ShearStudDiameter}\"×{Value.ShearStudHeight}\"";
        }
    }

    public class GH_Diaphragm : GH_ModelGoo<Diaphragm>
    {
        public GH_Diaphragm() { }
        public GH_Diaphragm(Diaphragm diaphragm) : base(diaphragm) { }

        public override IGH_Goo Duplicate() => new GH_Diaphragm(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Diaphragm";
            return $"Diaphragm: {Value.Name}, Type: {Value.Type}";
        }
    }

    public class GH_FrameProperties : GH_ModelGoo<FrameProperties>
    {
        public GH_FrameProperties() { }
        public GH_FrameProperties(FrameProperties props) : base(props) { }

        public override IGH_Goo Duplicate() => new GH_FrameProperties(Value);

        public override string ToString()
        {
            if (Value == null) return "Null FrameProperties";
            return $"FrameProps: {Value.Name}, Mat: {Value.MaterialId}";
        }
    }

    public class GH_ConcreteFrameProperties : GH_ModelGoo<ConcreteFrameProperties>
    {
        public GH_ConcreteFrameProperties() { }
        public GH_ConcreteFrameProperties(ConcreteFrameProperties props) : base(props) { }

        public override IGH_Goo Duplicate() => new GH_ConcreteFrameProperties(Value);

        public override string ToString()
        {
            if (Value == null) return "Null ConcreteFrameProperties";
            return $"ConcreteFrameProps: {Value.SectionType}, {Value.SectionName}";
        }
    }

    public class GH_SteelFrameProperties : GH_ModelGoo<SteelFrameProperties>
    {
        public GH_SteelFrameProperties() { }
        public GH_SteelFrameProperties(SteelFrameProperties props) : base(props) { }

        public override IGH_Goo Duplicate() => new GH_SteelFrameProperties(Value);

        public override string ToString()
        {
            if (Value == null) return "Null SteelFrameProperties";
            return $"SteelFrameProps: {Value.SectionType}, {Value.SectionName}";
        }
    }

    public class GH_PropertiesContainer : GH_ModelGoo<PropertiesContainer>
    {
        public GH_PropertiesContainer() { }
        public GH_PropertiesContainer(PropertiesContainer container) : base(container) { }

        public override IGH_Goo Duplicate() => new GH_PropertiesContainer(Value);

        public override string ToString()
        {
            if (Value == null) return "Null PropertiesContainer";
            return $"Props: {Value.Materials.Count} materials, {Value.FrameProperties.Count} frames, {Value.FloorProperties.Count} floors, {Value.WallProperties.Count} walls, {Value.Diaphragms.Count} diaphragms";
        }
    }
    public class GH_ETABSFrameModifiers : GH_ModelGoo<FrameModifiers>
    {
        public GH_ETABSFrameModifiers() { }
        public GH_ETABSFrameModifiers(FrameModifiers modifiers) : base(modifiers) { }

        public override IGH_Goo Duplicate() => new GH_ETABSFrameModifiers(Value);

        public override string ToString()
        {
            if (Value == null) return "Null ETABSFrameModifiers";
            return $"ETABS Frame Modifiers";
        }
    }

    public class GH_ShellModifiers : GH_ModelGoo<ShellModifiers>
    {
        public GH_ShellModifiers() { }
        public GH_ShellModifiers(ShellModifiers modifiers) : base(modifiers) { }

        public override IGH_Goo Duplicate() => new GH_ShellModifiers(Value);

        public override string ToString()
        {
            if (Value == null) return "Null ETABSShellModifiers";
            return $"ETABS Shell Modifiers";
        }
    }

    #endregion

    #region Loads

    public class GH_LoadDefinition : GH_ModelGoo<LoadDefinition>
    {
        public GH_LoadDefinition() { }
        public GH_LoadDefinition(LoadDefinition load) : base(load) { }

        public override IGH_Goo Duplicate() => new GH_LoadDefinition(Value);

        public override string ToString()
        {
            if (Value == null) return "Null LoadDefinition";
            return $"Load: {Value.Name}, Type: {Value.Type}";
        }
    }

    public class GH_SurfaceLoad : GH_ModelGoo<SurfaceLoad>
    {
        public GH_SurfaceLoad() { }
        public GH_SurfaceLoad(SurfaceLoad load) : base(load) { }

        public override IGH_Goo Duplicate() => new GH_SurfaceLoad(Value);

        public override string ToString()
        {
            if (Value == null) return "Null SurfaceLoad";
            return $"SurfaceLoad: Name={Value.Name}, Dead={Value.DeadLoadId}, Live={Value.LiveLoadId}";
        }
    }

    public class GH_LoadCombination : GH_ModelGoo<LoadCombination>
    {
        public GH_LoadCombination() { }
        public GH_LoadCombination(LoadCombination combo) : base(combo) { }

        public override IGH_Goo Duplicate() => new GH_LoadCombination(Value);

        public override string ToString()
        {
            if (Value == null) return "Null LoadCombination";
            return $"LoadCombo: Def={Value.LoadDefinitionIds}";
        }
    }

    public class GH_LoadContainer : GH_ModelGoo<LoadContainer>
    {
        public GH_LoadContainer() { }
        public GH_LoadContainer(LoadContainer container) : base(container) { }

        public override IGH_Goo Duplicate() => new GH_LoadContainer(Value);

        public override string ToString()
        {
            if (Value == null) return "Null LoadContainer";
            return $"Loads: {Value.LoadDefinitions.Count} defs, {Value.SurfaceLoads.Count} surfaces, {Value.LoadCombinations.Count} combos";
        }
    }

    #endregion

    #region Metadata

    public class GH_ProjectInfo : GH_ModelGoo<ProjectInfo>
    {
        public GH_ProjectInfo() { }
        public GH_ProjectInfo(ProjectInfo info) : base(info) { }

        public override IGH_Goo Duplicate() => new GH_ProjectInfo(Value);

        public override string ToString()
        {
            if (Value == null) return "Null ProjectInfo";
            return $"Project: {Value.ProjectName}, Created: {Value.CreationDate:yyyy-MM-dd}";
        }
    }

    public class GH_Units : GH_ModelGoo<Units>
    {
        public GH_Units() { }
        public GH_Units(Units units) : base(units) { }

        public override IGH_Goo Duplicate() => new GH_Units(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Units";
            return $"Units: {Value.Length}/{Value.Force}/{Value.Temperature}";
        }
    }

    public class GH_MetadataContainer : GH_ModelGoo<MetadataContainer>
    {
        public GH_MetadataContainer() { }
        public GH_MetadataContainer(MetadataContainer container) : base(container) { }

        public override IGH_Goo Duplicate() => new GH_MetadataContainer(Value);

        public override string ToString()
        {
            if (Value == null) return "Null MetadataContainer";
            string projectName = Value.ProjectInfo?.ProjectName ?? "No Project";
            string units = Value.Units?.Length ?? "No Units";
            return $"Metadata: Project={projectName}, Units={units}";
        }
    }

    #endregion

    #region Model

    public class GH_BaseModel : GH_ModelGoo<BaseModel>
    {
        public GH_BaseModel() { }
        public GH_BaseModel(BaseModel model) : base(model) { }

        public override IGH_Goo Duplicate() => new GH_BaseModel(Value);

        public override string ToString()
        {
            if (Value == null) return "Null BaseModel";
            int elementCount = 0;
            if (Value.Elements != null)
            {
                elementCount = Value.Elements.Beams.Count + Value.Elements.Columns.Count +
                    Value.Elements.Walls.Count + Value.Elements.Floors.Count + Value.Elements.Braces.Count;
            }
            string projectName = Value.Metadata?.ProjectInfo?.ProjectName ?? "No Project";
            return $"Model: {projectName}, {elementCount} elements, ID: {Value.Id}";
        }
    }

    #endregion
}
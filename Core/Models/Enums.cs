using System.Text.Json.Serialization;

namespace Core.Models
{
    public enum MaterialType
    {
        Concrete,
        Steel,
    }

    public enum DirectionalSymmetryType
    {
        Isotropic,
        Orthotropic,
        Anisotropic
    }

    public enum WeightClass
    {
        Normal,
        Lightweight
    }

    public enum DiaphragmType
    {
        Rigid,
        SemiRigid,
    }

    public enum StructuralFloorType
    {
        Slab,
        FilledDeck,
        UnfilledDeck,
        SolidSlabDeck
    }

    public enum ModelingType
    {
        ShellThin,
        ShellThick,
        Membrane,
        Layered
    }

    public enum SlabType
    {
        Slab,
        Drop,
        Stiff,
        Ribbed,
        Waffle,
        Mat,
        Footing
    }

    #region Frame Enums
    public enum FrameMaterialType
    {
        Steel,
        Concrete,
    }

    public enum SteelSectionType
    {
        W,
        HSS,
        PIPE,
        C,
        L,
        WT,
        ST,
        MC,
        HP
    }

    public enum ConcreteSectionType
    {
        Rectangular,
        Circular,
        TShaped,
        LShaped,
        Custom
    }
    #endregion

    public enum LoadType
    {
        Dead,
        Live,
        Snow,
        Wind,
        Seismic,
        Thermal,
        Other
    }
}
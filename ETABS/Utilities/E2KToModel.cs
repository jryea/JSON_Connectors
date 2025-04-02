using Core.Models;
using ETABS.Import.Elements;
using ETABS.Import.Loads;
using ETABS.Import.Metadata;
using ETABS.Import.ModelLayout;
using ETABS.Import.Properties;
using System.Collections.Generic;

namespace ETABS.Utilities
{
    public class E2KToModel
    {
        private readonly BeamImport _beamImport = new BeamImport();
        private readonly ColumnImport _columnImport = new ColumnImport();
        private readonly FloorImport _floorImport = new FloorImport();
        private readonly ProjectInfoImport _projectInfoImport = new ProjectInfoImport();
        private readonly StoriesImport _storiesImport = new StoriesImport();
        private readonly GridsImport _gridsImport = new GridsImport();
        private readonly MaterialsImport _materialsImport = new MaterialsImport();
        private readonly FramePropertiesImport _framePropertiesImport = new FramePropertiesImport();
        private readonly DiaphragmsImport _diaphragmsImport = new DiaphragmsImport();
        private readonly LoadDefinitionsImport _loadDefinitionsImport = new LoadDefinitionsImport();
        private readonly SurfaceLoadsImport _surfaceLoadsImport = new SurfaceLoadsImport();
        private readonly LoadCombinationsImport _loadCombinationsImport = new LoadCombinationsImport();

        public BaseModel ImportFromE2K(Dictionary<string, string> e2kSections)
        {
            BaseModel model = new BaseModel();

            return model;
        }
    }
}
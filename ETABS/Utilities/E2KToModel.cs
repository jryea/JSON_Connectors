using Core.Models;
using ETABS.Export.Elements;
using ETABS.Export.Loads;
using ETABS.Export.Metadata;
using ETABS.Export.ModelLayout;
using ETABS.Export.Properties;
using System.Collections.Generic;

public class E2KToModel
{
    private readonly ControlsImport _controlsImport = new ControlsImport();
    private readonly StoriesImport _storiesImport = new StoriesImport();
    private readonly GridsImport _gridsImport = new GridsImport();
    private readonly MaterialsImport _materialsImport = new MaterialsImport();
    private readonly FrameSectionsImport _frameSectionsImport = new FrameSectionsImport();
    private readonly DiaphragmsImport _diaphragmsImport = new DiaphragmsImport();
    private readonly PointCoordinatesImport _pointCoordinatesImport = new PointCoordinatesImport();
    private readonly LineConnectivitiesImport _lineConnectivitiesImport = new LineConnectivitiesImport();
    private readonly AreaConnectivitiesImport _areaConnectivitiesImport = new AreaConnectivitiesImport();
    private readonly ShellPropertiesImport _shellPropertiesImport = new ShellPropertiesImport();
    private readonly LoadPatternsImport _loadPatternsImport = new LoadPatternsImport();
    private readonly LoadCasesImport _loadCasesImport = new LoadCasesImport();
    private readonly LoadCombinationsImport _loadCombinationsImport = new LoadCombinationsImport();

    public BaseModel ImportFromE2K(Dictionary<string, string> e2kSections)
    {
        BaseModel model = new BaseModel();

        // Process each section and update the model
        if (e2kSections.TryGetValue("CONTROLS", out string controlsSection))
            _controlsImport.ImportControls(controlsSection, model);

        if (e2kSections.TryGetValue("STORIES - IN SEQUENCE FROM TOP", out string storiesSection))
            _storiesImport.ImportStories(storiesSection, model);

        if (e2kSections.TryGetValue("GRIDS", out string gridsSection))
            _gridsImport.ImportGrids(gridsSection, model);

        // Process other sections similarly

        return model;
    }
}
using System.Collections.Generic;

namespace ETABS.ToETABS.Elements
{
    // Interface for classes that convert element connectivity information to ETABS E2K format
    public interface IConnectivityToETABS
    {
        // Converts the connectivities for a specific element type to E2K format
        string ExportConnectivities();

        // Gets the ID mapping from source element IDs to E2K element IDs
        Dictionary<string, string> GetIdMapping();
    }
}
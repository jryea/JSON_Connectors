using System.Collections.Generic;

namespace ETABS.Import.Elements
{
    // Interface for classes that convert element assignment information to ETABS E2K format
    public interface IAssignmentImport
    {
        // Converts the assignments for a specific element type to E2K format
        string ExportAssignments(Dictionary<string, string> idMapping);
    }
}
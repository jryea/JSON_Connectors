using System;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Utilities;

public class IMEGToolbarInfo : GH_AssemblyInfo
{
    public override string Name => "JSON Connectors"; // Use a consistent name across assemblies

    public override Bitmap Icon => Helpers.GetLogo();

    public override string Description => "JSON connectors for structural interoperability";

    // Ensure this GUID is unique and consistent
    public override Guid Id => new Guid("48ce1b0b-18fc-45b7-abf4-22b110a37b64");

    public override string AuthorName => "IMEG";

    public override string AuthorContact => "https://www.imegcorp.com";
}
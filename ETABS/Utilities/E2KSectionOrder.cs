using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Core.Models;

namespace ETABS.Utilities
{
    // Class to specify the order of sections in an E2K file
    public class E2KSectionOrder
    {
        // List of section headers in the correct order
        private static readonly List<string> SectionOrder = new List<string>
        {
            "PROGRAM INFORMATION",
            "CONTROLS",
            "STORIES - IN SEQUENCE FROM TOP", 
            "GRIDS",
            "DIAPHRAGM NAMES",
            "MATERIAL PROPERTIES",
            "REBAR DEFINITIONS",
            "FRAME SECTIONS",
            "CONCRETE SECTIONS",
            "TENDON SECTIONS",
            "SLAB PROPERTIES",
            "DECK PROPERTIES",
            "WALL PROPERTIES",
            "LINK PROPERTIES",
            "PANEL ZONE PROPERTIES",
            "PIER/SPANDREL NAMES",
            "POINT COORDINATES",
            "LINE CONNECTIVITIES",
            "AREA CONNECTIVITIES",
            "GROUPS",
            "POINT ASSIGNS",
            "LINE ASSIGNS",
            "AREA ASSIGNS",
            "LOAD PATTERNS",
            "LOAD COMBINATIONS",
            "ANALYSIS OPTIONS",
            "MASS SOURCE",
            "FUNCTIONS",
            "GENERALIZED DISPLACEMENTS",
            "LOAD CASES",
            "STEEL DESIGN PREFERENCES",
            "STEEL DESIGN OVERWRITES",
            "CONCRETE DESIGN PREFERENCES",
            "COMPOSITE DESIGN PREFERENCES",
            "COMPOSITE COLUMN DESIGN PREFERENCES",
            "WALL DESIGN PREFERENCES",
            "CONCRETE SLAB DESIGN PREFERENCES",
            "DIMENSION LINES",
            "DEVELOPED ELEVATIONS",
            "TABLE SETS",
            "PROJECT INFORMATION",
            "LOG"
        };

       
        // Gets the index of a section in the predefined order
        public static int GetSectionOrderIndex(string sectionName)
        {
            for (int i = 0; i < SectionOrder.Count; i++)
            {
                if (sectionName.Equals(SectionOrder[i], StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return int.MaxValue; // Put sections not in the list at the end
        }

        /// <summary>
        /// Gets the list of sections in the correct order
        /// </summary>
        public static List<string> GetOrderedSections()
        {
            return new List<string>(SectionOrder);
        }
    }
}
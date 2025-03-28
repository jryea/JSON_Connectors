﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Core.Models;

namespace ETABS.Export
{
    /// <summary>
    /// Class to specify the order of sections in an E2K file
    /// </summary>
    public class E2KSectionOrder
    {
        // List of section headers in the correct order
        private static readonly List<string> SectionOrder = new List<string>
        {
            "PROGRAM INFORMATION",
            "CONTROLS",
            "STORIES - IN SEQUENCE FROM TOP",  // Updated to match the full section name
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

        /// <summary>
        /// Gets the index of a section in the predefined order
        /// </summary>
        /// <param name="sectionName">Name of the section</param>
        /// <returns>Index of the section in the order list, or int.MaxValue if not found</returns>
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
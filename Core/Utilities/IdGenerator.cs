﻿using System;

namespace Core.Utilities
{
 
    // Utility for generating unique IDs for structural model elements
    public static class IdGenerator
    {
        // Element type prefixes
        public static class Elements
        {
            public const string BEAM = "BM";
            public const string COLUMN = "COL";
            public const string WALL = "WL";
            public const string FLOOR = "FL";
            public const string BRACE = "BR";
            public const string ISOLATED_FOOTING = "IF";
            public const string CONTINUOUS_FOOTING = "CF";
            public const string PILE = "PL";
            public const string PIER = "PR";
            public const string DRILLED_PIER = "DP";
            public const string JOINT = "JT";
            public const string OPENING = "OP";
        }

        // Property type prefixes
        public static class Properties
        {
            public const string MATERIAL = "MAT";
            public const string WALL_PROPERTIES = "WP";
            public const string FLOOR_PROPERTIES = "FP";
            public const string FRAME_PROPERTIES = "FRP";
            public const string DIAPHRAGM = "DIA";
            public const string PIER_SPANDREL = "PS";
        }

        // Model layout prefixes
        public static class Layout
        {
            public const string GRID = "GR";
            public const string LEVEL = "LV";
            public const string FLOOR_TYPE = "FT";
            public const string FLOOR_LAYOUT = "FL";
        }

        // Loads prefixes
        public static class Loads
        {
            public const string LOAD_DEFINITION = "LD";
            public const string SURFACE_LOAD = "SL";
            public const string LOAD_COMBINATION = "LC";
        }

        // Generates a unique ID with a prefix for a specific element type
        public static string Generate(string prefix)
        {
            // Create a unique identifier using a GUID
            string uniquePart = Guid.NewGuid().ToString("N").Substring(0, 8);

            // Format: PREFIX-UNIQUEPART
            return $"{prefix}-{uniquePart}";
        }

        // Generates a unique model ID
        public static string GenerateModelId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
        }
    }
}
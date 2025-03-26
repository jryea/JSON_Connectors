using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Core.Models;

namespace ETABS.Export
{
    public class E2KInjector
    {
        // Dictionary to store custom E2K sections 
        private readonly Dictionary<string, string> _customSections = new Dictionary<string, string>();

        // Constructor
        public E2KInjector()
        {
        }

        // Parse a single E2K text string into sections
        public void ParseE2KContent(string rawE2KContent)
        {
            if (string.IsNullOrWhiteSpace(rawE2KContent))
                return;

            // Clear existing sections
            _customSections.Clear();

            // Use regex to find section headers (lines starting with $)
            var sectionPattern = new Regex(@"^\$ ([A-Z][A-Z0-9 _/]+)$", RegexOptions.Multiline);
            var matches = sectionPattern.Matches(rawE2KContent);

            if (matches.Count == 0)
                return; // No sections found

            // Process each section
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                string sectionName = match.Groups[1].Value.Trim();

                // Find the start and end indices for this section
                int startIndex = match.Index + match.Length;
                int endIndex;

                if (i < matches.Count - 1)
                {
                    // End at the start of the next section
                    endIndex = matches[i + 1].Index;
                }
                else
                {
                    // Last section - end at the end of the string
                    endIndex = rawE2KContent.Length;
                }

                // Extract the section content
                string content = rawE2KContent.Substring(startIndex, endIndex - startIndex).Trim();

                // Add to the dictionary
                _customSections[sectionName] = content;
            }
        }

        // Method to add a custom E2K section
        public void AddCustomSection(string sectionName, string content)
        {
            // If the section already exists, replace it
            _customSections[sectionName] = content;
        }

        // Method to inject custom sections into an existing E2K export
        public string InjectCustomSections(string baseE2kContent)
        {
            if (_customSections.Count == 0)
            {
                return baseE2kContent; // No custom sections to inject
            }

            var result = new StringBuilder();
            var lines = baseE2kContent.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            int i = 0;

            // Copy everything up to the first section marker
            while (i < lines.Length && !lines[i].StartsWith("$ "))
            {
                result.AppendLine(lines[i]);
                i++;
            }

            // Process the rest of the file
            while (i < lines.Length)
            {
                // Check if this is a section marker
                if (lines[i].StartsWith("$ ") && lines[i].Length > 2)
                {
                    string sectionName = lines[i].Substring(2).Trim();

                    // If we have a custom section for this, inject it
                    if (_customSections.TryGetValue(sectionName, out string customContent))
                    {
                        // Add the section marker
                        result.AppendLine(lines[i]);

                        // Add the custom content
                        result.AppendLine(customContent);

                        // Skip past the original section content
                        i++;
                        while (i < lines.Length &&
                               (!lines[i].StartsWith("$ ") || lines[i].Length <= 2))
                        {
                            i++;
                        }

                        continue;
                    }
                }

                // Copy the line as-is
                result.AppendLine(lines[i]);
                i++;
            }

            // Add any custom sections that weren't in the original file
            foreach (var section in _customSections)
            {
                if (!baseE2kContent.Contains($"$ {section.Key}"))
                {
                    result.AppendLine($"$ {section.Key}");
                    result.AppendLine(section.Value);
                    result.AppendLine();
                }
            }

            return result.ToString();
        }
    }
}
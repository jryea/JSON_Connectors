using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Core.Models;
//using ETABS.Export;

namespace ETABS.Utilities
{
    public class E2KInjector
    {
        // Dictionary to store custom E2K sections 
        private readonly Dictionary<string, string> _customSections = new Dictionary<string, string>();

        // Constructor
        public E2KInjector()
        {
        }

        // Injects custom E2K content into base E2K content
        public string InjectCustomE2K(string baseE2K, string customE2K)
        {
            if (string.IsNullOrWhiteSpace(customE2K))
                return baseE2K;

            // Parse the custom E2K content to extract its sections
            ParseE2KContent(customE2K);

            // Then inject those sections into the base E2K
            return InjectCustomSections(baseE2K);
        }

        // Parse a single E2K text string into sections
        public void ParseE2KContent(string rawE2KContent)
        {
            if (string.IsNullOrWhiteSpace(rawE2KContent))
                return;

            // Clear existing sections
            _customSections.Clear();

            // Use regex to find section headers (lines starting with $)
            Regex sectionPattern = new Regex(@"^\$ ([A-Z][A-Z0-9 _/\-]+)",
                RegexOptions.Multiline);
            MatchCollection matches = sectionPattern.Matches(rawE2KContent);

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

                // Format the content: add tab to first line if it doesn't have one
                var contentLines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (contentLines.Length > 0)
                {
                    var sb = new StringBuilder();

                    // Add tab to first line if it doesn't already have one
                    string firstLine = contentLines[0];
                    if (!firstLine.StartsWith("\t"))
                    {
                        sb.AppendLine("\t" + firstLine);
                    }
                    else
                    {
                        sb.AppendLine(firstLine);
                    }

                    // Add remaining lines
                    for (int j = 1; j < contentLines.Length; j++)
                    {
                        sb.AppendLine(contentLines[j]);
                    }

                    // Ensure there's a new line at the end
                    sb.AppendLine();

                    content = sb.ToString();
                }

                // Add to the dictionary
                _customSections[sectionName] = content;
            }
        }

        // Method to inject custom sections into an existing E2K export
        public string InjectCustomSections(string baseE2kContent)
        {
            if (_customSections.Count == 0)
            {
                return baseE2kContent; // No custom sections to inject
            }

            var result = new StringBuilder();
            var lines = baseE2kContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            int i = 0;

            // Copy everything up to the first section marker
            while (i < lines.Length && !lines[i].TrimStart().StartsWith("$"))
            {
                result.AppendLine(lines[i]);
                i++;
            }

            // Create a dictionary to store sections from base E2K
            var baseSections = new Dictionary<string, string>();
            string currentSection = null;
            var currentContent = new StringBuilder();

            // Process the original file and extract sections
            while (i < lines.Length)
            {
                string currentLine = lines[i];

                // Check if this is a section marker
                if (currentLine.TrimStart().StartsWith("$") && currentLine.Length > 2)
                {
                    // If we were processing a section, save it
                    if (currentSection != null)
                    {
                        baseSections[currentSection] = currentContent.ToString();
                        currentContent.Clear();
                    }

                    // Extract section name - everything after the $ and any spaces
                    currentSection = currentLine.TrimStart().Substring(1).Trim();
                }
                else if (currentSection != null)
                {
                    // Add this line to the current section content
                    currentContent.AppendLine(currentLine);
                }

                i++;
            }

            // Add the last section if any
            if (currentSection != null && currentContent.Length > 0)
            {
                baseSections[currentSection] = currentContent.ToString();
            }

            // Merge base and custom sections
            var mergedSections = new Dictionary<string, string>(baseSections);

            // Add or replace with custom sections
            foreach (var customSection in _customSections)
            {
                mergedSections[customSection.Key] = customSection.Value;
            }

            // Get all section names and sort them by the predefined order
            var orderedSections = mergedSections.Keys
                .Select(section => new { Name = section, Order = E2KSectionOrder.GetSectionOrderIndex(section) })
                .OrderBy(section => section.Order)
                .Select(section => section.Name);

            // Write the sections in the correct order
            foreach (var section in orderedSections)
            {
                result.AppendLine($"$ {section}");
                result.Append(mergedSections[section]);

                // Ensure there's a blank line after each section
                if (!mergedSections[section].EndsWith("\n\n"))
                {
                    result.AppendLine();
                }
            }

            return result.ToString();
        }
    }

}

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

        // Add this method to the E2KInjector class
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
            Regex sectionPattern = new System.Text.RegularExpressions.Regex(@"^\$ ([A-Z][A-Z0-9 _/\-]+)",
                System.Text.RegularExpressions.RegexOptions.Multiline);
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

                // Add to the dictionary
                _customSections[sectionName] = content;
            }
        }

        //// Method to add a custom E2K section
        //public void AddCustomSection(string sectionName, string content)
        //{
        //    // If the section already exists, replace it
        //    _customSections[sectionName] = content;
        //}

        // Method to inject custom sections into an existing E2K export
        public string InjectCustomSections(string baseE2kContent)
        {
            if (_customSections.Count == 0)
            {
                return baseE2kContent; // No custom sections to inject
            }

            var result = new System.Text.StringBuilder();
            var lines = baseE2kContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            int i = 0;

            // Copy everything up to the first section marker
            while (i < lines.Length && !lines[i].TrimStart().StartsWith("$"))
            {
                result.AppendLine(lines[i]);
                i++;
            }

            // Track sections we've already injected
            var injectedSections = new HashSet<string>();

            // Process the rest of the file
            while (i < lines.Length)
            {
                string currentLine = lines[i].TrimStart();

                // Check if this is a section marker
                if (currentLine.StartsWith("$") && currentLine.Length > 2)
                {
                    // Extract section name - everything after the $ and any spaces
                    string sectionLine = currentLine.Substring(1).Trim();

                    // Find matching custom section if any
                    string matchingSection = null;
                    foreach (var section in _customSections.Keys)
                    {
                        if (sectionLine.StartsWith(section))
                        {
                            matchingSection = section;
                            break;
                        }
                    }

                    // If we have a custom section for this, inject it
                    if (matchingSection != null)
                    {
                        // Add the section marker
                        result.AppendLine(lines[i]);

                        // Add the custom content
                        result.AppendLine(_customSections[matchingSection]);

                        // Mark this section as injected
                        injectedSections.Add(matchingSection);

                        // Skip past the original section content
                        i++;
                        while (i < lines.Length &&
                            (!lines[i].TrimStart().StartsWith("$") || lines[i].Trim().Length <= 1))
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
                if (!injectedSections.Contains(section.Key))
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
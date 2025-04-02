using System;
using System.Text.RegularExpressions;
using Core.Models.Metadata;

namespace ETABS.Import.Metadata
{
    // Imports project information from ETABS E2K file
    public class ProjectInfoImport
    {
        // Imports project information from E2K PROJECT INFORMATION section
    
        public ProjectInfo Import(string projectInfoSection)
        {
            var projectInfo = new ProjectInfo
            {
                CreationDate = DateTime.Now,
                SchemaVersion = "1.0" // Default schema version
            };

            if (string.IsNullOrWhiteSpace(projectInfoSection))
                return projectInfo;

            // Extract company name
            // Format: PROJECTINFO    COMPANYNAME "IMEG"    MODELNAME "TestProject"
            var companyNamePattern = new Regex(@"COMPANYNAME\s+""([^""]+)""",
                RegexOptions.Singleline);

            var companyNameMatch = companyNamePattern.Match(projectInfoSection);
            if (companyNameMatch.Success && companyNameMatch.Groups.Count >= 2)
            {
                // We don't have a company name field in ProjectInfo, 
                // but we could add it to an extended property if needed
                string companyName = companyNameMatch.Groups[1].Value;
            }

            // Extract model name
            var modelNamePattern = new Regex(@"MODELNAME\s+""([^""]+)""",
                RegexOptions.Singleline);

            var modelNameMatch = modelNamePattern.Match(projectInfoSection);
            if (modelNameMatch.Success && modelNameMatch.Groups.Count >= 2)
            {
                projectInfo.ProjectName = modelNameMatch.Groups[1].Value;
            }

            // Generate a unique project ID if none exists
            if (string.IsNullOrEmpty(projectInfo.ProjectId))
            {
                projectInfo.ProjectId = Guid.NewGuid().ToString();
            }

            return projectInfo;
        }

        // Extracts additional project information from PROGRAM INFORMATION and LOG sections
      
        public void ExtractAdditionalInfo(string programInfoSection, string logSection, ProjectInfo projectInfo)
        {
            // Extract ETABS version information
            if (!string.IsNullOrWhiteSpace(programInfoSection))
            {
                // Format: PROGRAM "ETABS" VERSION "21.2.0"
                var versionPattern = new Regex(@"PROGRAM\s+""ETABS""\s+VERSION\s+""([^""]+)""",
                    RegexOptions.Singleline);

                var versionMatch = versionPattern.Match(programInfoSection);
                if (versionMatch.Success && versionMatch.Groups.Count >= 2)
                {
                    string etabsVersion = versionMatch.Groups[1].Value;

                    // Use ETABS version as schema version if none exists
                    if (string.IsNullOrEmpty(projectInfo.SchemaVersion))
                    {
                        projectInfo.SchemaVersion = "ETABS " + etabsVersion;
                    }
                }
            }

            // Extract last saved date from LOG section
            if (!string.IsNullOrWhiteSpace(logSection))
            {
                // Format: ETABS Nonlinear 21.2.0 File saved as TestProject.EDB at 4/2/2025 8:44:53
                var savedDatePattern = new Regex(@"File saved as.*?at\s+([^$]+)",
                    RegexOptions.Singleline);

                var savedDateMatch = savedDatePattern.Match(logSection);
                if (savedDateMatch.Success && savedDateMatch.Groups.Count >= 2)
                {
                    string dateString = savedDateMatch.Groups[1].Value.Trim();

                    // Try to parse
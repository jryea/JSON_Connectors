using System.Collections.Generic;
using System.Text.RegularExpressions;

public class E2KParser
{
    public Dictionary<string, string> ParseE2K(string e2kContent)
    {
        Dictionary<string, string> sections = new Dictionary<string, string>();

        // Use regex to find section headers (lines starting with $)
        Regex sectionPattern = new Regex(@"^\$ ([A-Z][A-Z0-9 _/\-]+)",
            RegexOptions.Multiline);
        MatchCollection matches = sectionPattern.Matches(e2kContent);

        // Process each section
        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            string sectionName = match.Groups[1].Value.Trim();

            // Find section boundaries
            int startIndex = match.Index + match.Length;
            int endIndex = (i < matches.Count - 1) ? matches[i + 1].Index : e2kContent.Length;

            // Extract section content
            string content = e2kContent.Substring(startIndex, endIndex - startIndex).Trim();
            sections[sectionName] = content;
        }

        return sections;
    }
}
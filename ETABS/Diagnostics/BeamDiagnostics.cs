using System;
using System.Text;
using System.Collections.Generic;
using Core.Models;
using Core.Models.Elements;

namespace DiagnosticTools
{
    /// <summary>
    /// Utility class for diagnosing beam point issues
    /// </summary>
    public static class BeamDiagnostics
    {
        /// <summary>
        /// Generates a detailed diagnostic report about beam points in the model
        /// </summary>
        /// <param name="model">The structural model to analyze</param>
        /// <returns>Diagnostic report as formatted string</returns>
        public static string AnalyzeBeamPoints(BaseModel model)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("# BEAM POINTS DIAGNOSTIC REPORT");
            report.AppendLine("==============================");
            report.AppendLine();

            if (model?.Elements?.Beams == null || model.Elements.Beams.Count == 0)
            {
                report.AppendLine("No beams found in the model.");
                return report.ToString();
            }

            report.AppendLine($"Total beams: {model.Elements.Beams.Count}");
            report.AppendLine();

            // Analyze each beam
            for (int i = 0; i < model.Elements.Beams.Count; i++)
            {
                var beam = model.Elements.Beams[i];
                report.AppendLine($"## Beam {i + 1}");
                report.AppendLine($"ID: {beam.Id}");
                report.AppendLine($"Level ID: {beam.LevelId}");

                // Format beam points with high precision
                if (beam.StartPoint != null)
                    report.AppendLine($"StartPoint: X={beam.StartPoint.X:F15}, Y={beam.StartPoint.Y:F15}");
                else
                    report.AppendLine("StartPoint: NULL");

                if (beam.EndPoint != null)
                    report.AppendLine($"EndPoint: X={beam.EndPoint.X:F15}, Y={beam.EndPoint.Y:F15}");
                else
                    report.AppendLine("EndPoint: NULL");

                report.AppendLine();
            }

            // Check for point equality between beams
            report.AppendLine("## Point Equality Analysis");
            report.AppendLine("=========================");

            for (int i = 0; i < model.Elements.Beams.Count; i++)
            {
                var beam1 = model.Elements.Beams[i];

                for (int j = i + 1; j < model.Elements.Beams.Count; j++)
                {
                    var beam2 = model.Elements.Beams[j];

                    // Compare start points
                    if (beam1.StartPoint != null && beam2.StartPoint != null)
                    {
                        double distance = CalculateDistance(beam1.StartPoint, beam2.StartPoint);
                        bool exactMatch = beam1.StartPoint.X == beam2.StartPoint.X && beam1.StartPoint.Y == beam2.StartPoint.Y;
                        bool closeMatch = distance < 1e-6;

                        if (closeMatch)
                        {
                            report.AppendLine($"Beam {i + 1} StartPoint and Beam {j + 1} StartPoint are similar:");
                            report.AppendLine($"  Distance: {distance:E10}");
                            report.AppendLine($"  Exact match: {exactMatch}");
                            if (!exactMatch)
                            {
                                report.AppendLine($"  Difference X: {beam1.StartPoint.X - beam2.StartPoint.X:E10}");
                                report.AppendLine($"  Difference Y: {beam1.StartPoint.Y - beam2.StartPoint.Y:E10}");
                            }
                            report.AppendLine();
                        }
                    }

                    // Compare end points
                    if (beam1.EndPoint != null && beam2.EndPoint != null)
                    {
                        double distance = CalculateDistance(beam1.EndPoint, beam2.EndPoint);
                        bool exactMatch = beam1.EndPoint.X == beam2.EndPoint.X && beam1.EndPoint.Y == beam2.EndPoint.Y;
                        bool closeMatch = distance < 1e-6;

                        if (closeMatch)
                        {
                            report.AppendLine($"Beam {i + 1} EndPoint and Beam {j + 1} EndPoint are similar:");
                            report.AppendLine($"  Distance: {distance:E10}");
                            report.AppendLine($"  Exact match: {exactMatch}");
                            if (!exactMatch)
                            {
                                report.AppendLine($"  Difference X: {beam1.EndPoint.X - beam2.EndPoint.X:E10}");
                                report.AppendLine($"  Difference Y: {beam1.EndPoint.Y - beam2.EndPoint.Y:E10}");
                            }
                            report.AppendLine();
                        }
                    }

                    // Cross-compare start and end points
                    if (beam1.StartPoint != null && beam2.EndPoint != null)
                    {
                        double distance = CalculateDistance(beam1.StartPoint, beam2.EndPoint);
                        bool closeMatch = distance < 1e-6;

                        if (closeMatch)
                        {
                            report.AppendLine($"Beam {i + 1} StartPoint and Beam {j + 1} EndPoint are similar:");
                            report.AppendLine($"  Distance: {distance:E10}");
                            report.AppendLine();
                        }
                    }

                    if (beam1.EndPoint != null && beam2.StartPoint != null)
                    {
                        double distance = CalculateDistance(beam1.EndPoint, beam2.StartPoint);
                        bool closeMatch = distance < 1e-6;

                        if (closeMatch)
                        {
                            report.AppendLine($"Beam {i + 1} EndPoint and Beam {j + 1} StartPoint are similar:");
                            report.AppendLine($"  Distance: {distance:E10}");
                            report.AppendLine();
                        }
                    }
                }
            }

            // Check for unique coordinates among beams
            report.AppendLine("## Unique Coordinates Analysis");
            report.AppendLine("============================");

            var uniqueCoordinates = new Dictionary<string, List<string>>();

            foreach (var beam in model.Elements.Beams)
            {
                if (beam.StartPoint != null)
                {
                    // Round to 6 decimal places for tolerance
                    string key = $"{Math.Round(beam.StartPoint.X, 6)},{Math.Round(beam.StartPoint.Y, 6)}";
                    if (!uniqueCoordinates.ContainsKey(key))
                        uniqueCoordinates[key] = new List<string>();
                    uniqueCoordinates[key].Add($"Beam {beam.Id} StartPoint");
                }

                if (beam.EndPoint != null)
                {
                    // Round to 6 decimal places for tolerance
                    string key = $"{Math.Round(beam.EndPoint.X, 6)},{Math.Round(beam.EndPoint.Y, 6)}";
                    if (!uniqueCoordinates.ContainsKey(key))
                        uniqueCoordinates[key] = new List<string>();
                    uniqueCoordinates[key].Add($"Beam {beam.Id} EndPoint");
                }
            }

            report.AppendLine($"Total unique coordinates: {uniqueCoordinates.Count}");
            report.AppendLine();

            foreach (var entry in uniqueCoordinates)
            {
                if (entry.Value.Count > 1)
                {
                    report.AppendLine($"Coordinate {entry.Key} is used by {entry.Value.Count} points:");
                    foreach (var pointRef in entry.Value)
                        report.AppendLine($"  - {pointRef}");
                    report.AppendLine();
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// Calculate Euclidean distance between two 2D points
        /// </summary>
        private static double CalculateDistance(Point2D p1, Point2D p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        /// <summary>
        /// Generates a diagnostic E2K file for debugging
        /// </summary>
        public static string GenerateDiagnosticE2K(BaseModel model)
        {
            StringBuilder e2k = new StringBuilder();

            e2k.AppendLine("$ DIAGNOSTIC E2K FILE");
            e2k.AppendLine("$ =================");
            e2k.AppendLine();

            // Generate points section
            e2k.AppendLine("$ POINT COORDINATES");

            Dictionary<string, Point2D> uniquePoints = new Dictionary<string, Point2D>();
            Dictionary<string, int> pointIds = new Dictionary<string, int>();
            int pointCounter = 1;

            // Collect all unique points from beams
            foreach (var beam in model.Elements.Beams)
            {
                if (beam.StartPoint != null)
                {
                    string key = $"{beam.StartPoint.X:F15},{beam.StartPoint.Y:F15}";
                    if (!uniquePoints.ContainsKey(key))
                    {
                        uniquePoints[key] = beam.StartPoint;
                        pointIds[key] = pointCounter++;
                    }
                }

                if (beam.EndPoint != null)
                {
                    string key = $"{beam.EndPoint.X:F15},{beam.EndPoint.Y:F15}";
                    if (!uniquePoints.ContainsKey(key))
                    {
                        uniquePoints[key] = beam.EndPoint;
                        pointIds[key] = pointCounter++;
                    }
                }
            }

            // Output points
            foreach (var entry in uniquePoints)
            {
                e2k.AppendLine($"  POINT  \"{pointIds[entry.Key]}\"  {entry.Value.X:F6}  {entry.Value.Y:F6}");
            }

            e2k.AppendLine();
            e2k.AppendLine("$ LINE CONNECTIVITIES");

            // Generate lines for each beam
            for (int i = 0; i < model.Elements.Beams.Count; i++)
            {
                var beam = model.Elements.Beams[i];
                if (beam.StartPoint != null && beam.EndPoint != null)
                {
                    string startKey = $"{beam.StartPoint.X:F15},{beam.StartPoint.Y:F15}";
                    string endKey = $"{beam.EndPoint.X:F15},{beam.EndPoint.Y:F15}";

                    if (pointIds.ContainsKey(startKey) && pointIds.ContainsKey(endKey))
                    {
                        e2k.AppendLine($"  LINE  \"B{i + 1}\"  BEAM  \"{pointIds[startKey]}\"  \"{pointIds[endKey]}\"  0");
                    }
                }
            }

            return e2k.ToString();
        }
    }
}
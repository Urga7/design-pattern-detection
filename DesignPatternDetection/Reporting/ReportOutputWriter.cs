using DesignPatternDetection.Detection;
using DesignPatternDetection.Detection.InputResolution;

namespace DesignPatternDetection.Reporting;

/// <summary>
/// Writes whichever of the three match outputs - <c>--report</c> (JSON), <c>--sarif</c> and <c>--findings</c> (RDF) -
/// the command line asked for, and nothing at all when it asked for none. All three are built from one
/// <see cref="DetectionReport"/>.
/// </summary>
public static class ReportOutputWriter
{
    public static void Write(ScanResult result, CommandLineOptions options)
    {
        if (options.ReportPath is null && options.SarifPath is null && options.FindingsPath is null)
            return;

        var report = DetectionReport.From(result);

        if (options.ReportPath is not null)
        {
            report.Save(options.ReportPath);
            Console.WriteLine($"Report written to {options.ReportPath}.");
        }

        if (options.SarifPath is not null)
        {
            SarifReportWriter.Write(options.SarifPath, report);
            Console.WriteLine($"SARIF written to {options.SarifPath}.");
        }

        if (options.FindingsPath is not null)
        {
            RdfFindingsWriter.Write(options.FindingsPath, report);
            Console.WriteLine($"Findings written to {options.FindingsPath}.");
        }
    }
}

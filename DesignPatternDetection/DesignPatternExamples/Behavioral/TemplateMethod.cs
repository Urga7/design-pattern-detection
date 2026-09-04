using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Behavioral;

// AbstractClass: the concrete template method fixes the algorithm's skeleton
// and defers the individual steps to abstract primitive operations.
[UsedImplicitly]
public abstract class DataExporter
{
    public string Export() => $"{ReadData()} -> {FormatData()}";

    protected abstract string ReadData();

    protected abstract string FormatData();
}

// Concrete classes: each fills the steps in without touching the skeleton.
[UsedImplicitly]
public sealed class CsvExporter : DataExporter
{
    protected override string ReadData() => "rows";

    protected override string FormatData() => "csv";
}

[UsedImplicitly]
public sealed class JsonExporter : DataExporter
{
    protected override string ReadData() => "documents";

    protected override string FormatData() => "json";
}

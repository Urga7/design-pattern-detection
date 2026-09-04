using System.Reflection;
using DesignPatternDetection.Detection;

namespace DesignPatternDetection.Tests;

/// <summary>Reaches the protected members that describe a <see cref="SparqlPatternDetector"/>.</summary>
internal static class DetectorReflection
{
    /// <summary>Every concrete <see cref="SparqlPatternDetector"/> in the detector assembly.</summary>
    public static IEnumerable<SparqlPatternDetector> Detectors =>
        typeof(SparqlPatternDetector).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false } && typeof(SparqlPatternDetector).IsAssignableFrom(type))
            .Select(type => (SparqlPatternDetector)Activator.CreateInstance(type)!);

    public static string QueryOf(SparqlPatternDetector detector) => Value<string>(detector, "SparqlQuery");

    public static IReadOnlyDictionary<string, string> RolesOf(SparqlPatternDetector detector) =>
        Value<IReadOnlyDictionary<string, string>>(detector, "FdpRoles");

    public static string PatternOf(SparqlPatternDetector detector) => Value<string>(detector, "FdpPattern");

    private static T Value<T>(SparqlPatternDetector detector, string propertyName) =>
        (T)detector.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(detector)!;
}

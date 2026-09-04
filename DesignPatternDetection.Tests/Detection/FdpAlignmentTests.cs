using System.Text.RegularExpressions;
using DesignPatternDetection.Detection;
using DesignPatternDetection.Detection.Patterns.Behavioral;
using DesignPatternDetection.Detection.Patterns.Structural;

namespace DesignPatternDetection.Tests.Detection;

/// <summary>Guards the mapping from role variables onto FDP participants.</summary>
public class FdpAlignmentTests
{
    /// <summary>The variables the detector actually selects.</summary>
    private static List<string> SelectedVariables(SparqlPatternDetector detector)
    {
        var query = DetectorReflection.QueryOf(detector);
        var select = Regex.Match(query, @"SELECT\s+(?:DISTINCT\s+)?(.*?)\s+WHERE", RegexOptions.Singleline).Groups[1].Value;

        return Regex.Matches(select, @"\?([A-Za-z_][A-Za-z0-9_]*)").Select(match => match.Groups[1].Value).ToList();
    }

    [Fact]
    public void Every_aligned_role_is_a_variable_the_detector_actually_selects()
    {
        var offenders = DetectorReflection.Detectors
            .SelectMany(detector => DetectorReflection.RolesOf(detector).Keys
                .Where(role => !SelectedVariables(detector).Contains(role))
                .Select(role => $"{detector.PatternName} aligns '{role}', which it does not select"))
            .ToList();

        Assert.True(offenders.Count == 0, string.Join("; ", offenders));
    }

    /// <summary>
    /// FDP names participants &lt;Pattern&gt;&lt;Role&gt;, so a local name that does not open with this detector's
    /// own pattern was copied from a neighbor. Compared case-sensitively against <c>FdpPattern</c>, which makes this
    /// the check on the pattern individual's own spelling too.
    /// </summary>
    [Fact]
    public void Every_aligned_role_names_a_participant_of_this_detectors_own_pattern()
    {
        var offenders = DetectorReflection.Detectors
            .SelectMany(detector => DetectorReflection.RolesOf(detector)
                .Where(role => !role.Value.StartsWith(DetectorReflection.PatternOf(detector), StringComparison.Ordinal))
                .Select(role => $"{detector.PatternName}: '{role.Key}' -> fdp:{role.Value}"))
            .ToList();

        Assert.True(offenders.Count == 0, string.Join("; ", offenders));
    }

    /// <summary>Interpreter is the sole exemption because FDP models no participants for it.</summary>
    [Fact]
    public void Every_detector_except_interpreter_aligns_its_roles()
    {
        var unaligned = DetectorReflection.Detectors
            .Where(detector => DetectorReflection.RolesOf(detector).Count == 0)
            .Select(detector => detector.PatternName)
            .Order()
            .ToList();

        Assert.Equal(["Interpreter"], unaligned);
    }

    [Fact]
    public void A_match_carries_the_fdp_iri_for_each_aligned_role()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public interface INotifier { void Send(string message); }

        public class LegacyPager
        {
            public void Page(string text) { }
        }

        public class PagerAdapter : INotifier
        {
            private readonly LegacyPager _pager;

            public PagerAdapter(LegacyPager pager) { _pager = pager; }

            public void Send(string message) { _pager.Page(message); }
        }
        """);

        var match = Assert.Single(new AdapterPatternDetector().Detect(graph));
        var iris = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(match.RoleIris);

        Assert.Equal("https://dejanl.github.io/FDP/FDP.ttl#AdapterClientInterface", iris["target"]);
        Assert.Equal("https://dejanl.github.io/FDP/FDP.ttl#AdapterAdapter", iris["adapter"]);
        Assert.Equal("https://dejanl.github.io/FDP/FDP.ttl#AdapterService", iris["adaptee"]);
        Assert.Equal("https://dejanl.github.io/FDP/FDP.ttl#Adapter", match.PatternIri);
    }

    /// <summary>
    /// A pattern the ontology does not model must claim nothing about it - neither for its roles nor for the pattern
    /// itself, which the alignment would otherwise mint out of the detector's own name.
    /// </summary>
    [Fact]
    public void An_unmodelled_pattern_carries_no_fdp_iri_at_all()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Expression
        {
            public abstract int Interpret();
        }

        public sealed class Literal : Expression
        {
            private readonly int _value;
            public Literal(int value) { _value = value; }
            public override int Interpret() => _value;
        }

        public sealed class Sum : Expression
        {
            private readonly Expression _left;
            private readonly Expression _right;

            public Sum(Expression left, Expression right) { _left = left; _right = right; }

            public override int Interpret() => _left.Interpret() + _right.Interpret();
        }
        """);

        var match = Assert.Single(new InterpreterPatternDetector().Detect(graph));

        Assert.Null(match.PatternIri);
        Assert.Null(match.RoleIris);
    }
}

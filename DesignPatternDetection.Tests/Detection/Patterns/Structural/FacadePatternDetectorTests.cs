using DesignPatternDetection.Detection.Patterns.Structural;

namespace DesignPatternDetection.Tests.Detection.Patterns.Structural;

public class FacadePatternDetectorTests
{
    private readonly FacadePatternDetector _detector = new();

    [Fact]
    public void Detects_a_facade_fronting_multiple_subsystem_classes()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class Cpu { public string Execute() => "executing"; }

        public sealed class Memory { public string Load() => "loading"; }

        public sealed class ComputerFacade
        {
            private readonly Cpu _cpu = new();
            private readonly Memory _memory = new();
            public string Start() => $"{_memory.Load()}, {_cpu.Execute()}";
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("ComputerFacade", match.Bindings["facade"]);
        Assert.Equal("Cpu", match.Bindings["subsystemA"]);
        Assert.Equal("Memory", match.Bindings["subsystemB"]);
    }

    [Fact]
    public void Ignores_a_wrapper_that_conforms_to_an_existing_abstraction()
    {
        // Extending a Target while wrapping other classes is adapter-, proxy-
        // or decorator-shaped; a Facade offers a new interface of its own.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Notifier { public abstract string Send(string message); }

        public sealed class SmtpClient { public string Deliver(string body) => body; }

        public sealed class AuditLog { public void Record(string entry) { } }

        public sealed class EmailNotifier : Notifier
        {
            private readonly SmtpClient _smtp = new();
            private readonly AuditLog _log = new();
            public override string Send(string message) => _smtp.Deliver(message);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_class_wrapping_a_single_subsystem()
    {
        // One wrapped class is plain composition; a Facade coordinates a
        // subsystem of several.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class Cpu { public string Execute() => "executing"; }

        public sealed class CpuMonitor
        {
            private readonly Cpu _cpu = new();
            public string Sample() => _cpu.Execute();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_class_whose_fields_are_not_source_declared_classes()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class Settings
        {
            private readonly string _name = "app";
            private readonly int _timeout = 30;
            public string Describe() => $"{_name}:{_timeout}";
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

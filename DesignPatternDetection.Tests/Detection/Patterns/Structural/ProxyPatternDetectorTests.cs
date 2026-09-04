using DesignPatternDetection.Detection.Patterns.Structural;

namespace DesignPatternDetection.Tests.Detection.Patterns.Structural;

public class ProxyPatternDetectorTests
{
    private readonly ProxyPatternDetector _detector = new();

    [Fact]
    public void Detects_a_proxy_wrapping_a_concrete_real_subject()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Image { public abstract string Display(); }

        public sealed class RealImage : Image
        {
            public override string Display() => "image bytes";
        }

        public sealed class ImageProxy : Image
        {
            private RealImage? _real;

            public override string Display()
            {
                _real ??= new RealImage();
                return _real.Display();
            }
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("Image", match.Bindings["subject"]);
        Assert.Equal("RealImage", match.Bindings["realSubject"]);
        Assert.Equal("ImageProxy", match.Bindings["proxy"]);
    }

    [Fact]
    public void Ignores_a_decorator_wrapping_the_abstraction_itself()
    {
        // A Decorator's field is typed as the Subject abstraction so any
        // component can be wrapped; a Proxy commits to one concrete sibling.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Notifier { public abstract string Send(string message); }

        public sealed class LoggingNotifier : Notifier
        {
            private readonly Notifier _inner;
            public LoggingNotifier(Notifier inner) => _inner = inner;
            public override string Send(string message) => _inner.Send(message);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_an_adapter_wrapping_a_foreign_class()
    {
        // The wrapped type must belong to the Subject's own hierarchy; a
        // class from outside it is an Adaptee.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Notifier { public abstract string Send(string message); }

        public sealed class LegacyPager { public string Page(string text) => text; }

        public sealed class PagerAdapter : Notifier
        {
            private readonly LegacyPager _pager;
            public PagerAdapter(LegacyPager pager) => _pager = pager;
            public override string Send(string message) => _pager.Page(message);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_wrapper_that_never_overrides_the_operation()
    {
        // Holding a sibling without standing in for it is plain composition -
        // the proxy must intercept the Subject's operation.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Image { public abstract string Display(); }

        public sealed class RealImage : Image
        {
            public override string Display() => "image bytes";
        }

        public sealed class ImageStats : Image
        {
            private readonly RealImage _image;
            public ImageStats(RealImage image) => _image = image;
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}

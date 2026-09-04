using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Structural;

// Subject: the abstraction both the real object and its stand-in implement.
[UsedImplicitly]
public abstract class Image
{
    public abstract string Display();
}

// RealSubject: the expensive object the proxy shields.
[UsedImplicitly]
public sealed class RealImage : Image
{
    public override string Display() => "image bytes";
}

// Proxy: implements the same Subject and controls access to a wrapped RealSubject, creating it lazily on first use.
[UsedImplicitly]
public sealed class ImageProxy : Image
{
    private RealImage? _real;

    public override string Display()
    {
        _real ??= new RealImage();
        return _real.Display();
    }
}

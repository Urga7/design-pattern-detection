using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Creational;

// Abstract products: each is one member of the family the factory creates.
[UsedImplicitly]
public abstract class Button
{
    public abstract string Render();
}

[UsedImplicitly]
public abstract class Checkbox
{
    public abstract string Render();
}

// Concrete products for the Windows family.
[UsedImplicitly]
public sealed class WindowsButton : Button
{
    public override string Render() => "Rendering a Windows button.";
}

[UsedImplicitly]
public sealed class WindowsCheckbox : Checkbox
{
    public override string Render() => "Rendering a Windows checkbox.";
}

// Concrete products for the macOS family.
[UsedImplicitly]
public sealed class MacButton : Button
{
    public override string Render() => "Rendering a Mac button.";
}

[UsedImplicitly]
public sealed class MacCheckbox : Checkbox
{
    public override string Render() => "Rendering a Mac checkbox.";
}

// Abstract factory: one creation method per product in the family.
[UsedImplicitly]
public abstract class GuiFactory
{
    public abstract Button CreateButton();
    public abstract Checkbox CreateCheckbox();
}

// Concrete factories: each builds a whole family of matching products.
[UsedImplicitly]
public sealed class WindowsFactory : GuiFactory
{
    public override Button CreateButton() => new WindowsButton();
    public override Checkbox CreateCheckbox() => new WindowsCheckbox();
}

[UsedImplicitly]
public sealed class MacFactory : GuiFactory
{
    public override Button CreateButton() => new MacButton();
    public override Checkbox CreateCheckbox() => new MacCheckbox();
}

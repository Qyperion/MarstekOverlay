using System.Windows.Data;
using System.Windows.Markup;

namespace MarstekOverlay.Desktop.Localization;

/// <summary>Usage: <c>Content="{loc:Text Settings_Save}"</c>, where the argument is a key of <c>Strings.resx</c>.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TextExtension(string key) : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}

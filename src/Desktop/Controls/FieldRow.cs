using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace MarstekOverlay.Desktop.Controls;

/// <summary>A row of a settings page: the label, the field, and under the field what is wrong with it and a hint.
/// The field stays bound to the view model itself, so that its frame turns red from its own validation; a field
/// bound through this row would leave the error on the row. The labels of the rows within one
/// <see cref="Grid.IsSharedSizeScopeProperty"/> are one column wide.</summary>
/// <remarks>Built in code rather than templated: UI Automation leaves out the text of a template.</remarks>
[ContentProperty(nameof(Field))]
public sealed class FieldRow : Grid
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(FieldRow), new PropertyMetadata(null));

    public static readonly DependencyProperty ErrorProperty =
        DependencyProperty.Register(nameof(Error), typeof(string), typeof(FieldRow), new PropertyMetadata(null));

    public static readonly DependencyProperty HintProperty =
        DependencyProperty.Register(nameof(Hint), typeof(string), typeof(FieldRow), new PropertyMetadata(null));

    public static readonly DependencyProperty FieldProperty = DependencyProperty.Register(
        nameof(Field),
        typeof(UIElement),
        typeof(FieldRow),
        new PropertyMetadata(null, (row, change) => ((FieldRow)row).Place((UIElement?)change.OldValue, (UIElement?)change.NewValue)));

    public FieldRow()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "FieldLabel" });
        ColumnDefinitions.Add(new ColumnDefinition());
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddText(LabelProperty, "FieldLabel", row: 0, column: 0);
        AddText(ErrorProperty, "FieldError", row: 1, column: 1);
        AddText(HintProperty, "FieldHint", row: 2, column: 1);
    }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The message of the field; nothing while it is in order.</summary>
    public string? Error
    {
        get => (string?)GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public string? Hint
    {
        get => (string?)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <summary>The box, or the boxes, the row is for.</summary>
    public UIElement? Field
    {
        get => (UIElement?)GetValue(FieldProperty);
        set => SetValue(FieldProperty, value);
    }

    private void AddText(DependencyProperty source, string style, int row, int column)
    {
        var text = new TextBlock();
        text.SetBinding(TextBlock.TextProperty, new Binding(source.Name) { Source = this });
        text.SetResourceReference(StyleProperty, style);
        SetRow(text, row);
        SetColumn(text, column);
        Children.Add(text);
    }

    private void Place(UIElement? previous, UIElement? field)
    {
        if (previous is not null)
            Children.Remove(previous);

        if (field is null)
            return;

        // Right after the label, where UI Automation reads it.
        SetColumn(field, 1);
        Children.Insert(1, field);
    }
}

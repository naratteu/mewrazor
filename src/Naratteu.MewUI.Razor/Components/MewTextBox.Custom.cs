using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Microsoft.AspNetCore.Components;

namespace Naratteu.MewUI.Razor.Components;

/// <summary>
/// Adds two-way binding on top of the generated one-way <c>Text</c> parameter, so
/// <c>@bind-Value</c> works.
/// </summary>
public partial class MewTextBox
{
    // MewUI reports edits through a bound observable rather than a change event, so the control
    // keeps its native binding and the component forwards it to Blazor.
    private readonly ObservableValue<string> _text = new(string.Empty);

    private bool _writingParameter;

    [Parameter] public string? Value { get; set; }

    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    partial void OnControlCreated()
    {
        Control.SetBinding(TextBox.TextProperty, _text, BindingMode.TwoWay);
        _text.Subscribe(OnTextEdited);
    }

    partial void ApplyCustomParameters()
    {
        if (Value is null || _text.Value == Value) return;

        _writingParameter = true;
        try { _text.Value = Value; }
        finally { _writingParameter = false; }
    }

    private void OnTextEdited()
    {
        if (_writingParameter) return;

        Value = _text.Value;
        _ = ValueChanged.InvokeAsync(_text.Value);
    }
}

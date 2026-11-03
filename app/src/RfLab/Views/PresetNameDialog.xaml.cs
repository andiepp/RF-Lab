using System.Windows;
using RfLab.Core.Presets;

namespace RfLab.Views;

/// <summary>Asks for a stored setting's name and description.</summary>
public partial class PresetNameDialog : Window
{
    public PresetNameDialog(string title, string name, string description)
    {
        InitializeComponent();
        Title = title;
        NameBox.Text = name;
        DescriptionBox.Text = description;
        NameBox.SelectAll();
        OnTextChanged(this, null);
    }

    public string PresetName => NameBox.Text.Trim();

    public string PresetDescription => DescriptionBox.Text.Trim();

    private void OnTextChanged(object sender, RoutedEventArgs? eventArgs)
    {
        string? problem = RfLabPresetStore.ValidateName(PresetName);
        Problem.Text = NameBox.Text.Length == 0 ? string.Empty : problem ?? string.Empty;
        OkButton.IsEnabled = problem is null;
    }

    private void OnOk(object sender, RoutedEventArgs eventArgs) => DialogResult = true;
}

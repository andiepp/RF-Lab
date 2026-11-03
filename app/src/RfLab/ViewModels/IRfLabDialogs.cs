namespace RfLab.ViewModels;

/// <summary>The questions the view model asks the operator; the main window answers them.</summary>
public interface IRfLabDialogs
{
    /// <summary>Asks for a preset name and description; <c>null</c> when cancelled.</summary>
    (string Name, string Description)? AskPresetName(string title, string name, string description);

    bool Confirm(string message);

    void OpenFolder(string path);
}

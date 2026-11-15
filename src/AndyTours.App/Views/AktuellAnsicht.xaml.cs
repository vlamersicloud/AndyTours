using System.Windows.Controls;

namespace AndyTours.App.Views;

/// <summary>Zeitraum, Kennzahl und Kalender werden sichtbar gewählt; Diagrammklicks ändern keine Auswahl.</summary>
public partial class AktuellAnsicht : UserControl
{
    /// <summary>Initialisiert Datenbindungen und OxyPlot-Ansichten.</summary>
    public AktuellAnsicht()
    {
        InitializeComponent();
    }
}

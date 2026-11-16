namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    public event Action? OverlaysAngefordert;

    private void OverlaysOeffnen_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen) return;
        try { OverlaysAngefordert?.Invoke(); }
        catch (Exception)
        {
            if (!_geschlossen) _viewModel.StatusText = "Die Overlay-Verwaltung konnte nicht geöffnet werden.";
        }
    }
}

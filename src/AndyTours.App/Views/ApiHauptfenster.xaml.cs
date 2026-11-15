using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster : Window, IDisposable
{
    private readonly ApiUebersichtViewModel _viewModel;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _geladen;

    public string BackendAdresseAnzeige { get; }

    public ApiHauptfenster(ApiUebersichtViewModel viewModel, Uri backendAdresse)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BackendAdresseAnzeige = backendAdresse.GetLeftPart(UriPartial.Authority);
        DataContext = this;
        Loaded += FensterGeladen;
        Closed += FensterGeschlossen;
        DataContext = new ApiFensterBindingContext(viewModel, BackendAdresseAnzeige);
    }

    public void Dispose()
    {
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    private async void FensterGeladen(object sender, RoutedEventArgs e)
    {
        if (_geladen) return;
        _geladen = true;
        try { await _viewModel.AktualisierenAsync(_lifetime.Token); }
        catch (OperationCanceledException) { }
    }

    private void FensterGeschlossen(object? sender, EventArgs e)
    {
        _lifetime.Cancel();
        _viewModel.AktualisierenCommand.Execute(null);
        _viewModel.NaechsteSeiteCommand.Execute(null);
        _viewModel.VorherigeSeiteCommand.Execute(null);
        _lifetime.Dispose();
    }
}

public sealed record ApiFensterBindingContext(ApiUebersichtViewModel ViewModel, string BackendAdresseAnzeige)
{
    public DateTime? VonDatum { get => ViewModel.VonDatum; set => ViewModel.VonDatum = value; }
    public DateTime? BisDatum { get => ViewModel.BisDatum; set => ViewModel.BisDatum = value; }
    public object RasterOptionen => ViewModel.RasterOptionen;
    public object KennzahlOptionen => ViewModel.KennzahlOptionen;
    public object Raster { get => ViewModel.Raster; set => ViewModel.Raster = (Auswertungsraster)value; }
    public object Kennzahl { get => ViewModel.Kennzahl; set => ViewModel.Kennzahl = (ApiDiagrammkennzahl)value; }
    public object Diagramm => ViewModel.Diagramm;
    public object Touren => ViewModel.Touren;
    public object DistanzKmText => ViewModel.DistanzKmText;
    public object HoehenmeterText => ViewModel.HoehenmeterText;
    public object FahrzeitText => ViewModel.FahrzeitText;
    public object AnzahlText => ViewModel.AnzahlText;
    public object ZukunftsHinweis => ViewModel.ZukunftsHinweis;
    public object Laedt => ViewModel.Laedt;
    public object StatusText => ViewModel.StatusText;
    public object SeitenText => ViewModel.SeitenText;
    public System.Windows.Input.ICommand AktualisierenCommand => ViewModel.AktualisierenCommand;
    public System.Windows.Input.ICommand NaechsteSeiteCommand => ViewModel.NaechsteSeiteCommand;
    public System.Windows.Input.ICommand VorherigeSeiteCommand => ViewModel.VorherigeSeiteCommand;
}

public sealed class DauerConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long sekunden) return "–";
        var stunden = sekunden / 3600;
        var minuten = sekunden % 3600 / 60;
        return $"{stunden.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))}:{minuten:00} h";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}
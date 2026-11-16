using System.ComponentModel;
using System.Windows;
using AndyTours.App.ViewModels;
namespace AndyTours.App.Views;
public partial class ApiGarminEinstellungenFenster : Window, IDisposable
{
    private readonly ApiGarminEinstellungenViewModel _viewModel;
    private bool _disposed;
    public ApiGarminEinstellungenFenster(ApiGarminEinstellungenViewModel viewModel)
    {
        InitializeComponent();
        _viewModel=viewModel;
        DataContext=viewModel;
        Loaded+=LoadedWindow;
        Closing+=ClosingWindow;
        Closed+=ClosedWindow;
        viewModel.SchluesselEingabeLeerenAngefordert+=ClearPassword;
    }
    private async void LoadedWindow(object sender,RoutedEventArgs e)
    {
        try { await _viewModel.LadenAsync(); } catch(OperationCanceledException) { }
    }
    private void SchluesselFeld_PasswordChanged(object sender,RoutedEventArgs e)=>_viewModel.SchluesselEntwurfGeaendert(SchluesselFeld.Password);
    private void ClearPassword(object? sender,EventArgs e)=>SchluesselFeld.Clear();
    private void ClosingWindow(object? sender,CancelEventArgs e)
    {
        if(_viewModel.Aendert){e.Cancel=true;return;}
        Cleanup();
    }
    private void ClosedWindow(object? sender,EventArgs e)=>Cleanup();
    public void Dispose(){Cleanup();GC.SuppressFinalize(this);}
    private void Cleanup()
    {
        if(_disposed)return;
        _disposed=true;
        _viewModel.SchluesselEingabeLeerenAngefordert-=ClearPassword;
        _viewModel.BeginClose();
        SchluesselFeld.Clear();
        _viewModel.Dispose();
    }
}

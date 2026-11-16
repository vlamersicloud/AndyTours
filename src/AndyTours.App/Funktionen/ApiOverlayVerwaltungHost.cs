using System.Windows;
using AndyTours.Application.Overlays;
using AndyTours.App.ViewModels;
using AndyTours.App.Views;

namespace AndyTours.App.Funktionen;

/// <summary>Owns the API overlay context and its single modeless manager for one API root window.</summary>
public sealed class ApiOverlayVerwaltungHost : IDisposable
{
    private readonly Window _owner;
    private readonly IOverlayVerwaltung _verwaltung;
    private readonly IOverlayGpxImport _gpxImport;
    private ApiOverlaysFenster? _fenster;
    private bool _disposed;

    public ApiOverlayVerwaltungHost(Window owner, IOverlayLeser leser, IOverlayVerwaltung verwaltung, IOverlayGpxImport gpxImport)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        ArgumentNullException.ThrowIfNull(leser);
        _verwaltung = verwaltung ?? throw new ArgumentNullException(nameof(verwaltung));
        _gpxImport = gpxImport ?? throw new ArgumentNullException(nameof(gpxImport));
        Kontext = new ApiOverlayKontext(leser, Oeffnen, SynchronizationContext.Current);
        _owner.Closing += OwnerClosing;
        _owner.Closed += OwnerClosed;
    }

    public ApiOverlayKontext Kontext { get; }

    public void Oeffnen()
    {
        if (_disposed || !_owner.IsLoaded || !_owner.IsVisible || _owner.Dispatcher.HasShutdownStarted) return;
        if (_fenster is { IsVisible: true } existing)
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        ApiOverlaysViewModel? vm = null;
        ApiOverlaysFenster? window = null;
        try
        {
            vm = new ApiOverlaysViewModel(Kontext, _verwaltung, _gpxImport, prompt =>
                MessageBox.Show(_owner, prompt, "Overlays", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);
            window = new ApiOverlaysFenster(vm) { Owner = _owner };
            _fenster = window;
            window.Closed += ManagerClosed;
            window.Show();
            window.Activate();
        }
        catch (Exception)
        {
            if (window is not null)
            {
                window.Closed -= ManagerClosed;
                if (window.IsVisible) window.SchliesseFuerHostShutdown();
                else vm?.Dispose();
                _fenster = null;
            }
            else vm?.Dispose();
            if (_owner.IsVisible) MessageBox.Show(_owner, "Overlay-Verwaltung konnte nicht geöffnet werden.", "Overlays", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ManagerClosed(object? sender, EventArgs e)
    {
        if (sender is ApiOverlaysFenster window) window.Closed -= ManagerClosed;
        if (ReferenceEquals(_fenster, sender)) _fenster = null;
    }

    private void OwnerClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (e.Cancel || _disposed || _fenster is not { IsVisible: true } manager) return;
        manager.Close();
        if (manager.IsVisible) e.Cancel = true;
    }

    private void OwnerClosed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _owner.Closing -= OwnerClosing;
        _owner.Closed -= OwnerClosed;
        if (_fenster is { IsVisible: true } manager)
        {
            manager.SchliesseFuerHostShutdown();
            manager.Closed -= ManagerClosed;
            _fenster = null;
        }
        Kontext.Dispose();
    }
}

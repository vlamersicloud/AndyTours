using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AndyTours.App.Funktionen;

/// <summary>
/// Gemeinsame Bedienung der Tabellen und Listen in den Ansichten (Trainingseinheiten, Wartungen, Alarme, Overlays):
/// Markieren, Doppelklick, Entf und Rechtsklick.
/// </summary>
public static class Tabellenbedienung
{
    /// <summary>
    /// Sucht vom angeklickten Element aus aufwärts das nächste Element des Typs <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>Textteile (z. B. Run) gehören nicht zum sichtbaren Baum; dort geht es über den logischen Baum weiter.</remarks>
    /// <typeparam name="T">Gesuchter Typ, z. B. <see cref="DataGridRow"/>.</typeparam>
    /// <param name="quelle">Das angeklickte Element (<c>e.OriginalSource</c>).</param>
    /// <returns>Das gefundene Element oder <c>null</c>.</returns>
    public static T? SucheVorfahr<T>(object? quelle) where T : DependencyObject
    {
        var aktuell = quelle as DependencyObject;
        while (aktuell is not null and not T)
        {
            aktuell = aktuell is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(aktuell)
                : LogicalTreeHelper.GetParent(aktuell);
        }

        return aktuell as T;
    }

    /// <summary>
    /// Rechtsklick auf eine nicht markierte Zeile markiert diese, damit sich das Rechtsklickmenü auf sie bezieht;
    /// bei einer schon markierten Zeile bleibt eine Mehrfachmarkierung erhalten.
    /// </summary>
    /// <param name="tabelle">Die Tabelle.</param>
    /// <param name="e">Das Ereignis des Rechtsklicks.</param>
    public static void MarkiereBeiRechtsklick(DataGrid tabelle, MouseButtonEventArgs e)
    {
        if (SucheVorfahr<DataGridRow>(e.OriginalSource) is DataGridRow zeile && !zeile.IsSelected)
        {
            tabelle.SelectedItem = zeile.Item;
        }
    }

    /// <summary>
    /// Markiert die angegebenen Zeilen und holt die oberste (in der aktuellen Sortierung) in den sichtbaren Bereich.
    /// </summary>
    /// <param name="tabelle">Die Tabelle.</param>
    /// <param name="zeilen">Die zu markierenden Zeilen.</param>
    public static void MarkiereZeilen(DataGrid tabelle, IReadOnlyCollection<object> zeilen)
    {
        tabelle.SelectedItems.Clear();
        foreach (object zeile in zeilen)
        {
            tabelle.SelectedItems.Add(zeile);
        }

        if (tabelle.Items.Cast<object>().FirstOrDefault(zeilen.Contains) is object erste)
        {
            tabelle.ScrollIntoView(erste);
        }
    }

    /// <summary>Führt bei einem Doppelklick mit der linken Maustaste einen Befehl aus, sofern er wählbar ist.</summary>
    /// <param name="e">Das Ereignis des Doppelklicks.</param>
    /// <param name="befehl">Der Befehl, z. B. „Bearbeiten“.</param>
    public static void BeiDoppelklick(MouseButtonEventArgs e, ICommand? befehl)
    {
        if (befehl is not null && e.ChangedButton == MouseButton.Left)
        {
            e.Handled = true;
            FuehreAus(befehl);
        }
    }

    /// <summary>Führt bei der Entf-Taste einen Befehl aus, sofern er wählbar ist.</summary>
    /// <param name="e">Das Ereignis des Tastendrucks.</param>
    /// <param name="befehl">Der Befehl, z. B. „Löschen“.</param>
    public static void BeiEntf(KeyEventArgs e, ICommand? befehl)
    {
        if (befehl is not null && e.Key == Key.Delete)
        {
            e.Handled = true;
            FuehreAus(befehl);
        }
    }

    /// <summary>Führt einen Befehl aus, wenn er gerade wählbar ist.</summary>
    private static void FuehreAus(ICommand befehl)
    {
        if (befehl.CanExecute(null))
        {
            befehl.Execute(null);
        }
    }
}

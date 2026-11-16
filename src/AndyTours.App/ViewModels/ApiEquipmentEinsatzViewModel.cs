using System.Collections.ObjectModel;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndyTours.App.ViewModels;

/// <summary>Reine Darstellung der serverseitigen Equipment-Einsatzzeilen.</summary>
public partial class ApiEquipmentEinsatzViewModel : ObservableObject
{
    private ReadOnlyCollection<Einsatzzeile> _zeilen;

    public ApiEquipmentEinsatzViewModel(EquipmentEinsatzAntwort antwort)
    {
        ArgumentNullException.ThrowIfNull(antwort);
        if (antwort.Zeilen is null) throw Invalid();
        var source = antwort.Zeilen;
        int summaries = 0;
        for (int i = 0; i < source.Count; i++)
        {
            var row = source[i];
            if (row is null || row.Name is null ||
                (row.VorherKm is double before && !double.IsFinite(before)) ||
                (row.GesamtKm is double total && !double.IsFinite(total))) throw Invalid();
            if (row.IstSumme)
            {
                summaries++;
                if (i != source.Count - 1) throw Invalid();
            }
        }
        if (source.Count == 0 ? summaries != 0 : summaries != 1) throw Invalid();
        _zeilen = Array.AsReadOnly(source.Select(row => new Einsatzzeile(row.Name, row.VorherKm, row.GesamtKm, row.GesamtHm, row.IstSumme)).ToArray());
        HatZeilen = _zeilen.Count > 0;
    }

    public IReadOnlyList<Einsatzzeile> Zeilen => _zeilen;
    public bool HatZeilen { get; }

    public void Sortiere(Einsatzspalte spalte, bool aufsteigend)
    {
        if (!Enum.IsDefined(spalte)) throw new ArgumentOutOfRangeException(nameof(spalte));
        _zeilen = Array.AsReadOnly(SortiereEinsatzzeilen.Ausfuehren(_zeilen, spalte, aufsteigend).ToArray());
        OnPropertyChanged(nameof(Zeilen));
    }

    private static ArgumentException Invalid() => new("Equipment-Einsatzdaten sind ungültig.");
}

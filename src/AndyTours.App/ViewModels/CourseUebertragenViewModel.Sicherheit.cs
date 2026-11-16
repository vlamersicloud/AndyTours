using AndyTours.Core.Modell;
using AndyTours.Import.Garmin;

namespace AndyTours.App.ViewModels;

public partial class CourseUebertragenViewModel
{
    private Courseziel? _transferZiel;
    private Fahrprofil? _transferProfil;

    private Courseziel TransferZiel => _transferZiel ?? Ziel;
    private Fahrprofil TransferProfil => _transferProfil ?? Profil;

    private void ErfasseTransferauswahl()
    {
        _transferZiel = Ziel;
        _transferProfil = Profil;
    }

    private void VerwerfeTransferauswahl()
    {
        _transferZiel = null;
        _transferProfil = null;
    }
}

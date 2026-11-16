using System;
using System.Collections.Generic;

using AndyTours.Core.Modell;
namespace AndyTours.App.ViewModels;

public partial class TourdetailViewModel
{
    public bool IstApiModus { get; private set; }

    public TourdetailViewModel(
        IReadOnlyList<Etappe> etappen,
        Func<string?> ladeGpxOrdner,
        Action<string> speichereGpxOrdner,
        Func<IReadOnlyList<Coursezeile>, Task> apiCourseUebertragung)
        : this(etappen, ladeGpxOrdner, speichereGpxOrdner,
            () => throw new InvalidOperationException("Profil liegt im Backend."),
            () => throw new InvalidOperationException("Routing liegt im Backend."))
    {
        IstApiModus = true;
        Streckenkarte.ApiCourseUebertragung =
            apiCourseUebertragung ?? throw new ArgumentNullException(nameof(apiCourseUebertragung));
    }
}
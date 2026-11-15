namespace AndyTours.Application.Touren;
/// <summary>Basistyp fachlicher Tourfehler.</summary>
public abstract class TourException(string message):Exception(message);
/// <summary>Die Tour existiert nicht.</summary>
public sealed class TourNichtGefundenException(long id):TourException($"Tour {id} wurde nicht gefunden.");
/// <summary>Die gelesene Version ist nicht mehr aktuell.</summary>
public sealed class TourVersionskonfliktException():TourException("Die Tour wurde zwischenzeitlich geändert.");
/// <summary>Die Tourwerte sind fachlich ungültig.</summary>
public sealed class UngueltigeTourException(string message):TourException(message);

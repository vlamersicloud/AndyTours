using AndyTours.Core.Modell;

namespace AndyTours.App.Funktionen;

public sealed record Overlayfarbton(string Name, byte R, byte G, byte B)
{
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>Pure color palette shared by legacy rendering and API presentation.</summary>
public static class Overlayfarben
{
    public static Overlayfarbton Wert(Overlayfarbe farbe) => farbe switch
    {
        Overlayfarbe.Violett => new("Violett", 0x9B, 0x30, 0xD9),
        Overlayfarbe.Blau => new("Blau", 0x1F, 0x4E, 0xD8),
        Overlayfarbe.Hellblau => new("Hellblau", 0x3B, 0xA7, 0xF0),
        Overlayfarbe.Tuerkis => new("Türkis", 0x00, 0xA8, 0xA8),
        Overlayfarbe.Gruen => new("Grün", 0x2E, 0x9E, 0x3A),
        Overlayfarbe.Gelb => new("Gelb", 0xE8, 0xC2, 0x00),
        Overlayfarbe.Orange => new("Orange", 0xF0, 0x7A, 0x10),
        Overlayfarbe.Rot => new("Rot", 0xD6, 0x22, 0x22),
        Overlayfarbe.Pink => new("Pink", 0xE8, 0x3E, 0x9C),
        Overlayfarbe.Braun => new("Braun", 0x8B, 0x5A, 0x2B),
        Overlayfarbe.Grau => new("Grau", 0x80, 0x80, 0x80),
        Overlayfarbe.Schwarz => new("Schwarz", 0x20, 0x20, 0x20),
        _ => throw new ArgumentOutOfRangeException(nameof(farbe))
    };
}

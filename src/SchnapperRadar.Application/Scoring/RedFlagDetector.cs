using System.Text.RegularExpressions;
using SchnapperRadar.Domain.Entities;
using SchnapperRadar.Domain.Enums;
using SchnapperRadar.Domain.ValueObjects;

namespace SchnapperRadar.Application.Scoring;

/// <summary>
/// Regelbasierte Betrugs- und Qualitätssignale. Läuft lokal, kostet nichts
/// und entscheidet, wer überhaupt in die teureren Stufen kommt.
/// </summary>
public static partial class RedFlagDetector
{
    private sealed record TextRule(string Code, Regex Pattern, string Message, RedFlagSeverity Severity);

    private static readonly TextRule[] TextRules =
    [
        new(RedFlagCodes.Counterfeit, CounterfeitRegex(), "Hinweis auf Fälschung/Replika", RedFlagSeverity.Hard),
        new(RedFlagCodes.RiskyPayment, HardPaymentRegex(), "Unsichere Zahlungsart (Western Union, Gutscheinkarten, ...)", RedFlagSeverity.Hard),
        new(RedFlagCodes.RiskyPayment, SoftPaymentRegex(), "Nur Vorkasse/Überweisung bzw. PayPal Freunde", RedFlagSeverity.Major),
        new(RedFlagCodes.OffPlatformContact, OffPlatformRegex(), "Will Kontakt außerhalb der Plattform (WhatsApp, Mail, Telefon)", RedFlagSeverity.Major),
        new(RedFlagCodes.AbroadStory, AbroadRegex(), "Typische Ausreden-Story (Ausland, Montage, Spedition)", RedFlagSeverity.Major),
        new(RedFlagCodes.Defective, DefectiveRegex(), "Defekt / Bastlerware", RedFlagSeverity.Minor),
        new(RedFlagCodes.TradeOnly, TradeRegex(), "Evtl. nur Tausch", RedFlagSeverity.Minor),
    ];

    /// <summary>Textsignale aus Titel + Beschreibung.</summary>
    public static List<RedFlag> DetectText(Listing listing, string searchQuery)
    {
        var flags = new List<RedFlag>();
        var text = $"{listing.Title}\n{listing.Description}".ToLowerInvariant();

        foreach (var rule in TextRules)
        {
            if (rule.Pattern.IsMatch(text) && flags.TrueForAll(f => f.Code != rule.Code))
                flags.Add(new RedFlag(rule.Code, rule.Message, rule.Severity));
        }

        if (WantedRegex().IsMatch(listing.Title.ToLowerInvariant()))
            flags.Add(new RedFlag(RedFlagCodes.WantedAd, "Gesuch, kein Angebot", RedFlagSeverity.Hard));

        if (IsAccessory(listing.Title, searchQuery))
            flags.Add(new RedFlag(RedFlagCodes.Accessory, "Wahrscheinlich nur Zubehör/Verpackung", RedFlagSeverity.Minor));

        return flags;
    }

    /// <summary>Strukturelle Signale (Preis, Fotos, Verkäufer). Nach dem Anreichern erneut aufrufen.</summary>
    public static List<RedFlag> DetectStructural(Listing listing, decimal? marketReference)
    {
        var flags = new List<RedFlag>();
        var price = listing.Price ?? 0;

        if (listing.ImageCount == 0)
            flags.Add(new RedFlag(RedFlagCodes.NoPhotos, "Keine Fotos", RedFlagSeverity.Major));
        else if (listing.IsDetailLoaded && listing.ImageCount == 1 && price > 200)
            flags.Add(new RedFlag(RedFlagCodes.FewPhotos, "Nur ein Foto bei hohem Preis", RedFlagSeverity.Minor));

        if (marketReference is >= 20 && price > 0)
        {
            var ratio = (double)(price / marketReference.Value);
            if (ratio < 0.35)
            {
                var severity = listing.Seller.IsLoaded && TrustScorer.Score(listing.Seller, listing.Platform) >= 80
                    ? RedFlagSeverity.Minor
                    : RedFlagSeverity.Major;
                flags.Add(new RedFlag(RedFlagCodes.SuspiciouslyCheap,
                    $"Preis {Math.Round((1 - ratio) * 100)} % unter Marktwert – zu gut um wahr zu sein?", severity));
            }
        }

        if (!listing.Seller.IsLoaded) return flags;

        var accountAgeDays = listing.Seller.MemberSince is { } since
            ? DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - since.DayNumber
            : (int?)null;
        if (accountAgeDays is < 30 && price > 150)
            flags.Add(new RedFlag(RedFlagCodes.NewAccountHighValue, "Brandneues Konto + teurer Artikel", RedFlagSeverity.Major));

        if (listing.Seller.RatingCount is 0 && price > 100)
            flags.Add(new RedFlag(RedFlagCodes.NoRatings, "Verkäufer hat noch keine Bewertungen", RedFlagSeverity.Minor));

        if (listing.Seller.RatingPercent is < 80 && listing.Seller.RatingCount is >= 5)
            flags.Add(new RedFlag(RedFlagCodes.BadRatings, "Schlechte Verkäuferbewertungen", RedFlagSeverity.Major));
        if (listing.Seller.Badges.Exists(b => b.Contains("Na ja", StringComparison.OrdinalIgnoreCase)))
            flags.Add(new RedFlag(RedFlagCodes.BadRatings, "Zufriedenheit nur \"Na ja\"", RedFlagSeverity.Major));

        if (listing.IsDetailLoaded && listing.Description.Trim().Length < 15)
            flags.Add(new RedFlag(RedFlagCodes.ShortDescription, "Kaum Beschreibung", RedFlagSeverity.Minor));

        return flags;
    }

    public static bool IsAccessory(string title, string searchQuery)
    {
        var t = title.ToLowerInvariant();
        var q = searchQuery.ToLowerInvariant();
        var match = AccessoryRegex().Match(t);
        return match.Success && !q.Contains(match.Value.Trim());
    }

    [GeneratedRegex(@"\b(replika|replica|fake|nachbau|dupe|kopie|imitat|inspired by|1\s?:\s?1|aaa\+?\s*qualit)", RegexOptions.CultureInvariant)]
    private static partial Regex CounterfeitRegex();

    [GeneratedRegex(@"western\s*union|moneygram|paysafe|gutscheinkarte|(amazon|itunes|apple|steam|google\s*play)[\s-]*(gutschein|karte|guthaben)|krypto|bitcoin", RegexOptions.CultureInvariant)]
    private static partial Regex HardPaymentRegex();

    [GeneratedRegex(@"freunde\s*(und|&|u\.?)\s*familie|familie\s*(und|&|u\.?)\s*freunde|\bf\s?&\s?f\b|nur\s+(per\s+)?(überweisung|vorkasse)|kein\s+paypal", RegexOptions.CultureInvariant)]
    private static partial Regex SoftPaymentRegex();

    [GeneratedRegex(@"whats\s*app|whatsapp|telegram|signal\s*messenger|[\w.+-]+@[\w-]+\.[a-z]{2,}|(\+49|\b0)\s?1[5-7]\d[\s/-]?\d{6,8}|schreib(t)?\s+(mir\s+)?(eine\s+)?(e-?)?mail", RegexOptions.CultureInvariant)]
    private static partial Regex OffPlatformRegex();

    [GeneratedRegex(@"bin\s+(gerade\s+|zur\s?zeit\s+|derzeit\s+)?(im\s+ausland|auf\s+montage|beruflich\s+unterwegs)|lebe\s+(jetzt\s+)?im\s+ausland|per\s+spedition|versand\s+nur\s+aus\s+dem\s+ausland", RegexOptions.CultureInvariant)]
    private static partial Regex AbroadRegex();

    [GeneratedRegex(@"\b(defekt|bastler|für\s+bastler|ohne\s+funktion|funktioniert\s+nicht|ersatzteil(e|spender)?|display\s+(gebrochen|kaputt)|wasserschaden)\b", RegexOptions.CultureInvariant)]
    private static partial Regex DefectiveRegex();

    [GeneratedRegex(@"\b(nur\s+tausch|tausche\s+gegen|tausch\s+gegen)\b", RegexOptions.CultureInvariant)]
    private static partial Regex TradeRegex();

    [GeneratedRegex(@"^\s*(suche|gesucht|ankauf|kaufe)\b", RegexOptions.CultureInvariant)]
    private static partial Regex WantedRegex();

    [GeneratedRegex(@"\b(hülle|huelle|case|cover|schutzhülle|panzerglas|schutzglas|schutzfolie|displayschutz|ladekabel|netzteil|ladegerät|nur\s+(ovp|karton|verpackung)|leere?\s+(ovp|verpackung|karton)|originalkarton|skin|aufkleber|halterung|ersatzbox)\b", RegexOptions.CultureInvariant)]
    private static partial Regex AccessoryRegex();
}

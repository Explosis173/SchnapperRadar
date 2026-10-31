namespace SchnapperRadar.Infrastructure.Http;

public sealed class ScraperOptions
{
    public const string Section = "Scraper";

    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";

    public bool KleinanzeigenEnabled { get; set; } = true;
    public int KleinanzeigenMinDelayMs { get; set; } = 1500;

    public bool VintedEnabled { get; set; } = true;
    public string VintedHost { get; set; } = "www.vinted.de";
    public int VintedMinDelayMs { get; set; } = 1000;
}

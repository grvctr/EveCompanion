using System.Collections.ObjectModel;

namespace EveCompanion;

public sealed class AppState
{
    public ObservableCollection<IskSession> Sessions { get; set; } = [];
    public CompanionSettings Settings { get; set; } = new();
}

public sealed class CompanionSettings
{
    public string ClientId { get; set; } = "";
    public string CallbackUrl { get; set; } = "http://localhost:53682/callback/";
    public string UserAgent { get; set; } = "EVECompanionMvp/0.1 (add-contact-email)";
}

public sealed class IskSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? EndedAt { get; set; }
    public ObservableCollection<IncomeEntry> Entries { get; set; } = [];

    public bool IsActive => EndedAt is null;
    public decimal TotalIsk => Entries.Sum(entry => entry.Amount);
    public TimeSpan Duration => (EndedAt ?? DateTimeOffset.Now) - StartedAt;
    public decimal IskPerHour => IskMath.HourlyRate(TotalIsk, Duration);
    public string StartedText => StartedAt.LocalDateTime.ToString("g");
    public string EndedText => EndedAt?.LocalDateTime.ToString("g") ?? "активна";
    public string DurationText => Duration.ToString(Duration.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");
    public string TotalText => $"{TotalIsk:N0} ISK";
    public string RateText => $"{IskPerHour:N0} ISK/ч";
}

public sealed class IncomeEntry
{
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
    public decimal Amount { get; set; }
    public string Note { get; set; } = "";
}

public static class IskMath
{
    public static decimal HourlyRate(decimal totalIsk, TimeSpan duration) =>
        duration.TotalSeconds <= 0 ? 0 : totalIsk / (decimal)duration.TotalHours;

    public static bool TryParseAmount(string text, out decimal amount)
    {
        var compact = text.Replace(" ", "").Replace("_", "");
        return decimal.TryParse(compact, System.Globalization.NumberStyles.Number,
                   System.Globalization.CultureInfo.CurrentCulture, out amount)
               || decimal.TryParse(compact.Replace(',', '.'), System.Globalization.NumberStyles.Number,
                   System.Globalization.CultureInfo.InvariantCulture, out amount);
    }
}

public sealed record MarketSnapshot(decimal? BestBuy, decimal? BestSell, DateTimeOffset FetchedAt)
{
    public string Summary =>
        $"Buy: {(BestBuy is null ? "—" : $"{BestBuy:N2}")}  ·  Sell: {(BestSell is null ? "—" : $"{BestSell:N2}")} ISK";
}

public sealed record AnomalyDefinition(
    string Name,
    string Kind,
    string Space,
    string Guidance,
    string[] SuitableShips)
{
    public string Assessment { get; set; } = "Выберите корабль";
}

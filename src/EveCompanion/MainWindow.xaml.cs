using System.Text;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EveCompanion;

public partial class MainWindow : Window
{
    private static readonly string[] KnownShips =
    [
        "Heron", "Merlin", "Catalyst", "Cormorant", "Venture", "Badger", "Retriever"
    ];

    private readonly ObservableCollection<AnomalyDefinition> _anomalies =
    [
        new("Hideaway / Burrow", "Combat", "High-sec", "Базовый ориентир. Проверь урон, танк и триггеры до входа.", ["Merlin", "Catalyst", "Cormorant"]),
        new("Refuge", "Combat", "High-sec", "Для эсминца только после симуляции фита; при росте входящего урона — уходи.", ["Catalyst", "Cormorant"]),
        new("Den и выше", "Combat", "High-sec", "Текущий список кораблей не считаем достаточным без точного фита.", []),
        new("Data site", "Exploration", "High / Low / Null", "Heron подходит по роли; вне high-sec риск PvP остаётся всегда.", ["Heron"]),
        new("Relic site", "Exploration", "High / Low / Null", "Heron подходит по роли; проверь probes, analyzer и путь отхода.", ["Heron"]),
        new("Ore anomaly", "Mining", "Зависит от системы", "Venture — дешёвый старт; Retriever — только после покупки и проверки маршрута.", ["Venture", "Retriever"]),
        new("Gas site", "Harvesting", "Wormhole / Low / Null", "Venture возможен с нужными навыками, но сайт и пространство опасны.", ["Venture"]),
        new("Wormhole combat site", "Combat", "Wormhole", "Не рекомендовано для текущих известных корпусов и без разведки.", [])
    ];

    private readonly AppState _state;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private OverlayWindow? _overlay;

    public MainWindow()
    {
        InitializeComponent();
        _state = StateStore.Load(out var warning);

        SessionsGrid.ItemsSource = _state.Sessions;
        AnomaliesGrid.ItemsSource = _anomalies;
        ShipComboBox.ItemsSource = KnownShips;
        ClientIdTextBox.Text = _state.Settings.ClientId;
        CallbackUrlTextBox.Text = _state.Settings.CallbackUrl;
        UserAgentTextBox.Text = _state.Settings.UserAgent;
        DataPathText.Text = StateStore.FilePath;
        GlobalStatusText.Text = warning ?? "Данные загружены локально";

        _timer.Tick += (_, _) => RefreshSummary();
        _timer.Start();
        RefreshSummary();
    }

    private IskSession? ActiveSession => _state.Sessions.FirstOrDefault(session => session.IsActive);

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out var index))
            MainTabs.SelectedIndex = index;
    }

    private void StartSession_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is not null)
        {
            TrackerMessageText.Text = "Сессия уже запущена.";
            return;
        }

        _state.Sessions.Insert(0, new IskSession());
        TrackerMessageText.Text = "Новая сессия запущена.";
        SaveState("Сессия запущена");
        RefreshSummary();
    }

    private void StopSession_Click(object sender, RoutedEventArgs e)
    {
        var session = ActiveSession;
        if (session is null)
        {
            TrackerMessageText.Text = "Нет активной сессии.";
            return;
        }

        session.EndedAt = DateTimeOffset.Now;
        TrackerMessageText.Text = $"Сессия завершена: {session.TotalText}.";
        SaveState("Сессия завершена");
        RefreshSummary();
    }

    private void AddIncome_Click(object sender, RoutedEventArgs e)
    {
        var session = ActiveSession;
        if (session is null)
        {
            TrackerMessageText.Text = "Сначала начните сессию.";
            return;
        }

        if (!IskMath.TryParseAmount(IncomeAmountTextBox.Text, out var amount) || amount == 0)
        {
            TrackerMessageText.Text = "Введите ненулевую сумму, например 1250000 или -50000.";
            return;
        }

        session.Entries.Add(new IncomeEntry { Amount = amount, Note = IncomeNoteTextBox.Text.Trim() });
        IncomeAmountTextBox.Clear();
        IncomeNoteTextBox.Clear();
        TrackerMessageText.Text = $"Добавлено {amount:+#,0;-#,0} ISK.";
        SaveState("Запись ISK сохранена");
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        var session = ActiveSession;
        DashboardStateText.Text = session is null ? "не запущена" : "активна";
        DashboardTotalText.Text = session?.TotalText ?? "0 ISK";
        DashboardRateText.Text = session?.RateText ?? "0 ISK/ч";
        DashboardDurationText.Text = $"Длительность: {session?.DurationText ?? "—"}";
        SessionsGrid.Items.Refresh();
        _overlay?.Update(session);
    }

    private void ShipComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var ship = ShipComboBox.SelectedItem as string;
        foreach (var anomaly in _anomalies)
        {
            anomaly.Assessment = ship is null
                ? "Выберите корабль"
                : anomaly.SuitableShips.Contains(ship, StringComparer.OrdinalIgnoreCase)
                    ? "Подходит по роли"
                    : "Не рекомендовано";
        }
        AnomaliesGrid.Items.Refresh();
    }

    private async void MarketSearch_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RegionIdTextBox.Text, out var regionId) ||
            !int.TryParse(TypeIdTextBox.Text, out var typeId))
        {
            MarketResultText.Text = "Region ID и Type ID должны быть целыми числами.";
            return;
        }

        MarketSearchButton.IsEnabled = false;
        MarketResultText.Text = "Запрос ESI…";
        try
        {
            var snapshot = await EsiApi.GetMarketSnapshotAsync(regionId, typeId, _state.Settings.UserAgent);
            MarketResultText.Text = $"{snapshot.Summary}  ·  проверено {snapshot.FetchedAt.LocalDateTime:g}. Это лучшие публичные ордера региона, не гарантия сделки.";
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or ArgumentOutOfRangeException)
        {
            MarketResultText.Text = $"ESI недоступен: {error.Message}";
        }
        finally
        {
            MarketSearchButton.IsEnabled = true;
        }
    }

    private void ToggleOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay is { IsVisible: true })
        {
            _overlay.Close();
            return;
        }

        _overlay = new OverlayWindow();
        _overlay.Closed += (_, _) =>
        {
            _overlay = null;
            OverlayStatusText.Text = "Overlay выключен";
            OverlayToggleButton.Content = "Показать overlay";
        };
        _overlay.Show();
        _overlay.Update(ActiveSession);
        OverlayStatusText.Text = "Overlay включен и пропускает клики";
        OverlayToggleButton.Content = "Скрыть overlay";
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _state.Settings.ClientId = ClientIdTextBox.Text.Trim();
        _state.Settings.CallbackUrl = CallbackUrlTextBox.Text.Trim();
        _state.Settings.UserAgent = UserAgentTextBox.Text.Trim();
        SaveState("Настройки сохранены");
        SettingsMessageText.Text = "Сохранено. Client secret не нужен и не должен храниться в desktop-приложении с PKCE.";
    }

    private void PreviewSso_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var authorization = EveSso.CreateAuthorization(ClientIdTextBox.Text.Trim(), CallbackUrlTextBox.Text.Trim());
            SsoPreviewText.Text = authorization.Url.ToString();
            SettingsMessageText.Text = "PKCE URL сформирован. Браузер и callback listener пока намеренно не запускаются.";
        }
        catch (ArgumentException error)
        {
            SettingsMessageText.Text = error.Message;
            SsoPreviewText.Text = "";
        }
    }

    private void SaveState(string successMessage)
    {
        try
        {
            StateStore.Save(_state);
            GlobalStatusText.Text = successMessage;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            GlobalStatusText.Text = $"Не удалось сохранить данные: {error.Message}";
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _timer.Stop();
        SaveState("Данные сохранены");
        _overlay?.Close();
        base.OnClosing(e);
    }
}

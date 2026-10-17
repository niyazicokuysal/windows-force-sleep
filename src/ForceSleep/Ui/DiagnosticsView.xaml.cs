using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ForceSleep.Ui;

/// <summary>The "Neden uyumadı?" page: the report, with one-click fixes next to its lines.</summary>
internal partial class DiagnosticsView : UserControl
{
    private readonly AppController _controller;
    private ElevatedScan? _scan;
    private bool _busy;

    public DiagnosticsView(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        SetBusy(true);
        Summary.Text = L.Gathering;
        try
        {
            var scan = _scan;
            var settings = _controller.Settings;
            Render(DiagnosticsReport.Build(await Task.Run(() => DiagnosticsReport.Gather(scan, settings))));
        }
        catch (Exception e)
        {
            Log.Write($"Diagnostics failed: {e}");
            Summary.Text = L.GatherFailed;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnScan(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        ShowStatus(null);
        try
        {
            var scan = await PowerCfg.ScanElevatedAsync();
            if (scan is null)
            {
                ShowStatus(L.ScanDeclined);
                return;
            }
            _scan = scan;
        }
        finally
        {
            SetBusy(false);
        }
        await LoadAsync();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await LoadAsync();

    private void OnLog(object sender, RoutedEventArgs e)
    {
        if (File.Exists(Log.FilePath))
            Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
    }

    private async Task ApplyAsync(Fix fix)
    {
        if (fix is DisableDeviceWakeFix { NeedsConfirmation: true }
            && !ConfirmDialog.Ask(Window.GetWindow(this), L.ConfirmDisableInputWake))
            return;

        SetBusy(true);
        ShowStatus(L.Applying);
        FixResult result;
        try
        {
            result = await Fixes.ApplyAsync(fix, _controller.Settings);
        }
        catch (Exception e)
        {
            Log.Write($"Fix failed: {e}");
            result = new FixResult(false, L.UnexpectedError);
        }
        finally
        {
            SetBusy(false);
        }

        await LoadAsync();
        ShowStatus(result.Message);
        _controller.SettingsChanged();
    }

    private void Render(DiagnosticsReport report)
    {
        Summary.Text = report.Summary;
        Sections.Children.Clear();

        foreach (var section in report.Sections)
        {
            var body = new StackPanel();
            body.Children.Add(new TextBlock
            {
                Text = section.Title,
                Style = (Style)FindResource("SubtitleText"),
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 8),
            });

            foreach (var line in section.Lines)
                body.Children.Add(LineRow(line));

            if (section.Hint is not null)
                body.Children.Add(new TextBlock
                {
                    Text = section.Hint,
                    Style = (Style)FindResource("CaptionText"),
                    Margin = new Thickness(0, 8, 0, 0),
                });

            Sections.Children.Add(new Border { Style = (Style)FindResource("Card"), Child = body, Margin = new Thickness(0, 8, 0, 0) });
        }

        if (_scan is null)
            Sections.Children.Add(new TextBlock
            {
                Text = L.ScanHint,
                Style = (Style)FindResource("CaptionText"),
                Margin = new Thickness(2, 12, 0, 0),
            });
    }

    private UIElement LineRow(ReportLine line)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new TextBlock
        {
            Text = "•",
            Style = (Style)FindResource("BodyText"),
            Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
            Margin = new Thickness(2, 0, 10, 0),
        });

        var text = new TextBlock { Text = line.Text, Style = (Style)FindResource("BodyText") };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        if (line.Action is { } action)
        {
            var link = new Button
            {
                Content = action.Label,
                Style = (Style)FindResource("LinkButton"),
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                IsEnabled = !_busy,
            };
            link.Click += async (_, _) => await ApplyAsync(action.Fix);
            Grid.SetColumn(link, 2);
            row.Children.Add(link);
        }

        return row;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ScanButton.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        foreach (var link in Sections.Children.OfType<Border>()
                     .SelectMany(card => ((Panel)card.Child).Children.OfType<Grid>())
                     .SelectMany(row => row.Children.OfType<Button>()))
            link.IsEnabled = !busy;
    }

    private void ShowStatus(string? text)
    {
        Status.Text = text ?? "";
        Status.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }
}

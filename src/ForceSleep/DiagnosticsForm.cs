using System.Diagnostics;

namespace ForceSleep;

/// <summary>The "Neden uyumadı?" window.</summary>
internal sealed class DiagnosticsForm : Form
{
    private readonly Settings _settings;
    private readonly Label _summary;
    private readonly FlowLayoutPanel _body;
    private readonly Label _status;
    private readonly Button _scanButton;
    private readonly Button _refreshButton;
    private readonly Font _headingFont;
    private readonly Font _hintFont;
    private ElevatedScan? _scan;
    private DiagnosticsReport? _report;
    private bool _busy;

    public DiagnosticsForm(Icon icon, Settings settings)
    {
        _settings = settings;

        Text = L.WindowTitle;
        Icon = icon;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont ?? Font;
        ClientSize = new Size(700, 600);
        MinimumSize = new Size(500, 420);

        _headingFont = new Font(Font, FontStyle.Bold);
        _hintFont = new Font(Font, FontStyle.Italic);

        _summary = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Font = new Font(Font.FontFamily, Font.Size * 1.2f, FontStyle.Bold),
            Padding = new Padding(4, 8, 4, 12),
            Text = L.Gathering,
        };

        _body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = SystemColors.Window,
            Padding = new Padding(8),
        };

        _status = new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText };

        _scanButton = new Button { Text = L.DetailedScan, AutoSize = true };
        _scanButton.Click += async (_, _) => await ScanAsync();
        _refreshButton = new Button { Text = L.Refresh, AutoSize = true };
        _refreshButton.Click += async (_, _) => await LoadAsync();
        var logButton = new Button { Text = L.LogFile, AutoSize = true };
        logButton.Click += (_, _) => OpenLog();
        var closeButton = new Button { Text = L.Close, AutoSize = true, DialogResult = DialogResult.Cancel };
        closeButton.Click += (_, _) => Close();
        CancelButton = closeButton;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttons.Controls.AddRange([closeButton, logButton, _refreshButton, _scanButton]);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        // Without a fixed column, a long unwrapped summary widens the whole layout past the window.
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_summary, 0, 0);
        layout.Controls.Add(_body, 0, 1);
        layout.Controls.Add(_status, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);

        // Labels need an explicit maximum width to wrap, so lay the report out again on resize.
        FitSummaryWidth();
        Resize += (_, _) => FitSummaryWidth();
        _body.ClientSizeChanged += (_, _) =>
        {
            if (_report is not null)
                Render(_report);
        };
    }

    private void FitSummaryWidth() =>
        _summary.MaximumSize = new Size(Math.Max(100, ClientSize.Width - 2 * 12 - _summary.Padding.Horizontal), 0);

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        SetBusy(true);
        _summary.Text = L.Gathering;
        try
        {
            var scan = _scan;
            var input = await Task.Run(() => DiagnosticsReport.Gather(scan, _settings));
            Render(DiagnosticsReport.Build(input));
        }
        catch (Exception e)
        {
            Log.Write($"Diagnostics failed: {e}");
            _summary.Text = L.GatherFailed;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ScanAsync()
    {
        SetBusy(true);
        _status.Text = "";
        try
        {
            var scan = await PowerCfg.ScanElevatedAsync();
            if (scan is null)
            {
                _status.Text = L.ScanDeclined;
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

    private async Task ApplyAsync(Fix fix)
    {
        if (fix is DisableDeviceWakeFix { NeedsConfirmation: true }
            && MessageBox.Show(this, L.ConfirmDisableInputWake,
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        SetBusy(true);
        _status.Text = L.Applying;
        FixResult result;
        try
        {
            result = await Fixes.ApplyAsync(fix, _settings);
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
        _status.Text = result.Message;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _scanButton.Enabled = !busy;
        _refreshButton.Enabled = !busy;
        foreach (var link in _body.Controls.OfType<Control>().SelectMany(c => c.Controls.OfType<LinkLabel>()))
            link.Enabled = !busy;
    }

    private void Render(DiagnosticsReport report)
    {
        _report = report;
        _summary.Text = report.Summary;

        var width = Math.Max(200, _body.ClientSize.Width - _body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);

        _body.SuspendLayout();
        var old = _body.Controls.Cast<Control>().ToList();
        _body.Controls.Clear();
        old.ForEach(c => c.Dispose());

        foreach (var section in report.Sections)
        {
            _body.Controls.Add(TextLabel(section.Title, _headingFont, SystemColors.WindowText, width, topMargin: 10));
            foreach (var line in section.Lines)
                _body.Controls.Add(LineRow(line, width));
            if (section.Hint is not null)
                _body.Controls.Add(TextLabel(section.Hint, _hintFont, SystemColors.GrayText, width, leftMargin: 18));
        }

        if (_scan is null)
            _body.Controls.Add(TextLabel(L.ScanHint,
                _hintFont, SystemColors.GrayText, width, topMargin: 14));

        _body.ResumeLayout();
    }

    private Control LineRow(ReportLine line, int width)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            MaximumSize = new Size(width, 0),
            Margin = new Padding(10, 1, 0, 1),
        };

        row.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(width - 20, 0),
            Text = $"•  {line.Text}",
            Margin = new Padding(0, 2, 6, 2),
        });

        if (line.Action is { } action)
        {
            var link = new LinkLabel { AutoSize = true, Text = action.Label, Margin = new Padding(0, 2, 0, 2), Enabled = !_busy };
            link.LinkClicked += async (_, _) => await ApplyAsync(action.Fix);
            row.Controls.Add(link);
        }

        return row;
    }

    private static Label TextLabel(string text, Font font, Color color, int width, int leftMargin = 0, int topMargin = 2) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(width - leftMargin, 0),
        Text = text,
        Font = font,
        ForeColor = color,
        Margin = new Padding(leftMargin, topMargin, 0, 2),
    };

    private static void OpenLog()
    {
        if (File.Exists(Log.FilePath))
            Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _headingFont.Dispose();
            _hintFont.Dispose();
        }
        base.Dispose(disposing);
    }
}

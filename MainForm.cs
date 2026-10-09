namespace SoapTester;

public class MainForm : Form
{
    record Entry(string Request, SoapResult Result);

    readonly SoapClient client = new();
    readonly ServiceSettings settings = Store.LoadSettings();
    List<EnvironmentConfig> envs = Store.LoadEnvironments();

    readonly ComboBox envBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    readonly TextBox request = NewEditor(readOnly: false);
    readonly TextBox response = NewEditor(readOnly: true);
    readonly CheckBox raw = new() { Text = "Show raw response", AutoSize = true };
    readonly Label status = new() { AutoSize = true, Anchor = AnchorStyles.Right };
    readonly Button send = new() { Text = "Send  (Ctrl+Enter)", AutoSize = true };

    readonly History<Entry> history = new();
    readonly Button back = NavButton("◀"), fwd = NavButton("▶");
    readonly Label pos = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    CancellationTokenSource? cts;

    public MainForm()
    {
        Text = "SOAP tester";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Size = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;

        var manage = new Button { Text = "Manage…", AutoSize = true };
        var top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(8) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(new Label { Text = "Environment", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) }, 0, 0);
        top.Controls.Add(envBox, 1, 0);
        top.Controls.Add(manage, 2, 0);

        pos.Margin = new Padding(6, 6, 12, 0);
        send.Margin = new Padding(0, 0, 0, 0);
        var split = new SplitContainer { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 0) };
        split.Panel1.Controls.Add(Pane("Request XML", request, back, fwd, pos, send));
        split.Panel2.Controls.Add(Pane("Response XML", response, raw));

        var copy = new Button { Text = "Copy response", AutoSize = true };
        var save = new Button { Text = "Save response…", AutoSize = true };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        bottom.Controls.AddRange(new Control[] { copy, save, status });
        status.Margin = new Padding(12, 6, 0, 0);

        Controls.Add(split);
        Controls.Add(top);
        Controls.Add(bottom);
        Shown += (_, _) => split.SplitterDistance = split.Width / 2;

        ReloadEnvironments(envs);
        UpdateNav();

        manage.Click += (_, _) =>
        {
            var current = envBox.SelectedItem as EnvironmentConfig;
            using var dlg = new EnvironmentsForm(envs);
            dlg.ShowDialog(this);
            ReloadEnvironments(envs, current?.Name);
        };
        send.Click += async (_, _) => await SendAsync();
        KeyPreview = true;
        KeyDown += async (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SendAsync(); }
        };

        back.Click += (_, _) =>
        {
            // With unsent edits, Back first restores the entry on screen instead of skipping past it.
            if (history.Current != null && request.Text != history.Current.Request) ShowEntry();
            else { history.Back(); ShowEntry(); }
        };
        fwd.Click += (_, _) => { history.Forward(); ShowEntry(); };
        raw.CheckedChanged += (_, _) => ShowEntry(keepRequest: true);
        copy.Click += (_, _) => { if (response.TextLength > 0) Clipboard.SetText(response.Text); };
        save.Click += (_, _) =>
        {
            using var dlg = new SaveFileDialog { Filter = "XML (*.xml)|*.xml|All files|*.*", FileName = "response.xml" };
            if (dlg.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dlg.FileName, response.Text);
        };
    }

    static TextBox NewEditor(bool readOnly)
    {
        var tb = new TextBox
        {
            Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, AcceptsTab = true, ReadOnly = readOnly,
            Dock = DockStyle.Fill, Font = new Font("Consolas", 10f), MaxLength = 0
        };
        tb.MouseDoubleClick += (_, e) => SelectWordAt(tb, e.Location);
        return tb;
    }

    // Notepad++-style double-click: select only the run of letters, digits and underscores under the cursor.
    static void SelectWordAt(TextBox tb, Point location)
    {
        var text = tb.Text;
        if (text.Length == 0) return;
        var i = tb.GetCharIndexFromPosition(location);
        if (i >= text.Length) i = text.Length - 1;
        static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
        if (!IsWord(text[i]))
        {
            if (i > 0 && IsWord(text[i - 1])) i--;
            else return;
        }
        int start = i, end = i;
        while (start > 0 && IsWord(text[start - 1])) start--;
        while (end < text.Length && IsWord(text[end])) end++;
        tb.Select(start, end - start);
    }

    static Button NavButton(string t) => new() { Text = t, Width = 36, Height = 26, Margin = new Padding(0, 0, 4, 0) };

    static Control Pane(string title, TextBox box, params Control[] extras)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        bar.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 6, 12, 0) });
        bar.Controls.AddRange(extras);
        var panel = new Panel { Dock = DockStyle.Fill };
        panel.Controls.Add(box);
        panel.Controls.Add(bar);
        return panel;
    }

    void ReloadEnvironments(List<EnvironmentConfig> list, string? select = null)
    {
        envs = list;
        envBox.DataSource = null;
        envBox.DataSource = envs.ToList();
        var i = select == null ? -1 : envs.FindIndex(e => e.Name == select);
        if (i >= 0) envBox.SelectedIndex = i;
        else if (envs.Count > 0) envBox.SelectedIndex = 0;
        if (envs.Count == 0) status.Text = "No environments yet. Click Manage… to add one.";
    }

    void ShowEntry(bool keepRequest = false)
    {
        var e = history.Current;
        if (e != null)
        {
            if (!keepRequest) request.Text = e.Request;
            response.Text = raw.Checked ? e.Result.Raw : e.Result.Display;
            status.Text = $"{e.Result.Status} {e.Result.StatusText} · {e.Result.Elapsed.TotalMilliseconds:0} ms";
        }
        else response.Text = "";
        UpdateNav();
    }

    void UpdateNav()
    {
        back.Enabled = history.CanBack || (history.Current != null && request.Text != history.Current.Request);
        fwd.Enabled = history.CanForward;
        pos.Text = history.Count == 0 ? "" : $"{history.Index + 1} of {history.Count}";
    }

    async Task SendAsync()
    {
        if (envBox.SelectedItem is not EnvironmentConfig env)
        {
            MessageBox.Show(this, "Add an environment first (Manage…).", "SOAP tester");
            return;
        }
        if (string.IsNullOrWhiteSpace(request.Text)) { status.Text = "Enter a request first."; return; }

        cts?.Cancel();
        cts = new CancellationTokenSource();
        send.Enabled = false;
        status.Text = $"Signing in and sending to {env.Name}…";
        var text = request.Text;
        try
        {
            var result = await client.SendAsync(env, settings, text, cts.Token);
            history.Add(new Entry(text, result));
            ShowEntry(keepRequest: true);
        }
        catch (Exception ex)
        {
            status.Text = "Failed";
            response.Text = ex.ToString();
        }
        finally
        {
            send.Enabled = true;
            UpdateNav();
        }
    }
}

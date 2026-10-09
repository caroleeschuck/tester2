namespace SoapTester;

public class EnvironmentsForm : Form
{
    readonly List<EnvironmentConfig> envs;
    readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly TextBox name = new() { Dock = DockStyle.Fill };
    readonly TextBox url = new() { Dock = DockStyle.Fill, PlaceholderText = "https://<env>.operations.dynamics.com/" };
    readonly TextBox tenant = new() { Dock = DockStyle.Fill, PlaceholderText = "Tenant ID (GUID) or domain" };
    readonly TextBox company = new() { Dock = DockStyle.Fill, PlaceholderText = "Optional, e.g. USMF" };
    bool loading;

    public EnvironmentsForm(List<EnvironmentConfig> envs)
    {
        this.envs = envs;
        Text = "Manage environments";
        Size = new Size(720, 360);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;

        var add = new Button { Text = "Add", AutoSize = true };
        var remove = new Button { Text = "Delete", AutoSize = true };
        var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange(new Control[] { add, remove });

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.Controls.Add(list, 0, 0);
        left.Controls.Add(buttons, 0, 1);

        var form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control c, int r)
        {
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) }, 0, r);
            form.Controls.Add(c, 1, r);
        }
        Row("Name", name, 0);
        Row("URL", url, 1);
        Row("Tenant ID", tenant, 2);
        Row("Company", company, 3);
        var hint = new Label
        {
            Text = "Sign-in uses your current az login if available, otherwise opens a browser. No secret is stored.",
            AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 10, 0, 0)
        };
        form.Controls.Add(hint, 1, 4);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 200, FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(left);
        split.Panel2.Padding = new Padding(12);
        split.Panel2.Controls.Add(form);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(8) };
        bottom.Controls.Add(close);

        Controls.Add(split);
        Controls.Add(bottom);
        AcceptButton = close;

        RefreshList(0);
        list.SelectedIndexChanged += (_, _) => { if (!loading) ShowSelected(); };
        foreach (var tb in new[] { name, url, tenant, company }) tb.TextChanged += (_, _) => Apply();
        add.Click += (_, _) =>
        {
            envs.Add(new EnvironmentConfig { Name = "New environment" });
            RefreshList(envs.Count - 1);
            name.Focus(); name.SelectAll();
        };
        remove.Click += (_, _) =>
        {
            var i = list.SelectedIndex;
            if (i < 0) return;
            if (MessageBox.Show($"Delete \"{envs[i].Name}\"?", "Delete environment", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            envs.RemoveAt(i);
            RefreshList(Math.Min(i, envs.Count - 1));
        };
        FormClosing += (_, _) => Store.SaveEnvironments(envs);
    }

    void RefreshList(int select)
    {
        loading = true;
        list.DataSource = null;
        list.DataSource = envs.ToList();
        loading = false;
        if (envs.Count > 0) list.SelectedIndex = Math.Clamp(select, 0, envs.Count - 1);
        ShowSelected();
    }

    void ShowSelected()
    {
        loading = true;
        var e = list.SelectedIndex >= 0 ? envs[list.SelectedIndex] : null;
        name.Text = e?.Name ?? ""; url.Text = e?.Url ?? ""; tenant.Text = e?.TenantId ?? ""; company.Text = e?.Company ?? "";
        foreach (var c in new Control[] { name, url, tenant, company }) c.Enabled = e != null;
        loading = false;
    }

    void Apply()
    {
        if (loading || list.SelectedIndex < 0) return;
        var i = list.SelectedIndex;
        var e = envs[i];
        e.Name = name.Text.Trim(); e.Url = url.Text.Trim(); e.TenantId = tenant.Text.Trim(); e.Company = company.Text.Trim();
        loading = true;
        list.DataSource = envs.ToList();
        list.SelectedIndex = i;
        loading = false;
    }
}

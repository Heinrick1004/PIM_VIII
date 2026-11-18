namespace TaskFlow.Desktop;

internal sealed class MainForm : Form
{
    private readonly TextBox _apiUrl = new() { Text = "http://localhost:5046/", Width = 260 };
    private readonly TextBox _email = new() { Width = 220 };
    private readonly TextBox _password = new() { Width = 220, UseSystemPasswordChar = true };
    private readonly ListBox _tasks = new() { Dock = DockStyle.Fill };
    private readonly TextBox _details = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox _status = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _login = new() { Text = "Entrar" };
    private readonly Button _logout = new() { Text = "Sair", Enabled = false };
    private readonly Button _reload = new() { Text = "Atualizar tarefas", Enabled = false };
    private readonly Button _update = new() { Text = "Atualizar status", Enabled = false };
    private TaskFlowApiClient? _client;
    private string? _token;
    private DateTimeOffset _expiresAtUtc;
    private TaskDto? _selected;

    public MainForm()
    {
        Text = "TaskFlow Desktop"; Width = 1000; Height = 650; MinimumSize = new Size(760, 480); StartPosition = FormStartPosition.CenterScreen;
        var login = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 75, Padding = new Padding(8) };
        login.Controls.AddRange([new Label { Text = "API", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, _apiUrl,
            new Label { Text = "E-mail", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, _email,
            new Label { Text = "Senha", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, _password, _login, _logout, _reload]);
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 390 };
        split.Panel1.Controls.Add(_tasks); split.Panel2.Controls.Add(_details);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(8) };
        _status.Items.AddRange(["Pendente", "EmAndamento", "Concluida"]); bottom.Controls.AddRange([_status, _update]);
        Controls.Add(split); Controls.Add(bottom); Controls.Add(login);
        _login.Click += async (_, _) => await LoginAsync(); _logout.Click += (_, _) => Logout();
        _reload.Click += async (_, _) => await LoadTasksAsync(); _tasks.SelectedIndexChanged += async (_, _) => await SelectTaskAsync();
        _update.Click += async (_, _) => await UpdateStatusAsync();
    }

    private async Task LoginAsync()
    {
        try
        {
            if (!Uri.TryCreate(_apiUrl.Text.TrimEnd('/') + "/", UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new InvalidOperationException("Informe uma URL HTTP/HTTPS válida para a API.");
            _client?.Dispose(); _client = new TaskFlowApiClient(uri);
            var response = await _client.LoginAsync(_email.Text, _password.Text);
            if (string.IsNullOrWhiteSpace(response.AccessToken) || response.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException("A API retornou uma sessão vazia ou expirada.");
            _token = response.AccessToken; _expiresAtUtc = response.ExpiresAtUtc; _password.Clear();
            _login.Enabled = false; _logout.Enabled = _reload.Enabled = true;
            await LoadTasksAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task LoadTasksAsync()
    {
        if (_client is null || _token is null) return;
        if (SessionExpired()) return;
        try
        {
            var selectedId = _selected?.Id;
            var tasks = await _client.GetTasksAsync(_token);
            _tasks.DataSource = tasks.ToList();
            if (selectedId.HasValue) _tasks.SelectedItem = tasks.FirstOrDefault(t => t.Id == selectedId.Value);
            if (_tasks.SelectedIndex < 0 && _tasks.Items.Count > 0) _tasks.SelectedIndex = 0;
            if (tasks.Count == 0) { _selected = null; _details.Clear(); _update.Enabled = false; }
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task SelectTaskAsync()
    {
        if (_tasks.SelectedItem is not TaskDto task || _client is null || _token is null) return;
        if (SessionExpired()) return;
        try
        {
            _selected = await _client.GetTaskAsync(_token, task.Id);
            var history = await _client.GetHistoryAsync(_token, task.Id);
            _status.SelectedItem = _selected.Status;
            _details.Text = $"{_selected.Titulo}\r\nStatus: {_selected.Status}\r\nPrioridade: {_selected.Prioridade}\r\nCriada em UTC: {_selected.CriadaEmUtc:O}\r\nPrazo UTC: {_selected.PrazoUtc:O}\r\n\r\n{_selected.Descricao}\r\n\r\nHistórico:\r\n" +
                string.Join("\r\n", history.Select(h => $"{h.DataUtc:O} — {h.Acao}: {h.Descricao}"));
            _update.Enabled = true;
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task UpdateStatusAsync()
    {
        if (_selected is null || _token is null || _client is null || _status.SelectedItem is not string status) return;
        if (SessionExpired()) return;
        try
        {
            _selected = await _client.UpdateStatusAsync(_token, _selected.Id, status, _selected.RowVersion);
            await LoadTasksAsync();
        }
        catch (ApiException ex) when (ex.Status == System.Net.HttpStatusCode.Conflict)
        {
            try { await LoadTasksAsync(); } catch (Exception reloadError) { ShowError(reloadError); }
            MessageBox.Show(this, ex.Message, "Conflito de versão", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void Logout()
    {
        _token = null; _expiresAtUtc = default; _selected = null; _client?.Dispose(); _client = null;
        _tasks.DataSource = null; _details.Clear(); _password.Clear(); _login.Enabled = true;
        _logout.Enabled = _reload.Enabled = _update.Enabled = false;
    }

    private void ShowError(Exception ex)
    {
        if (ex is HttpRequestException or TaskCanceledException)
            MessageBox.Show(this, "Não foi possível conectar à API. Verifique URL e disponibilidade do serviço.", "Conexão", MessageBoxButtons.OK, MessageBoxIcon.Error);
        else MessageBox.Show(this, ex.Message, "TaskFlow", MessageBoxButtons.OK, MessageBoxIcon.Error);
        if (ex is ApiException { Status: System.Net.HttpStatusCode.Unauthorized }) Logout();
    }

    private bool SessionExpired()
    {
        if (_expiresAtUtc > DateTimeOffset.UtcNow) return false;
        MessageBox.Show(this, "Sua sessão expirou. Entre novamente.", "Sessão expirada", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Logout();
        return true;
    }
}

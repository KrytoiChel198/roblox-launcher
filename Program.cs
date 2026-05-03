using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RobloxLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private Button launchClientButton;
        private Button launchGameButton;
        private Button diagButton;
        private Label statusLabel;
        private ListBox favoritesList;
        private Button addFavButton;
        private Button removeFavButton;
        private TextBox hiddenInputBox; // скрытое поле, если захотите запускать конкретную игру
        private List<string> diag = new List<string>();
        private const string AppFolderName = "RobloxLauncher";
        private string FavoritesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName, "favorites.json");
        private List<string> favorites = new List<string>();

        public MainForm()
        {
            Text = "Roblox Launcher";
            Width = 540;
            Height = 360;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            InitializeComponents();
            LoadFavorites();
        }

        private void InitializeComponents()
        {
            var mainPanel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 5 };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            for (int i = 0; i < 5; i++) mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var titleLabel = new Label() { Text = "Нажмите кнопку ниже, чтобы запустить установленный Roblox (никаких ссылок вводить не нужно).", AutoSize = true, Dock = DockStyle.Fill };
            launchClientButton = new Button() { Text = "Launch Roblox", Width = 220, Height = 56, Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold) };
            launchClientButton.Click += LaunchClientButton_Click;

            // Optional: keep old "launch specific game" UI but minimized; user asked "никакую ссылку вводить не надо", so keep it small
            hiddenInputBox = new TextBox() { Dock = DockStyle.Top, Visible = false };
            launchGameButton = new Button() { Text = "Запустить игру (по ID/URL)", Width = 220, Height = 30, Visible = false };
            launchGameButton.Click += (s, e) => LaunchGame(hiddenInputBox.Text.Trim());

            statusLabel = new Label() { Text = "Готово.", AutoSize = false, Height = 40, Dock = DockStyle.Fill, ForeColor = System.Drawing.Color.DarkGreen };

            // favorites
            var favLabel = new Label() { Text = "Избранное (опционально):", Dock = DockStyle.Top, AutoSize = true };
            favoritesList = new ListBox() { Dock = DockStyle.Fill };
            favoritesList.DoubleClick += FavoritesList_DoubleClick;

            addFavButton = new Button() { Text = "Добавить (опц.)", Dock = DockStyle.Top };
            addFavButton.Click += AddFavButton_Click;

            removeFavButton = new Button() { Text = "Удалить выдел.", Dock = DockStyle.Top };
            removeFavButton.Click += RemoveFavButton_Click;

            diagButton = new Button() { Text = "Диагностика", Dock = DockStyle.Top };
            diagButton.Click += DiagButton_Click;

            // Arrange
            mainPanel.Controls.Add(titleLabel, 0, 0);
            mainPanel.SetColumnSpan(titleLabel, 2);

            var centerPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            centerPanel.Controls.Add(launchClientButton);
            centerPanel.Controls.Add(launchGameButton);
            mainPanel.Controls.Add(centerPanel, 0, 1);

            mainPanel.Controls.Add(statusLabel, 0, 2);
            mainPanel.SetColumnSpan(statusLabel, 2);

            mainPanel.Controls.Add(favLabel, 1, 0);
            mainPanel.Controls.Add(favoritesList, 1, 1);
            mainPanel.SetRowSpan(favoritesList, 2);

            var rightButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };
            rightButtons.Controls.Add(addFavButton);
            rightButtons.Controls.Add(removeFavButton);
            rightButtons.Controls.Add(diagButton);
            mainPanel.Controls.Add(rightButtons, 1, 3);

            Controls.Add(mainPanel);
        }

        private void LaunchClientButton_Click(object sender, EventArgs e)
        {
            diag.Clear();
            diag.Add($"Launch client requested at {DateTime.Now}");
            bool ok = TryLaunchViaRegisteredProtocolNoArgs();
            if (ok)
            {
                ShowStatus("Попытка запуска через зарегистрированный протокол отправлена. Если клиент установлен — он должен открыться.");
                return;
            }

            ok = TryLaunchLocalExeNoArgs();
            if (ok)
            {
                ShowStatus("Запущен локальный Roblox executable.");
                return;
            }

            ShowStatus("Не удалось найти клиент напрямую. Попробуйте переустановить Roblox или используйте fallback.", true);
        }

        private void LaunchGame(string input)
        {
            // This method kept for optional direct game launch (not required by you)
            if (string.IsNullOrEmpty(input))
            {
                ShowStatus("Пустой ввод для запуска игры.", true);
                return;
            }

            string url;
            if (Regex.IsMatch(input, @"^\d+$"))
                url = $"https://www.roblox.com/games/{input}";
            else if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                url = input;
            else
            {
                ShowStatus("Ошибка: введите цифровой ID или корректный HTTPS URL", true);
                return;
            }

            // Try protocol with url parameter (if protocol exists)
            if (TryLaunchViaRegisteredProtocolWithArg(url))
            {
                ShowStatus("Попытка запуска игры через протокол отправлена.");
                return;
            }

            if (TryLaunchLocalExeWithArg(url))
            {
                ShowStatus("Попытка запуска игры через локальный exe отправлена.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                ShowStatus("Открыто в браузере (fallback).");
            }
            catch (Exception ex)
            {
                ShowStatus("Не удалось открыть URL: " + ex.Message, true);
            }
        }

        private void DiagButton_Click(object sender, EventArgs e)
        {
            var msg = string.Join(Environment.NewLine, diag);
            if (string.IsNullOrEmpty(msg)) msg = "Диагностика пустая — попробуйте запустить клиент сначала.";
            MessageBox.Show(msg, "Диагностика");
        }

        private void AddFavButton_Click(object sender, EventArgs e)
        {
            // optional: add current hidden input to favs
            var val = hiddenInputBox.Text?.Trim();
            if (string.IsNullOrEmpty(val)) { ShowStatus("Нет значения для добавления.", true); return; }
            if (!favorites.Contains(val, StringComparer.OrdinalIgnoreCase))
            {
                favorites.Add(val);
                RefreshFavoritesList();
                SaveFavorites();
                ShowStatus("Добавлено в избранное.");
            }
            else ShowStatus("Уже в избранном.", true);
        }

        private void RemoveFavButton_Click(object sender, EventArgs e)
        {
            var sel = favoritesList.SelectedItem as string;
            if (sel == null) { ShowStatus("Ничего не выбрано.", true); return; }
            favorites.Remove(sel);
            RefreshFavoritesList();
            SaveFavorites();
            ShowStatus("Удалено.");
        }

        private void FavoritesList_DoubleClick(object sender, EventArgs e)
        {
            var sel = favoritesList.SelectedItem as string;
            if (sel != null)
            {
                hiddenInputBox.Text = sel;
                LaunchGame(sel);
            }
        }

        private void ShowStatus(string text, bool isError = false)
        {
            statusLabel.ForeColor = isError ? System.Drawing.Color.DarkRed : System.Drawing.Color.DarkGreen;
            statusLabel.Text = text;
            diag.Add("Status: " + text);
        }

        // --- Protocol/no-arg launch helpers ---

        private bool TryLaunchViaRegisteredProtocolNoArgs()
        {
            var protocols = new[] { "roblox-player", "roblox", "rbx" };
            foreach (var proto in protocols)
            {
                var cmd = ReadProtocolCommandFromRegistry(proto);
                if (string.IsNullOrEmpty(cmd))
                {
                    diag.Add($"Protocol {proto} not found in registry.");
                    continue;
                }

                diag.Add($"Protocol {proto} command: {cmd}");
                var exe = ExtractExePathFromCommand(cmd);
                if (exe != null && File.Exists(exe))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                        diag.Add($"Started EXE from protocol: {exe}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        diag.Add($"Failed to start EXE from protocol {exe}: {ex.Message}");
                    }
                }
                else
                {
                    // If command itself contains a protocol URI (like roblox-player://something) try launching the protocol directly
                    try
                    {
                        Process.Start(new ProcessStartInfo($"{proto}://") { UseShellExecute = true });
                        diag.Add($"Started protocol URI: {proto}://");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        diag.Add($"Failed to start protocol URI {proto}:// : {ex.Message}");
                    }
                }
            }
            return false;
        }

        private bool TryLaunchLocalExeNoArgs()
        {
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var versionsPath = Path.Combine(localAppData, "Roblox", "Versions");
                diag.Add($"Looking for local Roblox at: {versionsPath}");
                if (!Directory.Exists(versionsPath)) { diag.Add("Versions folder not found."); return false; }

                var candidates = Directory.EnumerateFiles(versionsPath, "*.exe", SearchOption.AllDirectories)
                    .Where(f => Path.GetFileName(f).IndexOf("RobloxPlayer", StringComparison.OrdinalIgnoreCase) >= 0
                             || Path.GetFileName(f).IndexOf("RobloxApp", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                diag.Add($"Found candidate exes: {candidates.Count}");
                foreach (var c in candidates)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(c) { UseShellExecute = true });
                        diag.Add($"Started local exe: {c}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        diag.Add($"Failed to start {c}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                diag.Add("Error scanning local exe: " + ex.Message);
            }
            return false;
        }

        private bool TryLaunchViaRegisteredProtocolWithArg(string url)
        {
            var protocols = new[] { "roblox-player", "roblox", "rbx" };
            foreach (var proto in protocols)
            {
                var cmd = ReadProtocolCommandFromRegistry(proto);
                if (string.IsNullOrEmpty(cmd)) { diag.Add($"Protocol {proto} not found."); continue; }
                diag.Add($"Protocol {proto} command: {cmd}");
                var exe = ExtractExePathFromCommand(cmd);
                try
                {
                    if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                    {
                        // if registry command included %1, replace it; else pass URL as arg
                        string args = cmd.Contains("%1") ? cmd.Substring(cmd.IndexOf(exe) + exe.Length).Replace("%1", $"\"{url}\"") : $"\"{url}\"";
                        args = TrimSurrounding(args);
                        Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
                        diag.Add($"Started {exe} with args: {args}");
                        return true;
                    }
                    else
                    {
                        // Try protocol uri
                        var protoUri = $"{proto}://{Uri.EscapeDataString(url)}";
                        Process.Start(new ProcessStartInfo(protoUri) { UseShellExecute = true });
                        diag.Add($"Started protocol uri: {protoUri}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    diag.Add($"Failed to start via protocol {proto}: {ex.Message}");
                }
            }
            return false;
        }

        private bool TryLaunchLocalExeWithArg(string url)
        {
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var versionsPath = Path.Combine(localAppData, "Roblox", "Versions");
                if (!Directory.Exists(versionsPath)) return false;

                var candidates = Directory.EnumerateFiles(versionsPath, "*.exe", SearchOption.AllDirectories)
                    .Where(f => Path.GetFileName(f).IndexOf("RobloxPlayer", StringComparison.OrdinalIgnoreCase) >= 0
                             || Path.GetFileName(f).IndexOf("RobloxApp", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                foreach (var c in candidates)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(c, $"\"{url}\"") { UseShellExecute = true });
                        diag.Add($"Started {c} with arg: {url}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        diag.Add($"Failed to start {c} with arg: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                diag.Add("Error when trying local exe with arg: " + ex.Message);
            }
            return false;
        }

        private string ReadProtocolCommandFromRegistry(string protocol)
        {
            try
            {
                var key1 = $@"Software\Classes\{protocol}\shell\open\command";
                using (var k = Registry.CurrentUser.OpenSubKey(key1))
                {
                    if (k != null)
                    {
                        var v = k.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(v)) return v;
                    }
                }

                var key2 = $@"{protocol}\shell\open\command";
                using (var k2 = Registry.ClassesRoot.OpenSubKey(key2))
                {
                    if (k2 != null)
                    {
                        var v = k2.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(v)) return v;
                    }
                }
            }
            catch (Exception ex)
            {
                diag.Add($"Registry read error for {protocol}: {ex.Message}");
            }
            return null;
        }

        private static string ExtractExePathFromCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return null;
            var m = Regex.Match(command, @"['""]?(?<path>[^'""]+?\.exe)['""]?", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups["path"].Value;
            var idx = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var s = command.Substring(0, idx + 4).Trim().Trim('"', '\'');
                return s;
            }
            return null;
        }

        private static string TrimSurrounding(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : s.Trim().Trim('"').Trim();
        }

        // Favorites persistence (same as before)
        private void LoadFavorites()
        {
            try
            {
                if (File.Exists(FavoritesPath))
                {
                    var json = File.ReadAllText(FavoritesPath);
                    favorites = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
            }
            catch { favorites = new List<string>(); }
            RefreshFavoritesList();
        }

        private void SaveFavorites()
        {
            try
            {
                var dir = Path.GetDirectoryName(FavoritesPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = System.Text.Json.JsonSerializer.Serialize(favorites);
                File.WriteAllText(FavoritesPath, json);
            }
            catch { /* ignore */ }
        }

        private void RefreshFavoritesList()
        {
            favoritesList.Items.Clear();
            foreach (var f in favorites) favoritesList.Items.Add(f);
        }
    }
}

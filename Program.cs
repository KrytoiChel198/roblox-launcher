using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

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
        private TextBox inputBox;
        private Button launchButton;
        private Label statusLabel;
        private ListBox favoritesList;
        private Button addFavButton;
        private Button removeFavButton;
        private Button openFolderButton;
        private const string AppFolderName = "RobloxLauncher";
        private string FavoritesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName, "favorites.json");
        private List<string> favorites = new List<string>();

        public MainForm()
        {
            Text = "Roblox Launcher";
            Width = 520;
            Height = 380;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            InitializeComponents();
            LoadFavorites();
        }

        private void InitializeComponents()
        {
            var mainPanel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 2, RowCount = 6 };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            for (int i = 0; i < 6; i++) mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var promptLabel = new Label() { Text = "Введите Place ID (только цифры) или полный HTTPS URL игры Roblox:", AutoSize = true, Dock = DockStyle.Fill };
            inputBox = new TextBox() { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 6, 6) };
            launchButton = new Button() { Text = "Запустить", Width = 110, Height = 32, Dock = DockStyle.Left };
            launchButton.Click += LaunchButton_Click;

            statusLabel = new Label() { Text = "", AutoSize = false, Height = 40, Dock = DockStyle.Fill, ForeColor = System.Drawing.Color.DarkGreen };

            favoritesList = new ListBox() { Dock = DockStyle.Fill };
            favoritesList.DoubleClick += FavoritesList_DoubleClick;

            addFavButton = new Button() { Text = "Добавить из поля", Dock = DockStyle.Top };
            addFavButton.Click += AddFavButton_Click;

            removeFavButton = new Button() { Text = "Удалить выдел.", Dock = DockStyle.Top };
            removeFavButton.Click += RemoveFavButton_Click;

            openFolderButton = new Button() { Text = "Открыть папку с данными", Dock = DockStyle.Top };
            openFolderButton.Click += OpenFolderButton_Click;

            // Layout
            mainPanel.Controls.Add(promptLabel, 0, 0);
            mainPanel.SetColumnSpan(promptLabel, 2);
            mainPanel.Controls.Add(inputBox, 0, 1);
            mainPanel.Controls.Add(launchButton, 0, 2);
            mainPanel.Controls.Add(statusLabel, 0, 3);
            mainPanel.SetColumnSpan(statusLabel, 2);

            var favLabel = new Label() { Text = "Избранное:", Dock = DockStyle.Top, AutoSize = true };
            mainPanel.Controls.Add(favLabel, 1, 0);
            mainPanel.Controls.Add(favoritesList, 1, 1);
            mainPanel.SetRowSpan(favoritesList, 2);

            var favButtonsPanel = new FlowLayoutPanel() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };
            favButtonsPanel.Controls.Add(addFavButton);
            favButtonsPanel.Controls.Add(removeFavButton);
            favButtonsPanel.Controls.Add(openFolderButton);
            mainPanel.Controls.Add(favButtonsPanel, 1, 3);

            Controls.Add(mainPanel);
        }

        private void OpenFolderButton_Click(object sender, EventArgs e)
        {
            var dir = Path.GetDirectoryName(FavoritesPath) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }

        private void AddFavButton_Click(object sender, EventArgs e)
        {
            var val = inputBox.Text.Trim();
            if (string.IsNullOrEmpty(val)) { ShowStatus("Нельзя добавить пустую строку.", true); return; }
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
            ShowStatus("Удалено из избранного.");
        }

        private void FavoritesList_DoubleClick(object sender, EventArgs e)
        {
            var sel = favoritesList.SelectedItem as string;
            if (sel != null)
            {
                inputBox.Text = sel;
                Launch(sel);
            }
        }

        private void LaunchButton_Click(object sender, EventArgs e)
        {
            var input = inputBox.Text.Trim();
            Launch(input);
        }

        private void Launch(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                ShowStatus("Ошибка: пустой ввод", true);
                return;
            }

            string url;
            if (System.Text.RegularExpressions.Regex.IsMatch(input, @"^\d+$"))
            {
                url = $"https://www.roblox.com/games/{input}";
            }
            else if (Uri.TryCreate(input, UriKind.Absolute, out var uriResult) && uriResult.Scheme == Uri.UriSchemeHttps)
            {
                url = input;
            }
            else
            {
                ShowStatus("Ошибка: введите только цифровой ID или корректный HTTPS URL", true);
                return;
            }

            // Try multiple launch methods:
            // 1) Try registered protocol (roblox-player://...) if available in registry
            // 2) Try local RobloxPlayerLauncher.exe if found under %LocalAppData%\Roblox\Versions\
            // 3) Fallback: open https URL in default browser
            try
            {
                // 1) Try protocol handler if registered
                bool protocolLaunched = TryLaunchProtocol(url);
                if (protocolLaunched)
                {
                    ShowStatus("Запущено через зарегистрированный протокол Roblox (если он есть).");
                    return;
                }

                // 2) Try local launcher exe
                bool exeLaunched = TryLaunchLocalExe(url);
                if (exeLaunched)
                {
                    ShowStatus("Запущено через локальный RobloxPlayerLauncher.exe.");
                    return;
                }

                // 3) Fallback to browser
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                ShowStatus("Открыто в браузере (fallback). Roblox Player должен запуститься автоматически.");
            }
            catch (Exception ex)
            {
                ShowStatus("Не удалось запустить: " + ex.Message, true);
            }
        }

        private bool TryLaunchProtocol(string url)
        {
            // Check whether a protocol like "roblox-player" or "roblox" is registered in HKCR
            try
            {
                // We will attempt a few plausible protocol URIs, but these may vary by system.
                // Try list (in order): roblox-player://, roblox://, rbx://
                var attempts = new[]
                {
                    $"roblox-player://{Uri.EscapeDataString(url)}",
                    $"roblox://{Uri.EscapeDataString(url)}",
                    $"rbx://{Uri.EscapeDataString(url)}"
                };

                foreach (var uri in attempts)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                        // If no exception thrown, assume it worked.
                        return true;
                    }
                    catch
                    {
                        // ignore and try next
                    }
                }
            }
            catch
            {
                // ignore overall failures
            }
            return false;
        }

        private bool TryLaunchLocalExe(string url)
        {
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var versionsPath = Path.Combine(localAppData, "Roblox", "Versions");
                if (!Directory.Exists(versionsPath)) return false;

                var exePath = Directory.EnumerateFiles(versionsPath, "RobloxPlayerLauncher.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (exePath == null) return false;

                // Many Roblox launchers accept the game URL as an argument — try that.
                Process.Start(new ProcessStartInfo(exePath, $"\"{url}\"") { UseShellExecute = true });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ShowStatus(string text, bool isError = false)
        {
            statusLabel.ForeColor = isError ? System.Drawing.Color.DarkRed : System.Drawing.Color.DarkGreen;
            statusLabel.Text = text;
        }

        private void LoadFavorites()
        {
            try
            {
                if (File.Exists(FavoritesPath))
                {
                    var json = File.ReadAllText(FavoritesPath);
                    favorites = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
            }
            catch
            {
                favorites = new List<string>();
            }
            RefreshFavoritesList();
        }

        private void SaveFavorites()
        {
            try
            {
                var dir = Path.GetDirectoryName(FavoritesPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(favorites);
                File.WriteAllText(FavoritesPath, json);
            }
            catch
            {
                // ignore save errors
            }
        }

        private void RefreshFavoritesList()
        {
            favoritesList.Items.Clear();
            foreach (var f in favorites) favoritesList.Items.Add(f);
        }
    }
}

// name=Program.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        private TextBox inputBox;
        private Button launchButton;
        private Label statusLabel;
        private ListBox favoritesList;
        private Button addFavButton;
        private Button removeFavButton;
        private Button diagButton;
        private const string AppFolderName = "RobloxLauncher";
        private string FavoritesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName, "favorites.json");
        private List<string> favorites = new List<string>();
        private List<string> diag = new List<string>();

        public MainForm()
        {
            Text = "Roblox Launcher";
            Width = 560;
            Height = 420;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            InitializeComponents();
            LoadFavorites();
        }

        private void InitializeComponents()
        {
            var mainPanel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 6 };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            for (int i = 0; i < 6; i++) mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var promptLabel = new Label() { Text = "Введите Place ID (только цифры) или полный HTTPS URL игры Roblox:", AutoSize = true, Dock = DockStyle.Fill };
            inputBox = new TextBox() { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 6, 6) };
            launchButton = new Button() { Text = "Запустить", Width = 120, Height = 36, Dock = DockStyle.Left };
            launchButton.Click += LaunchButton_Click;

            statusLabel = new Label() { Text = "", AutoSize = false, Height = 50, Dock = DockStyle.Fill, ForeColor = System.Drawing.Color.DarkGreen };

            favoritesList = new ListBox() { Dock = DockStyle.Fill };
            favoritesList.DoubleClick += FavoritesList_DoubleClick;

            addFavButton = new Button() { Text = "Добавить из поля", Dock = DockStyle.Top };
            addFavButton.Click += AddFavButton_Click;

            removeFavButton = new Button() { Text = "Удалить выдел.", Dock = DockStyle.Top };
            removeFavButton.Click += RemoveFavButton_Click;

            diagButton = new Button() { Text = "Диагностика", Dock = DockStyle.Top };
            diagButton.Click += DiagButton_Click;

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
            favButtonsPanel.Controls.Add(diagButton);
            mainPanel.Controls.Add(favButtonsPanel, 1, 3);

            Controls.Add(mainPanel);
        }

        private void DiagButton_Click(object sender, EventArgs e)
        {
            var diagText = string.Join(Environment.NewLine, diag);
            MessageBox.Show(string.IsNullOrEmpty(diagText) ? "Диагностики пока нет — попробуйте запустить игру." : diagText, "Диагностика");
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
            diag.Clear();
            diag.Add($"Запуск: {input} at {DateTime.Now}");

            if (string.IsNullOrEmpty(input))
            {
                ShowStatus("Ошибка: пустой ввод", true);
                return;
            }

            string url;
            if (Regex.IsMatch(input, @"^\d+$"))
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

            // 1) Try protocol handler from registry
            var protocolResult = TryLaunchViaRegisteredProtocol(url);
            if (protocolResult)
            {
                ShowStatus("Запущено через протокол Roblox (реестр).");
                return;
            }

            // 2) Try local RobloxPlayerLauncher.exe under %LocalAppData%\Roblox\Versions
            var exeResult = TryLaunchLocalLauncherExe(url);
            if (exeResult)
            {
                ShowStatus("Запущено через локальный RobloxPlayerLauncher.exe.");
                return;
            }

            // 3) Fallback
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                ShowStatus("Открыто в браузере (fallback). Roblox Player должен запуститься автоматически.");
                diag.Add("Fallback: browser open executed.");
            }
            catch (Exception ex)
            {
                ShowStatus("Не удалось открыть URL: " + ex.Message, true);
                diag.Add("Fallback failed: " + ex.ToString());
            }
        }

        private bool TryLaunchViaRegisteredProtocol(string url)
        {
            // проверяем несколько стандартных имён протоколов
            var protocols = new[] { "roblox-player", "roblox", "rbx" };
            foreach (var proto in protocols)
            {
                var command = ReadProtocolCommandFromRegistry(proto);
                if (string.IsNullOrEmpty(command))
                {
                    diag.Add($"Protocol '{proto}' не найден в реестре.");
                    continue;
                }

                diag.Add($"Protocol '{proto}' command from registry: {command}");

                var exe = ExtractExePathFromCommand(command);
                if (exe == null)
                {
                    diag.Add($"Не удалось извлечь путь к exe из строки: {command}");
                    continue;
                }

                if (!File.Exists(exe))
                {
                    diag.Add($"Exe из реестра не найден: {exe}");
                    continue;
                }

                // Подготавливаем аргументы: если в command есть %1, заменяем, иначе добавляем URL в конец
                string args = "";
                if (command.Contains("%1"))
                {
                    // Иногда команда формата: "C:\...\RobloxPlayerLauncher.exe" "%1"
                    // Мы передадим значение, заменив %1 на url
                    args = command.Substring(command.IndexOf(exe) + exe.Length).Trim();
                    args = args.Replace("%1", $"\"{url}\"");
                    // Уберём лишние кавычки вокруг exe (они уже отделены)
                    // Но при ProcessStart укажем exe в FileName и args в Arguments
                    args = TrimSurrounding(args);
                }
                else
                {
                    // нет %1 — просто передадим URL как аргумент
                    args = $"\"{url}\"";
                }

                try
                {
                    diag.Add($"Запуск exe '{exe}' с аргументами: {args}");
                    Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
                    return true;
                }
                catch (Exception ex)
                {
                    diag.Add($"Запуск exe из реестра не удался: {ex.Message}");
                    continue;
                }
            }

            return false;
        }

        private string ReadProtocolCommandFromRegistry(string protocol)
        {
            try
            {
                // 1) HKEY_CURRENT_USER\Software\Classes\<protocol>\shell\open\command
                var keyPaths = new[]
                {
                    $@"Software\Classes\{protocol}\shell\open\command",
                    $@"{protocol}\shell\open\command" // under HKEY_CLASSES_ROOT
                };

                // Try HKCU first
                using (var k1 = Registry.CurrentUser.OpenSubKey(keyPaths[0]))
                {
                    if (k1 != null)
                    {
                        var val = k1.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(val)) return val;
                    }
                }

                // Then HKEY_CLASSES_ROOT
                using (var k2 = Registry.ClassesRoot.OpenSubKey(keyPaths[1]))
                {
                    if (k2 != null)
                    {
                        var val = k2.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(val)) return val;
                    }
                }
            }
            catch (Exception ex)
            {
                diag.Add($"Ошибка чтения реестра для протокола {protocol}: {ex.Message}");
            }

            return null;
        }

        private bool TryLaunchLocalLauncherExe(string url)
        {
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var versionsPath = Path.Combine(localAppData, "Roblox", "Versions");
                diag.Add($"Пытаемся найти Roblox в: {versionsPath}");
                if (!Directory.Exists(versionsPath))
                {
                    diag.Add("Папка Versions не найдена.");
                    return false;
                }

                // Ищем RobloxPlayerLauncher.exe или RobloxPlayerBeta.exe
                var candidates = Directory.EnumerateFiles(versionsPath, "*.exe", SearchOption.AllDirectories)
                    .Where(fn => Path.GetFileName(fn).IndexOf("RobloxPlayer", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                if (!candidates.Any())
                {
                    diag.Add("Не найден ни один candidate exe в Versions.");
                    return false;
                }

                diag.Add($"Найдено кандидатов: {candidates.Count}");
                foreach (var c in candidates)
                {
                    diag.Add($"Попытка запуска: {c}");
                    try
                    {
                        // Многие локальные лончеры принимают URL как аргумент
                        Process.Start(new ProcessStartInfo(c, $"\"{url}\"") { UseShellExecute = true });
                        diag.Add($"Запущено: {c}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        diag.Add($"Не удалось запустить {c}: {ex.Message}");
                        // пробуем следующий
                    }
                }
            }
            catch (Exception ex)
            {
                diag.Add("Ошибка при поиске локального exe: " + ex.Message);
            }

            return false;
        }

        private static string ExtractExePathFromCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return null;
            // Попробуем регулярку, чтобы извлечь первый путь к .exe
            var m = Regex.Match(command, @"[""']?(?<path>[^""']+?\.exe)[""']?", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return m.Groups["path"].Value;
            }

            // как fallback — взять всё до .exe
            var idx = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var substr = command.Substring(0, idx + 4).Trim();
                // уберём кавычки
                substr = substr.Trim('"', '\'');
                return substr;
            }

            return null;
        }

        private static string TrimSurrounding(string s)
        {
            if (s == null) return "";
            return s.Trim().Trim('"').Trim();
        }

        private void ShowStatus(string text, bool isError = false)
        {
            statusLabel.ForeColor = isError ? System.Drawing.Color.DarkRed : System.Drawing.Color.DarkGreen;
            statusLabel.Text = text;
            diag.Add("Status: " + text);
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

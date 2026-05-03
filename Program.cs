using System;
using System.Diagnostics;
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
        private Label promptLabel;

        public MainForm()
        {
            Text = "Roblox Launcher";
            Width = 420;
            Height = 180;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            promptLabel = new Label() { Left = 12, Top = 12, Width = 380, Text = "Введите Place ID (только цифры) или полный URL игры Roblox:" };
            inputBox = new TextBox() { Left = 12, Top = 36, Width = 380 };
            launchButton = new Button() { Left = 12, Top = 70, Width = 100, Text = "Запустить" };
            statusLabel = new Label() { Left = 12, Top = 105, Width = 380, ForeColor = System.Drawing.Color.DarkGreen };

            launchButton.Click += LaunchButton_Click;
            Controls.Add(promptLabel);
            Controls.Add(inputBox);
            Controls.Add(launchButton);
            Controls.Add(statusLabel);
        }

        private void LaunchButton_Click(object sender, EventArgs e)
        {
            var input = inputBox.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                statusLabel.ForeColor = System.Drawing.Color.DarkRed;
                statusLabel.Text = "Ошибка: пустой ввод";
                return;
            }

            string url;
            if (System.Text.RegularExpressions.Regex.IsMatch(input, @"^\d+$"))
            {
                url = $"https://www.roblox.com/games/{input}";
            }
            else if (Uri.TryCreate(input, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttps))
            {
                url = input;
            }
            else
            {
                statusLabel.ForeColor = System.Drawing.Color.DarkRed;
                statusLabel.Text = "Ошибка: введите только цифровой ID или корректный HTTPS URL";
                return;
            }

            try
            {
                var psi = new ProcessStartInfo(url) { UseShellExecute = true };
                Process.Start(psi);
                statusLabel.ForeColor = System.Drawing.Color.DarkGreen;
                statusLabel.Text = "Открыто: " + url + " — Roblox Player должен запуститься.";
            }
            catch (Exception ex)
            {
                statusLabel.ForeColor = System.Drawing.Color.DarkRed;
                statusLabel.Text = "Не удалось открыть URL: " + ex.Message;
            }
        }
    }
}

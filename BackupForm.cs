using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace GrafanaBackup
{
    public partial class BackupForm : Form
    {
        private static readonly HttpClient client = new HttpClient();

        private TextBox apiKeyTextBox;
        private TextBox urlTextBox;
        private Button backupButton;
        private Button openFolderButton;
        private Button exitButton;
        private Label statusLabel;
        private string lastBackupDirectory = "";

        public BackupForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.apiKeyTextBox = new TextBox() { PlaceholderText = "API Key", Width = 400 };
            this.urlTextBox = new TextBox() { PlaceholderText = "Grafana URL (ex: https://seu-url-aqui.com.br)", Width = 400 };
            this.backupButton = new Button() { Text = "Fazer Backup", Width = 150 };
            this.openFolderButton = new Button() { Text = "Abrir Pasta", Width = 150 };
            this.exitButton = new Button() { Text = "Sair", Width = 150 };
            this.statusLabel = new Label() { Width = 400, AutoSize = true };

            this.backupButton.Click += async (sender, e) => await BackupDashboards();
            this.openFolderButton.Click += OpenBackupFolder;
            this.exitButton.Click += (sender, e) => Application.Exit();

            var layout = new FlowLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Controls.Add(apiKeyTextBox);
            layout.Controls.Add(urlTextBox);
            layout.Controls.Add(backupButton);
            layout.Controls.Add(openFolderButton);
            layout.Controls.Add(exitButton);
            layout.Controls.Add(statusLabel);

            this.Controls.Add(layout);
            this.Text = "Backup de Dashboards Grafana";
            this.Width = 500;
            this.Height = 250;
        }

        private async Task BackupDashboards()
        {
            string apiKey = apiKeyTextBox.Text.Trim();
            string grafanaUrl = urlTextBox.Text.Trim().TrimEnd('/');

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(grafanaUrl))
            {
                MessageBox.Show("Por favor, preencha a API Key e a URL do Grafana.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupDirectory = Path.Combine(Environment.CurrentDirectory, $"GrafanaBackup_{timestamp}");
                Directory.CreateDirectory(backupDirectory);
                lastBackupDirectory = backupDirectory;

                var dashboards = await ListDashboards(grafanaUrl);

                foreach (var dashboard in dashboards)
                {
                    await BackupDashboard(grafanaUrl, dashboard, backupDirectory);
                }

                statusLabel.Text = $"Backup concluído! Salvou em: {backupDirectory}";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Erro: {ex.Message}";
            }
        }

        private static async Task<List<string>> ListDashboards(string grafanaUrl)
        {
            var dashboards = new List<string>();
            string listUrl = $"{grafanaUrl}/api/search?query=&";

            var response = await client.GetAsync(listUrl);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var jsonArray = JArray.Parse(jsonResponse);

            foreach (var item in jsonArray)
            {
                var uid = item["uid"]?.ToString();
                if (!string.IsNullOrEmpty(uid))
                {
                    dashboards.Add(uid);
                }
            }

            return dashboards;
        }

        private static async Task BackupDashboard(string grafanaUrl, string uid, string backupDirectory)
        {
            string dashboardUrl = $"{grafanaUrl}/api/dashboards/uid/{uid}";
            var response = await client.GetAsync(dashboardUrl);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var fullJson = JObject.Parse(jsonResponse);

            var dashboardOnly = fullJson["dashboard"];

            var dashboardTitle = dashboardOnly?["title"]?.ToString() ?? "dashboard_sem_nome";
            var safeTitle = string.Join("_", dashboardTitle.Split(Path.GetInvalidFileNameChars()));

            string backupFilePath = Path.Combine(backupDirectory, $"{safeTitle}_backup.json");

            await File.WriteAllTextAsync(backupFilePath, dashboardOnly.ToString());

            Console.WriteLine($"Backup da dashboard '{dashboardTitle}' salvo em: {backupFilePath}");
        }

        private void OpenBackupFolder(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(lastBackupDirectory) && Directory.Exists(lastBackupDirectory))
            {
                Process.Start("explorer.exe", lastBackupDirectory);
            }
            else
            {
                MessageBox.Show("Nenhum backup foi feito ainda ou a pasta não existe.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}

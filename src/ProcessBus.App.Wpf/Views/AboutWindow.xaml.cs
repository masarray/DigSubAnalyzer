using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace ProcessBus.App.Wpf.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            ShowActualInstalledBuild();
        }

        private void ShowActualInstalledBuild()
        {
            // Neither a hardcoded About version nor an installer filename proves
            // which ProcessBusInsight.exe the Windows shortcut actually launched.
            var assembly = typeof(AboutWindow).Assembly;
            var information = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var version = string.IsNullOrWhiteSpace(information)
                ? assembly.GetName().Version?.ToString(3) ?? "unknown"
                : information.Split('+', 2)[0];

            InstalledVersionText.Text = "Version " + version;
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "ProcessBusInsight.exe");
            InstalledPathText.Text = "Path: " + exe;
            InstalledPathText.ToolTip = exe;
            InstalledBuildText.Text = "Build identity unavailable";

            try
            {
                var manifest = Path.Combine(AppContext.BaseDirectory, "BuildInfo.json");
                if (!File.Exists(manifest))
                {
                    InstalledBuildText.Text = "Build identity missing (older install)";
                    return;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(manifest));
                var root = document.RootElement;
                var commit = root.GetProperty("commit").GetString() ?? string.Empty;
                var sourceVersion = root.GetProperty("version").GetString() ?? string.Empty;
                var runId = root.TryGetProperty("githubRunId", out var run)
                    ? run.GetString() ?? string.Empty : string.Empty;

                if (commit.Length != 40 || !System.Text.RegularExpressions.Regex.IsMatch(
                    commit, "^[0-9a-fA-F]{40}$"))
                {
                    InstalledBuildText.Text = "Build identity invalid";
                    return;
                }

                var shortId = commit[..8];
                if (!string.Equals(sourceVersion, version, StringComparison.OrdinalIgnoreCase))
                {
                    InstalledBuildText.Text = "BUILD VERSION MISMATCH · " + shortId;
                }
                else
                {
                    InstalledBuildText.Text = "Build " + shortId +
                        (string.IsNullOrEmpty(runId) ? string.Empty : " · CI " + runId);
                }

                InstalledBuildText.ToolTip = "Commit: " + commit +
                    "\nInstalled version: " + sourceVersion +
                    "\nExecutable: " + exe;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or
                InvalidOperationException)
            {
                InstalledBuildText.Text = "Build identity unreadable";
                InstalledBuildText.ToolTip = ex.Message;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

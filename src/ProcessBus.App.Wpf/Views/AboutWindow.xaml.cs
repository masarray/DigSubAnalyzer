using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace ProcessBus.App.Wpf.Views;

public partial class AboutWindow : Window
{
    private const string RepositoryUrl = "https://github.com/masarray/DigSubAnalyzer";
    private const string LicenseUrl = RepositoryUrl + "/blob/main/LICENSE";
    private const string CommercialUrl = RepositoryUrl + "/blob/main/COMMERCIAL-LICENSE.md";
    private const string ThirdPartyUrl = RepositoryUrl + "/blob/main/THIRD_PARTY_NOTICES.md";

    public AboutWindow()
    {
        InitializeComponent();
        PopulateBuildIdentity();
    }

    private void PopulateBuildIdentity()
    {
        var assembly = typeof(AboutWindow).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            informationalVersion = assembly.GetName().Version?.ToString() ?? "unknown";
        }

        var versionParts = informationalVersion.Split('+', 2, StringSplitOptions.TrimEntries);
        VersionText.Text = $"Version {versionParts[0]}";

        if (versionParts.Length == 2 && !string.IsNullOrWhiteSpace(versionParts[1]))
        {
            var commit = versionParts[1];
            BuildText.Text = $"Commit {commit[..Math.Min(commit.Length, 12)]}";
        }
        else
        {
            BuildText.Text = "Build commit not embedded";
        }
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }

    private void OpenRepository_Click(object sender, RoutedEventArgs e) => OpenUrl(RepositoryUrl);
    private void OpenLicense_Click(object sender, RoutedEventArgs e) => OpenUrl(LicenseUrl);
    private void OpenCommercial_Click(object sender, RoutedEventArgs e) => OpenUrl(CommercialUrl);
    private void OpenThirdParty_Click(object sender, RoutedEventArgs e) => OpenUrl(ThirdPartyUrl);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

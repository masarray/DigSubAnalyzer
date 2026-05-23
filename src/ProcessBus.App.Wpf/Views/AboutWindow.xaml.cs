using System;
using System.Globalization;
using System.Reflection;
using System.Windows;

namespace ProcessBus.App.Wpf.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            Loaded += AboutWindow_Loaded;
        }

        private void AboutWindow_Loaded(object sender, RoutedEventArgs e)
        {
            object? snapshot = Application.Current != null && Application.Current.Properties.Contains("LicenseSnapshot")
                ? Application.Current.Properties["LicenseSnapshot"]
                : null;
            if (snapshot == null)
            {
                LicenseStateTextBlock.Text = "-";
                TrialCounterTextBlock.Text = "-";
                TrialStartTextBlock.Text = "-";
                return;
            }

            LicenseStateTextBlock.Text = GetLicenseStateText(snapshot);
            bool isLicensed = GetBool(snapshot, "IsLicensed");
            int daysElapsed = GetInt(snapshot, "DaysElapsed");
            int daysRemaining = GetInt(snapshot, "DaysRemaining");
            DateTime firstRunUtc = GetDateTime(snapshot, "FirstRunUtc");

            TrialCounterTextBlock.Text = isLicensed
                ? "Licensed"
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "Day {0} / {1} ({2} days left)",
                    Math.Min(daysElapsed + 1, 30),
                    30,
                    daysRemaining);
            TrialStartTextBlock.Text = firstRunUtc == DateTime.MinValue
                ? "-"
                : firstRunUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private static string GetLicenseStateText(object snapshot)
        {
            if (GetBool(snapshot, "IsLicensed"))
            {
                return "Licensed";
            }

            return GetBool(snapshot, "IsExpired") ? "Expired" : "Trial";
        }

        private static bool GetBool(object target, string propertyName)
        {
            object? value = GetPropertyValue(target, propertyName);
            return value is bool && (bool)value;
        }

        private static int GetInt(object target, string propertyName)
        {
            object? value = GetPropertyValue(target, propertyName);
            return value is int ? (int)value : 0;
        }

        private static DateTime GetDateTime(object target, string propertyName)
        {
            object? value = GetPropertyValue(target, propertyName);
            return value is DateTime ? (DateTime)value : DateTime.MinValue;
        }

        private static object? GetPropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            PropertyInfo? property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property != null ? property.GetValue(target, null) : null;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

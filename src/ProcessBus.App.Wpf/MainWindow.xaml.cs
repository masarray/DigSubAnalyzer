using ProcessBus.Core.Presentation;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Threading;
using ProcessBus.App.Wpf.ViewModels;
using ProcessBus.App.Wpf.Views;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ProcessBus.App.Wpf;

public partial class MainWindow : Window
{
    private DateTime _lastGooseInteractionUtc = DateTime.MinValue;
    private bool _gooseWorkspaceHooksAttached;
    private bool _gooseSourceSyncing;
    private bool _gooseSourceSyncQueued;
    private bool _gooseSourceKeyboardSelecting;
    private GoosePublisherKey? _gooseSourceRangeAnchor;
    private bool _gooseFollowQueued;
    private bool _gooseProgrammaticScroll;
    private int _pendingWorkspaceTabIndex = -1;
    private bool _isWorkspaceTabSwitchQueued;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;

    public MainWindow()
    {
        InitializeComponent();
        if (DataContext is MainWindowViewModel viewModel) { }

        Loaded += GooseWorkspace_Loaded;
        Loaded += (_, _) => { UpdateHeaderDensity(); UpdateWorkspaceActivePill(animate: false); };
        SizeChanged += (_, _) => UpdateHeaderDensity();
        Closing += MainWindow_Closing;
        Closed += (_, _) => Application.Current?.Shutdown();
        WorkspaceSegmentRail.SizeChanged += (_, _) => UpdateWorkspaceActivePill(animate: false);
    }


    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_shutdownCompleted)
            return;

        e.Cancel = true;

        if (_shutdownStarted)
            return;

        _shutdownStarted = true;
        IsEnabled = false;

        try
        {
            if (DataContext is MainWindowViewModel viewModel)
                await viewModel.ShutdownAsync();
        }
        catch
        {
            // Closing must be reliable even when a capture adapter does not stop cleanly.
        }

        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        await Dispatcher.InvokeAsync(() =>
        {
            _shutdownCompleted = true;
            Close();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };

        aboutWindow.ShowDialog();
    }

    private void LockedFeature_Click(object sender, RoutedEventArgs e)
    {
        var featureName = sender is FrameworkElement { Tag: string tag } && !string.IsNullOrWhiteSpace(tag)
            ? tag
            : "Engineering Platform Module";

        MessageBox.Show(
            this,
            $"{featureName} is not part of the receive-only product build.",
            "Feature not included",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // Specialist pages are preserved but intentionally excluded from the operator tab rail.
    private void WorkspaceToolsButton_Click(object sender, RoutedEventArgs e)
    {
        if (ToolsWorkspaceButton.ContextMenu is not { } menu)
            return;

        menu.PlacementTarget = ToolsWorkspaceButton;
        menu.IsOpen = true;
    }

    private void WorkspaceToolMenuItem_Click(object sender, RoutedEventArgs e)
        => WorkspaceTabButton_Click(sender, e);

    // The original header had seven fixed-width tabs and clipped at desktop sizes.
    // Choose compact dimensions only on narrow windows; no data/capture refresh.
    private bool _compactHeader;
    private void UpdateHeaderDensity()
    {
        if (!IsLoaded || HeaderAdapterComboBox is null || WorkspaceSegmentButtons is null)
            return;

        var compact = ActualWidth < 1450;
        if (_compactHeader == compact && HeaderAdapterComboBox.Width == (compact ? 180 : 250))
            return;

        _compactHeader = compact;
        HeaderProductSubtitle.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        HeaderProductLogo.Width = compact ? 32 : 38;
        HeaderProductLogo.Height = compact ? 32 : 38;
        HeaderProductTitle.FontSize = compact ? 13 : 15;
        HeaderIdentityGap.Width = new GridLength(compact ? 12 : 18);
        HeaderAdapterComboBox.Width = compact ? 180 : 250;
        HeaderCaptureToolbar.Margin = new Thickness(compact ? 14 : 20, 0, 0, 0);
        foreach (var tab in WorkspaceSegmentButtons.Children.OfType<RadioButton>())
            tab.Width = compact ? 94 : 112;

        UpdateWorkspaceActivePill(animate: false);
    }

    private void WorkspaceTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } ||
            !int.TryParse(tag, out var tabIndex))
            return;

        if (WorkspaceTabs.SelectedIndex == tabIndex)
            return;

        _pendingWorkspaceTabIndex = tabIndex;

        if (_isWorkspaceTabSwitchQueued)
            return;

        _isWorkspaceTabSwitchQueued = true;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (_pendingWorkspaceTabIndex >= 0 &&
                    WorkspaceTabs.SelectedIndex != _pendingWorkspaceTabIndex)
                {
                    WorkspaceTabs.SelectedIndex = _pendingWorkspaceTabIndex;
                }

                UpdateWorkspaceActivePill(animate: true);
            }
            finally
            {
                _pendingWorkspaceTabIndex = -1;
                _isWorkspaceTabSwitchQueued = false;
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void WorkspaceTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        UpdateWorkspaceActivePill(animate: true);
    }

    private void UpdateWorkspaceActivePill(bool animate)
    {
        if (WorkspaceSegmentRail is null ||
            WorkspaceActivePill is null ||
            WorkspaceActivePillTransform is null ||
            WorkspaceSegmentButtons is null)
            return;

        var primaryTabs = WorkspaceSegmentButtons.Children.OfType<RadioButton>().ToArray();
        var selectedButton = primaryTabs.FirstOrDefault(button =>
            string.Equals(button.Tag?.ToString(), WorkspaceTabs.SelectedIndex.ToString(), StringComparison.Ordinal));

        // Tab selection may also originate in the VM, keyboard navigation, or Tools.
        foreach (var tab in primaryTabs)
            tab.IsChecked = ReferenceEquals(tab, selectedButton);

        bool inTools = WorkspaceTabs.SelectedIndex is 4 or 6;
        ToolsWorkspaceButton.ToolTip = inTools
            ? (WorkspaceTabs.SelectedIndex == 4 ? "Validation · select another tool" : "Advanced · select another tool")
            : "More tools: Validation and Advanced";
        ToolsWorkspaceButton.Background = inTools
            ? new SolidColorBrush(Color.FromRgb(22, 76, 119))
            : Brushes.Transparent;

        // No blue primary-tab pill should remain selected when a specialist page is open.
        WorkspaceActivePill.Visibility = selectedButton is null ? Visibility.Collapsed : Visibility.Visible;
        if (selectedButton is null || selectedButton.ActualWidth <= 0)
            return;

        var targetPoint = selectedButton.TransformToAncestor(WorkspaceSegmentRail).Transform(new Point(0, 0));
        var targetX = targetPoint.X;
        var targetWidth = Math.Max(1, selectedButton.ActualWidth);

        if (!animate)
        {
            WorkspaceActivePillTransform.X = targetX;
            WorkspaceActivePill.Width = targetWidth;
            return;
        }

        var slide = new DoubleAnimation
        {
            To = targetX,
            Duration = TimeSpan.FromMilliseconds(360),
            EasingFunction = new BackEase
            {
                Amplitude = 0.32,
                EasingMode = EasingMode.EaseOut
            }
        };

        var resize = new DoubleAnimation
        {
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        WorkspaceActivePillTransform.BeginAnimation(TranslateTransform.XProperty, slide, HandoffBehavior.SnapshotAndReplace);
        WorkspaceActivePill.BeginAnimation(WidthProperty, resize, HandoffBehavior.SnapshotAndReplace);
    }

    private void SvPill_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(0.975, 0.975);
        }
    }

    private void SvPill_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(1.0, 1.0);
        }
    }

    private void SvPill_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(1.0, 1.0);
        }
    }

    private void GooseClearSearch_Click(object sender, RoutedEventArgs e)
    {
        // The command clears exactly one filter property. Restore keyboard focus
        // without blocking the capture thread or queuing another UI refresh.
        GooseSourceSearchBox.Focus();
    }

    private void GooseWorkspace_Loaded(object sender, RoutedEventArgs e)
    {
        if (_gooseWorkspaceHooksAttached || DataContext is not MainWindowViewModel vm) return;
        _gooseWorkspaceHooksAttached = true;
        vm.PropertyChanged += GooseViewModel_PropertyChanged;
        vm.GoosePublisherRows.CollectionChanged += GooseSources_CollectionChanged;
        vm.GooseVisibleMessagesAppended += GooseVisibleMessagesAppended;
        QueueGooseSourceSync();
    }

    private void GooseViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.GoosePublisherFilterText)
            or nameof(MainWindowViewModel.GooseSelectedSourceCount))
            QueueGooseSourceSync();
    }

    private void GooseSources_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        QueueGooseSourceSync();

    private void QueueGooseSourceSync()
    {
        if (_gooseSourceSyncQueued) return;
        _gooseSourceSyncQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _gooseSourceSyncQueued = false;
            SyncGooseSourceSelection();
        }), DispatcherPriority.Background);
    }

    private void SyncGooseSourceSelection()
    {
        if (_gooseSourceSyncing || DataContext is not MainWindowViewModel vm) return;
        _gooseSourceSyncing = true;
        try
        {
            var visible = GooseSourcesList.Items.OfType<GoosePublisherRow>()
                .Where(row => vm.SelectedGooseSourceKeys.Contains(row.Key)).ToArray();
            var selected = GooseSourcesList.SelectedItems.OfType<GoosePublisherRow>().ToArray();
            if (visible.Length == selected.Length && visible.All(selected.Contains)) return;
            GooseSourcesList.SelectedItems.Clear();
            foreach (var row in visible)
                GooseSourcesList.SelectedItems.Add(row);
        }
        finally { _gooseSourceSyncing = false; }
    }

    private void GoosePublisherRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm ||
            sender is not ListViewItem { DataContext: GoosePublisherRow row })
            return;

        var chosen = vm.SelectedGooseSourceKeys.ToHashSet();
        var modifier = Keyboard.Modifiers;
        if ((modifier & ModifierKeys.Shift) != 0)
        {
            var visible = GooseSourcesList.Items.OfType<GoosePublisherRow>().ToArray();
            var anchor = Array.FindIndex(visible, item => item.Key == _gooseSourceRangeAnchor);
            var target = Array.FindIndex(visible, item => item.Key == row.Key);
            if (anchor < 0) anchor = target;
            if (target >= 0)
                for (var i = Math.Min(anchor, target); i <= Math.Max(anchor, target); i++)
                    chosen.Add(visible[i].Key);
        }
        else if ((modifier & ModifierKeys.Control) != 0)
        {
            if (!chosen.Add(row.Key)) chosen.Remove(row.Key);
            _gooseSourceRangeAnchor = row.Key;
        }
        else
        {
            if (chosen.Count == 1 && chosen.Contains(row.Key)) chosen.Clear();
            else { chosen.Clear(); chosen.Add(row.Key); }
            _gooseSourceRangeAnchor = row.Key;
        }

        vm.SetGooseSourceSelection(chosen);
        // Only plain click intentionally changes the live Inspector target.
        if (modifier == ModifierKeys.None && chosen.Contains(row.Key))
            vm.SelectedGoosePublisherRow = row;
        SyncGooseSourceSelection();
        ((ListViewItem)sender).Focus();
        e.Handled = true; // WPF must not undo our explicit toggle/range semantics.
    }

    private void GooseSources_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp
            or Key.PageDown or Key.Space)
            _gooseSourceKeyboardSelecting = true;
    }

    private void GooseSources_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        _gooseSourceKeyboardSelecting = false;
    }

    private void GooseSources_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_gooseSourceSyncing || !_gooseSourceKeyboardSelecting ||
            DataContext is not MainWindowViewModel vm) return;
        // Hidden selected identities are retained when search narrows the left rail.
        var visible = GooseSourcesList.Items.OfType<GoosePublisherRow>()
            .Select(row => row.Key).ToHashSet();
        var keys = vm.SelectedGooseSourceKeys.Where(key => !visible.Contains(key)).ToHashSet();
        foreach (var row in GooseSourcesList.SelectedItems.OfType<GoosePublisherRow>())
            keys.Add(row.Key);
        vm.SetGooseSourceSelection(keys);
        if (Keyboard.Modifiers == ModifierKeys.None &&
            GooseSourcesList.SelectedItem is GoosePublisherRow focus)
            vm.SelectedGoosePublisherRow = focus;
    }

    private void GooseVisibleMessagesAppended(GooseTrafficRow row)
    {
        if (DataContext is MainWindowViewModel { FollowGooseMessages: true })
            QueueGooseFollow();
    }

    private void QueueGooseFollow()
    {
        if (_gooseFollowQueued || !GooseMessagesGrid.IsVisible) return;
        _gooseFollowQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _gooseFollowQueued = false;
            if (DataContext is not MainWindowViewModel { FollowGooseMessages: true } vm ||
                !GooseMessagesGrid.IsVisible) return;
            var lastVisible = vm.GooseHistoryView.OfType<GooseTrafficRow>().LastOrDefault();
            if (lastVisible is null) return;
            _gooseProgrammaticScroll = true;
            GooseMessagesGrid.ScrollIntoView(lastVisible);
            Dispatcher.BeginInvoke(new Action(() => _gooseProgrammaticScroll = false),
                DispatcherPriority.ContextIdle);
        }), DispatcherPriority.Loaded);
    }

    private void GooseFollowLive_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.FollowGooseMessages = true;
        QueueGooseFollow();
    }

    private void GooseMessagesGrid_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true &&
            DataContext is MainWindowViewModel { FollowGooseMessages: true })
            QueueGooseFollow();
    }

    private void SuspendGooseFollow()
    {
        if (!_gooseProgrammaticScroll && DataContext is MainWindowViewModel vm)
            vm.FollowGooseMessages = false;
    }

    private void GooseMessagesGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        SuspendGooseFollow();

    private void GooseMessagesGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        SuspendGooseFollow();

    private void GooseMessagesGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or
            Key.Home or Key.End or Key.Space)
            SuspendGooseFollow();
    }

    private void GooseMessagesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0) SuspendGooseFollow();
    }

    private void GooseMessagesGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e) =>
        SuspendGooseFollow();

    private void GooseMessagesGrid_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Real scrollbar/touch navigation away from bottom is an operator action.
        // Content growth and programmatic follow do not suspend the follow state.
        if (!_gooseProgrammaticScroll && e.ExtentHeightChange == 0 &&
            Math.Abs(e.VerticalChange) > 0.01 &&
            e.VerticalOffset < e.ExtentHeight - e.ViewportHeight - 2)
            SuspendGooseFollow();
    }

    private void GooseMessagesGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        // Recycling must never transfer a previous event's visual accent.
        e.Row.ClearValue(Control.BackgroundProperty);
        if (e.Row.Item is not GooseTrafficRow row ||
            row.EventType == "Retrans" ||
            row.VisibleSinceUtc == DateTime.MinValue ||
            DateTime.UtcNow - row.VisibleSinceUtc > TimeSpan.FromSeconds(1.5) ||
            e.Row.IsSelected) return;
        var brush = new SolidColorBrush(Color.FromArgb(53, 41, 125, 179));
        e.Row.Background = brush;
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
        {
            To = Color.FromArgb(0, 41, 125, 179),
            Duration = new Duration(TimeSpan.FromSeconds(1.25)),
            FillBehavior = FillBehavior.HoldEnd
        });
    }

    private void GooseTraffic_Interaction(object sender, MouseEventArgs e)
    {
        DeferGooseTrafficFlush();
    }

    private void GooseTraffic_ClickInteraction(object sender, MouseButtonEventArgs e)
    {
        DeferGooseTrafficFlush();
    }

    private void DeferGooseTrafficFlush()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastGooseInteractionUtc).TotalMilliseconds < 120)
            return;

        _lastGooseInteractionUtc = now;

        if (DataContext is not MainWindowViewModel viewModel ||
            !viewModel.GooseInteractionCommand.CanExecute(null))
            return;

        viewModel.GooseInteractionCommand.Execute(null);
    }
}

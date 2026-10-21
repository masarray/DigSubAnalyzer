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
    private int _pendingWorkspaceTabIndex = -1;
    private bool _isWorkspaceTabSwitchQueued;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;

    public MainWindow()
    {
        InitializeComponent();
        if (DataContext is MainWindowViewModel viewModel) { }

        Loaded += (_, _) =>
        {
            UpdateHeaderDensity(); UpdateWorkspaceActivePill(animate: false);
            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += GooseViewModel_PropertyChanged;
                SyncGooseSourceVisualSelection();
            }
        };
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

    private void GooseViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.GoosePublisherFilterText) or
            nameof(MainWindowViewModel.GooseSourceScopeText))
            Dispatcher.BeginInvoke(new Action(SyncGooseSourceVisualSelection),
                System.Windows.Threading.DispatcherPriority.ContextIdle);

        if (e.PropertyName == nameof(MainWindowViewModel.GooseVisibleAppendRevision))
            ScheduleGooseFollowScroll(force: false);
        else if (e.PropertyName == nameof(MainWindowViewModel.GooseFollowLive) &&
                 sender is MainWindowViewModel { GooseFollowLive: true })
            ScheduleGooseFollowScroll(force: true);
    }

    private bool _syncingGooseSourceSelection;
    private ProcessBus.Core.Presentation.GoosePublisherKey? _gooseSourceRangeAnchor;

    private void GoosePublisherRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm ||
            sender is not ListViewItem { DataContext: ProcessBus.Core.Presentation.GoosePublisherRow row })
            return;

        // This click ACKs exactly this physical publisher's decoded signal changes.
        // Modifier/range selection never ACKs any other source in the set.
        vm.AcknowledgeGooseSource(row.Key);
        var next = vm.SelectedGooseSourceKeys.ToHashSet();
        var modifiers = Keyboard.Modifiers;
        var shift = modifiers.HasFlag(ModifierKeys.Shift);
        var control = modifiers.HasFlag(ModifierKeys.Control);
        if (shift && _gooseSourceRangeAnchor.HasValue)
        {
            var visible = GooseSourceList.Items.OfType<ProcessBus.Core.Presentation.GoosePublisherRow>().ToList();
            var start = visible.FindIndex(item => item.Key == _gooseSourceRangeAnchor.Value);
            var finish = visible.FindIndex(item => item.Key == row.Key);
            if (start >= 0 && finish >= 0)
            {
                if (!control) next.Clear();
                foreach (var item in visible.Skip(Math.Min(start, finish)).Take(Math.Abs(start - finish) + 1))
                    next.Add(item.Key);
            }
            else next.Add(row.Key);
        }
        else if (control)
        {
            if (!next.Add(row.Key)) next.Remove(row.Key);
        }
        else
        {
            var onlySelected = next.Count == 1 && next.Contains(row.Key);
            next.Clear();
            if (!onlySelected) next.Add(row.Key);
        }
        if (!shift) _gooseSourceRangeAnchor = row.Key;
        vm.SetGooseSourceSelection(next, !shift && !control && next.Contains(row.Key) ? row : null);
        SyncGooseSourceVisualSelection();
        e.Handled = true;
        ((ListViewItem)sender).Focus();
    }

    private void GooseSourceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingGooseSourceSelection || DataContext is not MainWindowViewModel vm)
            return;
        // Native keyboard selection is supported; a filtered-out selection is
        // not an operator deselection and must survive in stable key storage.
        var next = vm.SelectedGooseSourceKeys.ToHashSet();
        foreach (ProcessBus.Core.Presentation.GoosePublisherRow row in e.RemovedItems)
            if (GooseSourceList.Items.Contains(row)) next.Remove(row.Key);
        foreach (ProcessBus.Core.Presentation.GoosePublisherRow row in e.AddedItems)
            next.Add(row.Key);
        vm.SetGooseSourceSelection(next);
    }

    private void GooseSourceList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter) || DataContext is not MainWindowViewModel vm ||
            GooseSourceList.SelectedItem is not ProcessBus.Core.Presentation.GoosePublisherRow row)
            return;
        // Space toggles membership and ACKs; Enter ACKs the focused source only.
        vm.AcknowledgeGooseSource(row.Key);
        if (e.Key == Key.Enter) { e.Handled = true; return; }
        var next = vm.SelectedGooseSourceKeys.ToHashSet();
        if (!next.Add(row.Key)) next.Remove(row.Key);
        vm.SetGooseSourceSelection(next);
        SyncGooseSourceVisualSelection();
        e.Handled = true;
    }

    private void SyncGooseSourceVisualSelection()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        _syncingGooseSourceSelection = true;
        try
        {
            GooseSourceList.SelectedItems.Clear();
            foreach (ProcessBus.Core.Presentation.GoosePublisherRow item in GooseSourceList.Items)
                if (vm.SelectedGooseSourceKeys.Contains(item.Key))
                    GooseSourceList.SelectedItems.Add(item);
        }
        finally { _syncingGooseSourceSelection = false; }
    }

    private void GooseTraffic_Interaction(object sender, MouseEventArgs e)
    {
        DeferGooseTrafficFlush();
    }

    private void GooseTraffic_ClickInteraction(object sender, MouseButtonEventArgs e)
    {
        SuspendGooseFollowFromOperator();
        DeferGooseTrafficFlush();
        if (e.ChangedButton != MouseButton.Left) return;

        // ACK only the row that was actually clicked, not the selected item in
        // the VM: that item may still reference an earlier pinned history event.
        // Never ACK all rows, nor ACK on passive streaming/virtualized recycling.
        for (var node = e.OriginalSource as DependencyObject;
             node is not null && !ReferenceEquals(node, GooseMessageGrid);
             node = VisualTreeHelper.GetParent(node))
        {
            if (node is DataGridRow { DataContext: GooseTrafficRow history })
            {
                history.Acknowledge();
                return;
            }
        }
    }

    private bool _gooseFollowScrollScheduled;
    private bool _gooseForceFollowScroll;

    private void SuspendGooseFollowFromOperator()
    {
        if (DataContext is MainWindowViewModel vm)
            vm.SuspendGooseFollow();
    }

    private void GooseMessageGrid_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) ScheduleGooseFollowScroll(force: true);
    }

    private void GooseMessageGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        SuspendGooseFollowFromOperator();

    private void GooseMessageGrid_PreviewTouchDown(object sender, TouchEventArgs e) =>
        SuspendGooseFollowFromOperator();

    private void GooseMessageGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.Home or Key.End or
            Key.PageUp or Key.PageDown or Key.Space or Key.Left or Key.Right or Key.Enter)
            SuspendGooseFollowFromOperator();

        // Accessible keyboard acknowledgement of the explicitly focused event.
        // Directional navigation suspends follow but does not ACK history.
        if (e.Key is Key.Enter or Key.Space &&
            GooseMessageGrid.SelectedItem is GooseTrafficRow selected)
        {
            selected.Acknowledge();
            e.Handled = true;
        }
    }

    private static T? FindGooseVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T result) return result;
            var descendant = FindGooseVisualChild<T>(child);
            if (descendant is not null) return descendant;
        }
        return null;
    }

    private bool IsGooseGridAtBottom()
    {
        var scroll = FindGooseVisualChild<ScrollViewer>(GooseMessageGrid);
        return scroll is null || scroll.VerticalOffset >= scroll.ScrollableHeight - 1.5;
    }

    private void ScheduleGooseFollowScroll(bool force)
    {
        if (DataContext is not MainWindowViewModel { GooseFollowLive: true }) return;
        if (!force && !IsGooseGridAtBottom())
        {
            SuspendGooseFollowFromOperator();
            return;
        }
        _gooseForceFollowScroll |= force;
        if (_gooseFollowScrollScheduled) return;
        _gooseFollowScrollScheduled = true;
        // The view has already received the entire bounded append batch.
        // No selection/focus mutation, no per-frame or per-packet dispatch.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _gooseFollowScrollScheduled = false;
            var shouldScroll = _gooseForceFollowScroll;
            _gooseForceFollowScroll = false;
            if (DataContext is not MainWindowViewModel vm || !vm.GooseFollowLive ||
                !GooseMessageGrid.IsVisible) return;
            if (shouldScroll || IsGooseGridAtBottom() ||
                vm.GooseVisibleAppendRevision > 0)
            {
                var last = vm.GooseLatestVisibleTrafficRow;
                if (last is not null) GooseMessageGrid.ScrollIntoView(last);
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
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

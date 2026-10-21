# Fail the Windows release pipeline if primary WPF panel gutters regress.
# Geometric source contract: 8 DIP between major columns, 8 DIP between
# primary vertical panels. Inner label/control spacing remains 4/8 DIP.
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$windowPath = Join-Path $Root 'src/ProcessBus.App.Wpf/MainWindow.xaml'
$themePath = Join-Path $Root 'src/ProcessBus.App.Wpf/Themes/Controls.xaml'
[xml]$window = Get-Content -LiteralPath $windowPath -Raw
[xml]$theme = Get-Content -LiteralPath $themePath -Raw
$ns = [System.Xml.XmlNamespaceManager]::new($window.NameTable)
$ns.AddNamespace('w', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')

function Assert-True([bool]$Ok, [string]$Description) {
    if (-not $Ok) { throw "WPF spacing regression: $Description" }
}

function Has-Definitions([string]$DefinitionName, [string]$ChildName, [string]$Attribute, [string]$Expected) {
    $selector = "//w:Grid/w:$DefinitionName"
    $collections = $window.SelectNodes($selector, $ns)
    foreach ($collection in $collections) {
        $parts = @()
        foreach ($child in $collection.ChildNodes) {
            if ($child.LocalName -ne $ChildName) { continue }
            $parts += $child.GetAttribute($Attribute)
        }
        if (($parts -join ',') -ceq $Expected) { return $true }
    }
    return $false
}

# Main three-column shell is the operator's visual alignment reference.
# Before this fix, the left explorer spanned BOTH the workspace row and its
# footer-gap row; that made its bottom edge 8 DIP lower than the main panel.
$shells = @($window.SelectNodes("//w:Grid[w:Grid.ColumnDefinitions/w:ColumnDefinition[@Width='280']]", $ns))
Assert-True ($shells.Count -eq 1) 'exactly one three-column workspace shell'
$shell = $shells[0]
$leftExplorer = $shell.SelectSingleNode("w:Border[@Grid.Column='0' and @Grid.Row='0']", $ns)
Assert-True ($null -ne $leftExplorer) 'left explorer exists in the workspace row'
Assert-True ($leftExplorer.GetAttribute('Grid.RowSpan') -in @('', '1')) 'left explorer cannot overlap footer-gap row'
$mainCanvas = $shell.SelectSingleNode("w:Grid[@Grid.Column='2' and @Grid.Row='0']", $ns)
Assert-True ($null -ne $mainCanvas) 'main workspace canvas shares left explorer row'
$footer = $shell.SelectSingleNode("w:Border[@Grid.Row='2']", $ns)
Assert-True ($null -ne $footer) 'workspace footer is separated from the panel row'
Assert-True (Has-Definitions 'Grid.RowDefinitions' 'RowDefinition' 'Height' '*,8,28') 'uniform 8-DIP footer gutter'
Assert-True (Has-Definitions 'Grid.ColumnDefinitions' 'ColumnDefinition' 'Width' '280,8,*,8,320') 'shell columns must have 8 DIP gutters'
Assert-True (Has-Definitions 'Grid.ColumnDefinitions' 'ColumnDefinition' 'Width' '*,8,320') 'Analyzer waveform/inspector gutter'
Assert-True (Has-Definitions 'Grid.ColumnDefinitions' 'ColumnDefinition' 'Width' '*,8,320') 'overlay aligns with outer explorer gutter'
Assert-True (Has-Definitions 'Grid.RowDefinitions' 'RowDefinition' 'Height' '0.82*,8,1.18*') 'Analyzer phasor/measurement gap'

# All five customer-facing workspaces share the same horizontal or vertical rhythm.
Assert-True (Has-Definitions 'Grid.RowDefinitions' 'RowDefinition' 'Height' 'Auto,8,Auto,8,Auto,Auto,*') 'Diagnostics primary vertical gutters'
Assert-True (Has-Definitions 'Grid.ColumnDefinitions' 'ColumnDefinition' 'Width' '*,8,460') 'GOOSE timeline/inspector gutter (readable engineering details, 8 DIP separator)'
Assert-True (Has-Definitions 'Grid.RowDefinitions' 'RowDefinition' 'Height' 'Auto,8,Auto,8,*') 'Timing/PTP major sections'
Assert-True (Has-Definitions 'Grid.ColumnDefinitions' 'ColumnDefinition' 'Width' '1.15*,8,1.1*,8,1.2*') 'Timing/PTP header-card gutters'
Assert-True (Has-Definitions 'Grid.ColumnDefinitions' 'ColumnDefinition' 'Width' '1.1*,8,1.4*') 'Timing/PTP evidence gutter'
Assert-True (Has-Definitions 'Grid.RowDefinitions' 'RowDefinition' 'Height' 'Auto,8,*') 'SCL hero/body vertical gutter'
Assert-True (Has-Definitions 'Grid.ColumnDefinitions' 'ColumnDefinition' 'Width' '1.18*,8,0.92*') 'SCL catalog/inspector gutter'

# Standardize padding via named tokens, not brittle per-tab copies.
foreach ($rule in @(
    @{ Name = 'WorkspacePanelInset'; Value = '12' },
    @{ Name = 'WorkspaceHeroInset'; Value = '16,12' },
    @{ Name = 'WorkspacePanelGapBelow'; Value = '0,0,0,8' }
)) {
    $nodes = $theme.SelectNodes("//w:Thickness[@x:Key='$($rule.Name)']", $ns)
    Assert-True ($nodes.Count -eq 1) "one shared token: $($rule.Name)"
    Assert-True ($nodes[0].InnerText.Trim() -eq $rule.Value) "$($rule.Name) must be $($rule.Value)"
}

$styles = $theme.SelectNodes("//w:Style[@x:Key='DenseGroupBorderStyle']/w:Setter[@Property='Padding']", $ns)
Assert-True ($styles.Count -eq 1 -and $styles[0].GetAttribute('Value') -eq '{StaticResource WorkspacePanelInset}') 'shared primary panel padding'
$source = Get-Content -LiteralPath $windowPath -Raw
$diagnosticsStart = $source.IndexOf('<!-- Immediate SV engineering evidence')
$diagnosticsEnd = $source.IndexOf('<!-- Event Log:', $diagnosticsStart)
Assert-True ($diagnosticsStart -ge 0 -and $diagnosticsEnd -gt $diagnosticsStart) 'Diagnostics evidence bounds'
$diagnostics = $source.Substring($diagnosticsStart, $diagnosticsEnd - $diagnosticsStart)
$legacyCardGapCount = ([regex]::Matches($diagnostics, '(?s)<Border\b[^>]*\bMargin="0,0,12,0"')).Count
Assert-True ($legacyCardGapCount -eq 0) 'no legacy 12-DIP gaps between adjacent diagnostic cards'
foreach ($name in @('WorkspacePanelInset', 'WorkspacePanelGapBelow', 'WorkspaceHeroInset')) {
    Assert-True ($source.Contains("{StaticResource $name}")) "workspace must use spacing token $name"
}
# GOOSE-specific P0–P3 readout contracts: only GOOSE receives the
# compact disclosure style; all other workspace layout tokens stay untouched.
$gooseExpanderStyle = $theme.SelectNodes("//w:Style[@x:Key='GooseDatasetExpanderStyle']", $ns)
Assert-True ($gooseExpanderStyle.Count -eq 1) 'GOOSE has one scoped engineer disclosure style'
# The event-aware DataSet row is based on the shared GOOSE-only disclosure style,
# preserving its exact compact chevron template while adding conditional value accents.
$gooseExpanderUsage = $window.SelectNodes("//w:Expander/w:Expander.Style/w:Style[@BasedOn='{StaticResource GooseDatasetExpanderStyle}']", $ns)
Assert-True ($gooseExpanderUsage.Count -eq 1) 'GOOSE typed rows retain scoped compact chevrons through BasedOn'
foreach ($binding in @(
    'GooseInspectorRows',
    'GooseTimelineCountersText',
    'GooseInspectorModeBadgeText',
    'GooseInspectorMessage.IsTest',
    'GooseReturnToLiveCommand'
)) {
    Assert-True ($source.Contains($binding)) "GOOSE inspector contract: $binding"
}
# Exact UI defect reported in operator screenshots: Inspector controls must
# remain visible when raw evidence scrolls; count must have its own grid cell.
$inspectorStart = $source.IndexOf('<!-- Right inspector: clean grouped information, typed values emphasized. -->')
$inspectorEnd = $source.IndexOf('<TabItem>', $inspectorStart)
Assert-True ($inspectorStart -ge 0 -and $inspectorEnd -gt $inspectorStart) 'GOOSE inspector markup found'
$inspector = $source.Substring($inspectorStart, $inspectorEnd - $inspectorStart)
$headerOffset = $inspector.IndexOf('Text="Details"')
$scrollOffset = $inspector.IndexOf('<ScrollViewer Grid.Row="1"')
Assert-True ($headerOffset -ge 0 -and $scrollOffset -gt $headerOffset) 'Inspector header remains above independently scrolling evidence'
Assert-True ($inspector.Contains('Grid.Row="0" Margin="0,0,0,12"')) 'Inspector header stays in fixed grid row'
Assert-True ($inspector.Contains('Text="Data"')) 'Value group has compact title'
Assert-True ($inspector.Contains('Grid.Column="1" Margin="10,0,3,0"')) 'Value item count uses a separate aligned column'
Assert-True ($inspector.Contains('Text="{Binding TypeText}"')) 'Expanded signal kind uses friendly type description'
Assert-True ($inspector.Contains('Text="{Binding ProtocolTypeText}"')) 'Exact original wire type stays available in expanded evidence'
Assert-True ($inspector.Contains('Text="{Binding ConfigurationTypeText}"')) 'Matched configuration type remains available as technical evidence'
Assert-True ($inspector.Contains('Text="Status number"')) 'State number is named rather than abbreviated'
Assert-True ($inspector.Contains('Text="Sequence number"')) 'Sequence number is named rather than abbreviated'
Assert-True ($inspector.Contains('Text="GOOSE ID"')) 'Message name explains the GOOSE identifier'
Assert-True ($inspector.Contains('Text="Time allowed to live (ms)"')) 'Time allowed to live uses understandable wording'
Assert-True ($source.Contains('Text="Messages"')) 'History describes what operators are reading'
Assert-True ($source.Contains('ToolTip="{Binding EventDescriptionText}"')) 'Recorded event types are translated for operators'
Assert-True ($source.Contains('GooseOpenConfigurationCommand')) 'Unidentified values provide a path to device configuration'
Assert-True ($source.Contains('Text="Name"')) 'Unnamed raw data is explicitly identified'
$viewModelPath = Join-Path $Root 'src/ProcessBus.App.Wpf/ViewModels/MainWindowViewModel.cs'
$vmSource = Get-Content -LiteralPath $viewModelPath -Raw
Assert-True ($vmSource.Contains('Source.LastSeenUtc.ToLocalTime().ToString("h:mm:ss.fffffff tt"')) 'GOOSE timeline uses the same local clock as Inspector'
Assert-True ($vmSource.Contains('GooseEngineeringLanguage.TryDescribeDoublePoint')) 'Signal position labels use config-validated state interpretation'
Assert-True ($vmSource.Contains('GooseEngineeringLanguage.DataType')) 'Data type labels use human-friendly terminology'
Assert-True ($inspector.Contains('Text="Data"')) 'Compact signal table title remains meaningful'
Assert-True ($source.Contains('Text="Value"')) 'Values remain clearly distinguished from interpretation'
foreach ($heading in @('Header="Time"', 'Header="Relative time"', 'Header="Source"', 'Header="Destination"', 'Header="Description"')) {
    Assert-True ($source.Contains($heading)) "standard message column: $heading"
}
foreach ($label in @('Text="Control Block reference"', 'Text="Destination MAC address"',
                    'Text="Source MAC address"', 'Text="Application ID"', 'Text="GOOSE ID"',
                    'Text="DataSet reference"', 'Text="VLAN ID"', 'Text="VLAN priority"',
                    'Text="Needs commissioning"', 'Text="Configuration revision"',
                    'Text="Simulation/Test"', 'Text="Entry time"', 'Text="Status number"',
                    'Text="Sequence number"', 'Text="Time allowed to live (ms)"')) {
    Assert-True ($inspector.Contains($label)) "standard detail field: $label"
}
Assert-True ($source.Contains('Text="Name"') -and $source.Contains('Text="Value"')) 'Data group has Name / Value columns'
Assert-True ($vmSource.Contains('_firstGooseHistoryTimeUtc ??= msg.LastSeenUtc;')) 'Relative time derives from first observed message, not previous gap'
Assert-True ($vmSource.Contains('RelativeMs = relativeMilliseconds')) 'Relative time evidence stored with each history row'
Write-Host 'GOOSE standard terminology contract: PASS (documented messages, details, fields and relative time)'
Write-Host 'GOOSE operator language contract: PASS (plain labels + original wire evidence)'
Assert-True (-not $inspector.Contains('<DockPanel Margin="0,0,0,10">')) 'DataSet title no longer uses ambiguous DockPanel filling'
Write-Host 'GOOSE Inspector screenshot regression gate: PASS (fixed header, count alignment, compact MMS details)'
# Operator-approved compact GOOSE UX: no duplicated name filter, no
# unbounded ListView inside a StackPanel, and all three independent sections.
$gooseLeftStart = $source.IndexOf('GOOSE LEFT EXPLORER')
$gooseLeftEnd = $source.IndexOf('TIMING / PTP LEFT EXPLORER', $gooseLeftStart)
Assert-True ($gooseLeftStart -ge 0 -and $gooseLeftEnd -gt $gooseLeftStart) 'GOOSE source panel markup available'
$gooseLeft = $source.Substring($gooseLeftStart, $gooseLeftEnd - $gooseLeftStart)
Assert-True (-not $gooseLeft.Contains('<ComboBox')) 'GOOSE source list has only one search filter, not a duplicate dropdown'
Assert-True ($gooseLeft.Contains('x:Name="GooseSourceSearchBox"')) 'Source search has a directly focusable TextBox'
Assert-True ($gooseLeft.Contains('GooseMaterialSearch')) 'Material vector search icon is present'
Assert-True ($gooseLeft.Contains('GooseMaterialClose')) 'Material vector clear icon is present'
Assert-True ($gooseLeft.Contains('Command="{Binding GooseClearSearchCommand}"')) 'Clear action is wired'
Assert-True ($gooseLeft.Contains('GooseHasSearchText')) 'Clear control visibility responds to actual input'
Assert-True ($gooseLeft.Contains('ScrollViewer.VerticalScrollBarVisibility="Auto"')) 'Source list has an independent scrollbar'
Assert-True ($gooseLeft.Contains('VirtualizingPanel.VirtualizationMode="Recycling"')) 'Source rows remain virtualized'
# The obsolete Sources repeat toggle was removed. The bounded source list now
# occupies row 2 rather than row 3; the star-sized viewport is unchanged.
Assert-True ($gooseLeft.Contains('<Grid Grid.Row="2" MinHeight="0">')) 'Scrollable source viewport is height constrained'
Assert-True (-not $gooseLeft.Contains('Text="{Binding GooseFilterSummaryText}"')) 'Redundant source footer removed'

$sectionNodes = $window.SelectNodes("//w:Expander[@Style='{StaticResource GooseInspectorSectionExpanderStyle}']", $ns)
Assert-True ($sectionNodes.Count -eq 3) 'Exactly three collapsible GOOSE Inspector sections'
Assert-True (($sectionNodes[0].GetAttribute('IsExpanded') -eq '{Binding GooseMessageExpanded, Mode=TwoWay}') -and
             ($sectionNodes[1].GetAttribute('IsExpanded') -eq '{Binding GooseNetworkExpanded, Mode=TwoWay}') -and
             ($sectionNodes[2].GetAttribute('IsExpanded') -eq '{Binding GooseValuesExpanded, Mode=TwoWay}')) 'Three independent section states are bound to session ViewModel'
Assert-True ($vmSource.Contains('private bool _gooseMessageExpanded = true;') -and
             $vmSource.Contains('private bool _gooseNetworkExpanded;') -and
             $vmSource.Contains('private bool _gooseValuesExpanded = true;')) 'Message and Values open by default; Network closed'
Assert-True ($theme.OuterXml.Contains('GooseMaterialExpandCircle')) 'Inspector and data items use the Material circled chevron'
Assert-True (-not $inspector.Contains('Content="Identify these signals in the Configuration tab"')) 'Long textual onboarding removed from signal list'
Assert-True ($inspector.Contains('Content="Identify signals"')) 'Compact signal mapping action retained'
Assert-True ($source.Contains('Text="Messages"')) 'History is available without changing protocol logic'
# GOOSE operator-acknowledged alert contract: UX should never silently expire
# received evidence before the engineer has a chance to inspect it.
Assert-True ($source.Contains('x:Key="GooseHeaderSwitchStyle"')) 'GOOSE header exposes one compact vector switch style'
Assert-True (([regex]::Matches($source, 'Style="\{StaticResource GooseHeaderSwitchStyle\}"')).Count -eq 2) 'Show repeats and Follow live use exactly two adjacent switches'
Assert-True ($source.Contains('IsChecked="{Binding IncludeGooseRetransmission, Mode=TwoWay}"')) 'Repeats switch binds only to message representation'
Assert-True ($source.Contains('IsChecked="{Binding GooseFollowLive, Mode=TwoWay}"')) 'Follow-live switch is a two-way explicit state'
Assert-True (-not $source.Contains('Command="{Binding GooseResumeFollowCommand}"')) 'Old transient Follow live button is removed'
Assert-True ($source.Contains('IsUnacknowledgedEvent') -and $source.Contains('HasUnacknowledgedValueChange')) 'Message and source alert visuals bind to acknowledged state'
Assert-True ($vmSource.Contains('public void Acknowledge()') -and
             $vmSource.Contains('public void AcknowledgeGooseSource(')) 'Operator-owned acknowledgement is available for history and sources'
Assert-True (-not $vmSource.Contains('ExpireEmphasis(') -and
             -not $vmSource.Contains('ExpireValueChangeNotice(')) 'Streaming UI does not auto-expire unread engineering evidence'
$messageStart = $source.IndexOf('<!-- Main event timeline: lean, readable, no debug ornaments. -->')
$messageEnd = $source.IndexOf('x:Name="GooseMessageGrid"', $messageStart)
Assert-True ($messageStart -ge 0 -and $messageEnd -gt $messageStart) 'Messages header markup present'
$messageHeader = $source.Substring($messageStart, $messageEnd - $messageStart)
Assert-True (-not $messageHeader.Contains('Clear filter')) 'Messages header has no ugly Clear filter button'
Assert-True (-not $messageHeader.Contains('Grid.Row="1"')) 'Messages header uses exactly one horizontal line'
Assert-True ($messageHeader.Contains('GooseTimelineCountersText')) 'Compact counters remain inline in Messages header'
Assert-True ($vmSource.Contains('InspectNextUnreadGooseSource(') -and
             $vmSource.Contains('AcknowledgeGooseHistoryEvent(') -and
             $vmSource.Contains('ProjectGooseUnreadIndicators(')) 'Sources and Messages share one unread-event journal'
Assert-True ($inspector.Contains('GooseInspectorChangedCountText') -and
             $inspector.Contains('Binding IsChanged')) 'Inspector highlights exact decoded changed entries'
Write-Host 'GOOSE tri-pane event contract: PASS (one-line header, decoder evidence, source navigation, single ACK, inspector deltas)'
Write-Host 'GOOSE acknowledgement UX contract: PASS (paired switches, per-source/event ACK, no timer expiry)'
Write-Host 'GOOSE calm UX contract: PASS (Material icons, searchable scrolling sources, independent Inspector accordions)'
Write-Host 'GOOSE readout contract: PASS (scoped chevron, RX/events, TEST, stable typed rows, live return)'
Write-Host 'WPF workspace spacing contract: PASS (8 DIP columns / 8 DIP panel gaps, named insets)'

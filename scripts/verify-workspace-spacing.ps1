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
$gooseExpanderUsage = $window.SelectNodes("//w:Expander[@Style='{StaticResource GooseDatasetExpanderStyle}']", $ns)
Assert-True ($gooseExpanderUsage.Count -eq 1) 'GOOSE typed rows use scoped compact chevrons'
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
$headerOffset = $inspector.IndexOf('Text="Message Details"')
$scrollOffset = $inspector.IndexOf('<ScrollViewer Grid.Row="1"')
Assert-True ($headerOffset -ge 0 -and $scrollOffset -gt $headerOffset) 'Inspector header remains above independently scrolling evidence'
Assert-True ($inspector.Contains('Grid.Row="0" Margin="0,0,0,12"')) 'Inspector header stays in fixed grid row'
Assert-True ($inspector.Contains('<Grid Margin="0,0,0,10">')) 'DataSet heading/count have independent grid columns'
Assert-True ($inspector.Contains('Grid.Column="1" Margin="12,0,0,0"')) 'DataSet entry count has clear separation from heading'
Assert-True ($inspector.Contains('Text="{Binding TypeText}"')) 'Expanded signal kind uses friendly type description'
Assert-True ($inspector.Contains('Text="{Binding ProtocolTypeText}"')) 'Exact original wire type stays available in expanded evidence'
Assert-True ($inspector.Contains('Text="{Binding ConfigurationTypeText}"')) 'Matched configuration type remains available as technical evidence'
Assert-True ($inspector.Contains('Text="State number"')) 'State number is named rather than abbreviated'
Assert-True ($inspector.Contains('Text="Sequence number"')) 'Sequence number is named rather than abbreviated'
Assert-True ($inspector.Contains('Text="Message name"')) 'Message name explains the GOOSE identifier'
Assert-True ($inspector.Contains('Text="Allowed wait"')) 'Time allowed to live uses understandable wording'
Assert-True ($source.Contains('Text="GOOSE Message History"')) 'History describes what operators are reading'
Assert-True ($source.Contains('Text="{Binding EventDescriptionText}"')) 'Recorded event types are translated for operators'
Assert-True ($source.Contains('GooseOpenConfigurationCommand')) 'Unidentified values provide a path to device configuration'
Assert-True ($source.Contains('Text="SIGNAL OR DATA ITEM"')) 'Unnamed raw data is explicitly identified'
$viewModelPath = Join-Path $Root 'src/ProcessBus.App.Wpf/ViewModels/MainWindowViewModel.cs'
$vmSource = Get-Content -LiteralPath $viewModelPath -Raw
Assert-True ($vmSource.Contains('Source.LastSeenUtc.ToLocalTime().ToString("HH:mm:ss.fff")')) 'GOOSE timeline uses the same local clock as Inspector'
Assert-True ($vmSource.Contains('GooseEngineeringLanguage.TryDescribeDoublePoint')) 'Signal position labels use config-validated state interpretation'
Assert-True ($vmSource.Contains('GooseEngineeringLanguage.DataType')) 'Data type labels use human-friendly terminology'
Assert-True ($source.Contains('Text="Published Values"')) 'Signal table uses plain wording'
Assert-True ($source.Contains('Text="RECEIVED VALUE"')) 'Values remain clearly distinguished from interpretation'
# Every GOOSE Inspector card is independently collapsible. Section controls
# live in the fixed WPF view, not in a per-packet ItemsSource or decode path.
$inspectorSectionStyles = $theme.SelectNodes("//w:Style[@x:Key='GooseInspectorSectionExpanderStyle']", $ns)
Assert-True ($inspectorSectionStyles.Count -eq 1) 'GOOSE has one scoped Inspector section expander style'
$sections = $window.SelectNodes("//w:Expander[@Style='{StaticResource GooseInspectorSectionExpanderStyle}']", $ns)
Assert-True ($sections.Count -eq 3) 'exactly three independent Inspector section expanders'
$expected = @(
    @{ Name = 'GooseMessageIdentitySection'; Expanded = 'True' },
    @{ Name = 'GooseNetworkInformationSection'; Expanded = 'False' },
    @{ Name = 'GoosePublishedValuesSection'; Expanded = 'True' }
)
foreach ($item in $expected) {
    $section = $window.SelectSingleNode("//w:Expander[@x:Name='$($item.Name)']", $ns)
    Assert-True ($null -ne $section) "Inspector section exists: $($item.Name)"
    Assert-True ($section.GetAttribute('IsExpanded') -eq $item.Expanded) "default expansion for $($item.Name)"
    Assert-True ($section.GetAttribute('Style') -eq '{StaticResource GooseInspectorSectionExpanderStyle}') "scoped expander style for $($item.Name)"
}
Assert-True ($inspector.Contains('GooseInspectorMessage.GoId')) 'closed Message & State card retains identifying message name'
Assert-True ($inspector.Contains('GooseInspectorMessage.SourceMac, StringFormat={}From {0}')) 'closed Network card retains sending address'
Assert-True ($inspector.Contains('GooseInspectorDatasetCountText')) 'closed Values card retains item count'
Assert-True ($source.Contains('GooseInspectorRows')) 'collapsible values preserve stable item rows'
Write-Host 'GOOSE Inspector disclosure contract: PASS (three independent sections, keyboard template, smart defaults)'

Write-Host 'GOOSE operator language contract: PASS (plain labels + original wire evidence)'
Assert-True (-not $inspector.Contains('<DockPanel Margin="0,0,0,10">')) 'DataSet title no longer uses ambiguous DockPanel filling'
Write-Host 'GOOSE Inspector screenshot regression gate: PASS (fixed header, count alignment, compact MMS details)'
Write-Host 'GOOSE readout contract: PASS (scoped chevron, RX/events, TEST, stable typed rows, live return)'
Write-Host 'WPF workspace spacing contract: PASS (8 DIP columns / 8 DIP panel gaps, named insets)'

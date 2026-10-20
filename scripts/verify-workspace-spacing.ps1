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
Assert-True (Has-Definitions 'Grid.ColumnDefinitions' 'ColumnDefinition' 'Width' '280,8,*,8,320') 'shell columns must have 8 DIP gutters'
Assert-True (Has-Definitions 'Grid.ColumnDefinitions' 'ColumnDefinition' 'Width' '*,8,320') 'Analyzer waveform/inspector gutter'
Assert-True (Has-Definitions 'Grid.RowDefinitions' 'RowDefinition' 'Height' '0.82*,8,1.18*') 'Analyzer phasor/measurement gap'

# All five customer-facing workspaces share the same horizontal or vertical rhythm.
Assert-True (Has-Definitions 'Grid.RowDefinitions' 'RowDefinition' 'Height' 'Auto,8,Auto,8,Auto,Auto,*') 'Diagnostics primary vertical gutters'
Assert-True (Has-Definitions 'Grid.ColumnDefinitions' 'ColumnDefinition' 'Width' '*,8,400') 'GOOSE timeline/inspector gutter'
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
foreach ($name in @('WorkspacePanelInset', 'WorkspacePanelGapBelow', 'WorkspaceHeroInset')) {
    Assert-True ($source.Contains("{StaticResource $name}")) "workspace must use spacing token $name"
}
Write-Host 'WPF workspace spacing contract: PASS (8 DIP columns / 8 DIP panel gaps, named insets)'

# Usage: pwsh check-observability.ps1 [-KeyVaultName <name>] [-LookbackHours <n>]

param(
    [string]$KeyVaultName = $env:KV_NAME,
    [string]$SubscriptionId,
    [string]$ResourceGroupName,
    [int]$LookbackHours = 24,
    [string]$Namespace = 'longevity',
    [string]$WorkspaceName = 'longevity-workspace',
    [string]$AppInsightsName = 'longevity-appinsights',
    [string]$WorkbookDisplayName = 'Longevity Workbook',
    [string]$StorageAccountName = 'longevityphotos',
    [int]$MaxApplicationErrors = 10,
    [int]$MaxIngress5xx = 3,
    [double]$MaxIngress5xxRatePct = 5,
    [int]$MaxKubeWarnings = 0,
    [int]$MaxStorageErrors = 0,
    [int]$MaxWafDetections = 20,
    [int]$MaxWafDistinctIps = 5,
    [switch]$AsJson,
    [switch]$OpenPortal
)

$ErrorActionPreference = 'Stop'
$ScriptsDir = Resolve-Path "$PSScriptRoot/.."
$ParamsFile = Resolve-Path "$ScriptsDir/../azure/main.parameters.json"
$MainParameters = Get-Content -Raw $ParamsFile | ConvertFrom-Json

function Get-ParameterValue {
    param([string]$Name)

    $parameter = $MainParameters.parameters.$Name
    if ($null -eq $parameter) {
        return $null
    }

    $parameter.value
}

$Config = $null
$UsedKeyVault = $false
$UsedFallback = $false

if (-not [string]::IsNullOrWhiteSpace($KeyVaultName)) {
    try {
        $Config = & "$ScriptsDir/lib/get-config.ps1" -KeyVaultName $KeyVaultName
        $UsedKeyVault = $true
    }
    catch {
        $UsedFallback = $true
    }
}

if ($Config -and [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    $SubscriptionId = $Config.subscriptionId
}

if ($Config -and [string]::IsNullOrWhiteSpace($ResourceGroupName)) {
    $ResourceGroupName = $Config.rgName
}

if ([string]::IsNullOrWhiteSpace($SubscriptionId)) {
    $SubscriptionId = & az account show --query id -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($SubscriptionId)) {
        throw 'Unable to resolve the Azure subscription id. Run az login or '
            + 'pass -SubscriptionId explicitly.'
    }
    $UsedFallback = $true
}

if ([string]::IsNullOrWhiteSpace($ResourceGroupName)) {
    $ResourceGroupName = Get-ParameterValue 'rgName'
    $UsedFallback = $true
}

if (-not $PSBoundParameters.ContainsKey('StorageAccountName')) {
    $storageFromParams = Get-ParameterValue 'storageAccountName'
    if (-not [string]::IsNullOrWhiteSpace($storageFromParams)) {
        $StorageAccountName = $storageFromParams
    }
}

$RgName = $ResourceGroupName
$ConfigSource = if ($UsedKeyVault -and $UsedFallback) {
    'key vault + local fallback'
} elseif ($UsedKeyVault) {
    'key vault'
} else {
    'main.parameters.json + az account'
}

function Invoke-AzJson {
    param([string[]]$Arguments)

    $output = & az @Arguments -o json 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        $message = $output.Trim()
        if ($message -match 'AADSTS|az login') {
            throw (
                'Azure CLI session is expired or invalid. Run az logout, then ' +
                'az login, and retry the observability check.' +
                [Environment]::NewLine + $message
            )
        }

        throw $message
    }

    if ([string]::IsNullOrWhiteSpace($output)) {
        return $null
    }

    $output | ConvertFrom-Json -Depth 100
}

function Convert-LogAnalyticsRows {
    param($Result)

    if ($null -eq $Result) {
        return @()
    }

    # az CLI may return either:
    #   [{name:"PrimaryResult", columns:[...], rows:[...]}]  (flat array, CLI <2.x)
    #   {tables:[{name:"PrimaryResult", columns:[...], rows:[...]}]}  (wrapped, newer)
    $tables = if ($Result -is [System.Array]) {
        @($Result)
    } else {
        @($Result.tables)
    }

    if ($tables.Count -eq 0) {
        return @()
    }

    $table = $tables[0]
    if ($null -eq $table) {
        return @()
    }

    $columns = @($table.columns)
    $rows = if ($null -eq $table.rows) { @() } else { @($table.rows) }

    @(
        foreach ($row in $rows) {
            $record = [ordered]@{}
            for ($index = 0; $index -lt $columns.Count; $index++) {
                $record[$columns[$index].name] = $row[$index]
            }
            [PSCustomObject]$record
        }
    )
}

function Invoke-WorkspaceQuery {
    param([string]$Query)

    $result = Invoke-AzJson @(
        'monitor', 'log-analytics', 'query',
        '--workspace', $WorkspaceQueryId,
        '--analytics-query', $Query,
        '--subscription', $SubscriptionId
    )

    Convert-LogAnalyticsRows $result
}

function Join-Values {
    param($Value)

    if ($null -eq $Value) {
        return ''
    }

    if ($Value -is [string]) {
        return $Value
    }

    if ($Value -is [System.Collections.IEnumerable]) {
        return @($Value)
            | ForEach-Object { "$_".Trim() }
            | Where-Object { $_ }
            | Select-Object -Unique
            | Join-String -Separator ', '
    }

    "$Value"
}

function To-Count {
    param($Value)

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace("$Value")) {
        return 0
    }

    [int][double]$Value
}

function To-Rate {
    param($Value)

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace("$Value")) {
        return 0.0
    }

    [double]$Value
}

function Add-Check {
    param(
        [string]$Check,
        [string]$Status,
        [string]$Observed,
        [string]$Threshold,
        [string]$Note
    )

    [PSCustomObject]@{
        Check = $Check
        Status = $Status
        Observed = $Observed
        Threshold = $Threshold
        Note = $Note
    }
}

function Get-FirstRow {
    param(
        [string]$CheckName,
        [string]$Query
    )

    try {
        Invoke-WorkspaceQuery $Query | Select-Object -First 1
    }
    catch {
        $Checks.Add(
            (Add-Check `
                -Check $CheckName `
                -Status 'critical' `
                -Observed 'query failed' `
                -Threshold 'n/a' `
                -Note $_.Exception.Message)
        )
        $null
    }
}

$Workspace = Invoke-AzJson @(
    'resource', 'show',
    '--resource-group', $RgName,
    '--name', $WorkspaceName,
    '--resource-type', 'Microsoft.OperationalInsights/workspaces',
    '--subscription', $SubscriptionId
)

$WorkspaceQueryId = $Workspace.properties.customerId
if ([string]::IsNullOrWhiteSpace($WorkspaceQueryId)) {
    $WorkspaceQueryId = Invoke-AzJson @(
        'monitor', 'log-analytics', 'workspace', 'show',
        '--resource-group', $RgName,
        '--workspace-name', $WorkspaceName,
        '--subscription', $SubscriptionId,
        '--query', 'customerId'
    )
}

$AppInsights = Invoke-AzJson @(
    'resource', 'show',
    '--resource-group', $RgName,
    '--name', $AppInsightsName,
    '--resource-type', 'Microsoft.Insights/components',
    '--subscription', $SubscriptionId
)

$Workbooks = @(
    Invoke-AzJson @(
        'resource', 'list',
        '--resource-group', $RgName,
        '--resource-type', 'Microsoft.Insights/workbooks',
        '--subscription', $SubscriptionId
    )
)

$Workbook = @(
    $Workbooks
    | Where-Object { $_.properties.displayName -ieq $WorkbookDisplayName }
    | Select-Object -First 1
)

if (-not $Workbook) {
    $Workbook = @($Workbooks | Select-Object -First 1)
}

$Links = [ordered]@{
    Workbook = if ($Workbook) {
        "https://portal.azure.com/#resource$($Workbook.id)/overview"
    } else {
        $null
    }
    LogAnalytics =
        "https://portal.azure.com/#resource$($Workspace.id)/overview"
    AppInsights =
        "https://portal.azure.com/#resource$($AppInsights.id)/overview"
}

if ($OpenPortal) {
    foreach ($url in $Links.Values) {
        if (-not [string]::IsNullOrWhiteSpace($url)) {
            Start-Process $url
        }
    }
}

$Checks = [System.Collections.Generic.List[object]]::new()

$PodHealthQuery = @"
KubePodInventory
| where TimeGenerated > ago(${LookbackHours}h)
| where Namespace == '$Namespace'
| summarize arg_max(TimeGenerated, *) by Name
| where PodStatus !in ('Running', 'Succeeded')
| summarize Count = count(), Pods = make_set(Name, 10),
    Statuses = make_set(PodStatus, 10)
"@

$KubeEventsQuery = @"
KubeEvents
| where TimeGenerated > ago(${LookbackHours}h)
| where Namespace == '$Namespace'
| where Type =~ 'Warning'
| summarize Count = count(), Reasons = make_set(Reason, 10),
    Examples = make_set(substring(Message, 0, 120), 5)
"@

$ApplicationErrorsQuery = @"
ContainerLogV2
| where TimeGenerated > ago(${LookbackHours}h)
| where PodNamespace == '$Namespace'
| where ContainerName in ('photo-api', 'thumbnail-worker')
| extend LogEntry = tostring(LogMessage)
| where LogEntry matches regex @'(?i)\b(error|exception|fail(ed)?|fatal|panic)\b'
| summarize Count = count(), Containers = make_set(ContainerName, 10),
    Examples = make_set(substring(LogEntry, 0, 120), 5)
"@

$Ingress5xxQuery = @"
ContainerLogV2
| where TimeGenerated > ago(${LookbackHours}h)
| extend LogEntry = tostring(LogMessage)
| where LogEntry has 'HTTP/'
| where LogEntry has_any ('GET ', 'POST ', 'PUT ', 'PATCH ', 'DELETE ')
| parse LogEntry with RemoteAddr ' - ' * ' [' * '] "' Method ' ' Path ' ' * '" '
    Status:int ' ' BodyBytes:int ' "' * '" "' * '" ' * ' ' RequestTime
    ' [' Upstream '] ' * ' ' TraceId
| summarize Total = count(), Failures = countif(Status >= 500),
    FailureRatePct = round(
        iff(count() == 0, 0.0,
            todouble(countif(Status >= 500)) * 100.0 / todouble(count())),
        2),
    Paths = make_set_if(Path, Status >= 500, 5)
"@

$WafQuery = @"
ContainerLogV2
| where TimeGenerated > ago(${LookbackHours}h)
| extend LogEntry = tostring(LogMessage)
| where LogEntry has 'ModSecurity'
| parse LogEntry with * '[uri "' Uri '"]' *
| parse LogEntry with * '[client ' ClientIp ']' *
| summarize Count = count(), DistinctIps = dcount(ClientIp),
    Clients = make_set(ClientIp, 10), Uris = make_set(Uri, 5)
"@

$StorageErrorsQuery = @"
union isfuzzy=true
(
    StorageBlobLogs
    | where AccountName == '$StorageAccountName'
    | where TimeGenerated > ago(${LookbackHours}h)
    | where toint(StatusCode) >= 400
    | project ObjectKey
),
(
    StorageQueueLogs
    | where AccountName == '$StorageAccountName'
    | where TimeGenerated > ago(${LookbackHours}h)
    | where toint(StatusCode) >= 400
    | project ObjectKey
)
| summarize Count = count(), Samples = make_set(ObjectKey, 5)
"@

$PodHealth = Get-FirstRow -CheckName 'pod health' -Query $PodHealthQuery
if ($PodHealth) {
    $count = To-Count $PodHealth.Count
    $Checks.Add(
        (Add-Check `
            -Check 'pod health' `
            -Status $(if ($count -gt 0) { 'critical' } else { 'healthy' }) `
            -Observed "$count unhealthy pods" `
            -Threshold '0 unhealthy pods' `
            -Note $(if ($count -gt 0) {
                "Pods: $(Join-Values $PodHealth.Pods); statuses: " +
                "$(Join-Values $PodHealth.Statuses)"
            } else {
                'All latest pods are Running or Succeeded.'
            }))
    )
}

$KubeWarnings = Get-FirstRow -CheckName 'kube warning events' -Query $KubeEventsQuery
if ($KubeWarnings) {
    $count = To-Count $KubeWarnings.Count
    $Checks.Add(
        (Add-Check `
            -Check 'kube warning events' `
            -Status $(if ($count -gt $MaxKubeWarnings) {
                'warning'
            } else {
                'healthy'
            }) `
            -Observed "$count warnings" `
            -Threshold "$MaxKubeWarnings warnings" `
            -Note $(if ($count -gt 0) {
                "Reasons: $(Join-Values $KubeWarnings.Reasons); " +
                "examples: $(Join-Values $KubeWarnings.Examples)"
            } else {
                'No warning Kube events in the lookback window.'
            }))
    )
}

$ApplicationErrors = Get-FirstRow `
    -CheckName 'application error logs' `
    -Query $ApplicationErrorsQuery
if ($ApplicationErrors) {
    $count = To-Count $ApplicationErrors.Count
    $Checks.Add(
        (Add-Check `
            -Check 'application error logs' `
            -Status $(if ($count -gt $MaxApplicationErrors) {
                'warning'
            } else {
                'healthy'
            }) `
            -Observed "$count matching log lines" `
            -Threshold "$MaxApplicationErrors log lines" `
            -Note $(if ($count -gt 0) {
                "Containers: $(Join-Values $ApplicationErrors.Containers); " +
                "examples: $(Join-Values $ApplicationErrors.Examples)"
            } else {
                'No matching error-pattern log lines in app containers.'
            }))
    )
}

$Ingress5xx = Get-FirstRow -CheckName 'ingress 5xx' -Query $Ingress5xxQuery
if ($Ingress5xx) {
    $failures = To-Count $Ingress5xx.Failures
    $rate = [math]::Round((To-Rate $Ingress5xx.FailureRatePct), 2)
    $status = if (
        $failures -gt $MaxIngress5xx -or
        $rate -gt $MaxIngress5xxRatePct
    ) {
        'warning'
    } else {
        'healthy'
    }

    $Checks.Add(
        (Add-Check `
            -Check 'ingress 5xx' `
            -Status $status `
            -Observed "$failures responses ($rate%)" `
            -Threshold "$MaxIngress5xx responses ($MaxIngress5xxRatePct%)" `
            -Note $(if ($failures -gt 0) {
                "Paths: $(Join-Values $Ingress5xx.Paths)"
            } else {
                'No ingress 5xx responses in the lookback window.'
            }))
    )
}

$Waf = Get-FirstRow -CheckName 'waf detections' -Query $WafQuery
if ($Waf) {
    $count = To-Count $Waf.Count
    $distinctIps = To-Count $Waf.DistinctIps
    $status = if (
        $count -gt $MaxWafDetections -or
        $distinctIps -gt $MaxWafDistinctIps
    ) {
        'warning'
    } else {
        'healthy'
    }

    $Checks.Add(
        (Add-Check `
            -Check 'waf detections' `
            -Status $status `
            -Observed "$count detections across $distinctIps IPs" `
            -Threshold "$MaxWafDetections detections / $MaxWafDistinctIps IPs" `
            -Note $(if ($count -gt 0) {
                "IPs: $(Join-Values $Waf.Clients); URIs: $(Join-Values $Waf.Uris)"
            } else {
                'No ModSecurity detections in the lookback window.'
            }))
    )
}

$StorageErrors = Get-FirstRow -CheckName 'storage errors' -Query $StorageErrorsQuery
if ($StorageErrors) {
    $count = To-Count $StorageErrors.Count
    $Checks.Add(
        (Add-Check `
            -Check 'storage errors' `
            -Status $(if ($count -gt $MaxStorageErrors) {
                'critical'
            } else {
                'healthy'
            }) `
            -Observed "$count failing storage operations" `
            -Threshold "$MaxStorageErrors failing operations" `
            -Note $(if ($count -gt 0) {
                "Objects: $(Join-Values $StorageErrors.Samples)"
            } else {
                'No storage blob or queue operations failed in the window.'
            }))
    )
}

$CriticalCount = @($Checks | Where-Object { $_.Status -eq 'critical' }).Count
$WarningCount = @($Checks | Where-Object { $_.Status -eq 'warning' }).Count
$HealthyCount = @($Checks | Where-Object { $_.Status -eq 'healthy' }).Count

$OverallStatus = if ($CriticalCount -gt 0) {
    'critical'
} elseif ($WarningCount -gt 0) {
    'warning'
} else {
    'healthy'
}

$ExitCode = if ($CriticalCount -gt 0) {
    2
} elseif ($WarningCount -gt 0) {
    1
} else {
    0
}

$Report = [PSCustomObject]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    lookbackHours = $LookbackHours
    configSource = $ConfigSource
    subscriptionId = $SubscriptionId
    resourceGroup = $RgName
    overallStatus = $OverallStatus
    links = $Links
    checks = @($Checks)
}

if ($AsJson) {
    $Report | ConvertTo-Json -Depth 10
} else {
    Write-Host "==> Azure observability links" -ForegroundColor Cyan
    foreach ($pair in $Links.GetEnumerator()) {
        $value = if ($pair.Value) { $pair.Value } else { 'not found' }
        Write-Host "  $($pair.Key): $value"
    }

    Write-Host "==> Observability health (last $LookbackHours h)" `
        -ForegroundColor Cyan
    Write-Host "  Config source: $ConfigSource"
    Write-Host "  Overall: $OverallStatus"
    Write-Host "  Healthy: $HealthyCount  Warning: $WarningCount  Critical: $CriticalCount"

    $Checks
    | Sort-Object Status, Check
    | Format-Table Status, Check, Observed, Threshold, Note -Wrap
    | Out-String
    | Write-Host
}

exit $ExitCode
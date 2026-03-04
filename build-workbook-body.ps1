# Usage: pwsh build-workbook-body.ps1 [-ApiConfigFile <path>] [-OutputFile <path>]
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ApiConfigFile = (Join-Path $PSScriptRoot 'config/apis.yaml'),

    [Parameter(Mandatory = $false)]
    [string]$EnvConfigFile = (Join-Path $PSScriptRoot 'config/environments.yaml'),

    [Parameter(Mandatory = $false)]
    [string]$OptionsConfigFile = (Join-Path $PSScriptRoot 'config/options.yaml'),

    [Parameter(Mandatory = $false)]
    [string]$LayoutConfigFile = (Join-Path $PSScriptRoot 'config/layout.yaml'),

    [Parameter(Mandatory = $false)]
    [string]$OutputFile = (Join-Path $PSScriptRoot 'output/workbook-body.json')
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command ConvertFrom-Yaml -ErrorAction SilentlyContinue)) {
    throw 'ConvertFrom-Yaml is not available. Use PowerShell 7+ or install module powershell-yaml.'
}

foreach ($f in @($ApiConfigFile, $EnvConfigFile, $OptionsConfigFile, $LayoutConfigFile)) {
    if (-not (Test-Path $f)) { throw "Config file not found: $f" }
    $ext = [System.IO.Path]::GetExtension($f).ToLowerInvariant()
    if ($ext -ne '.yaml' -and $ext -ne '.yml') {
        throw "Unsupported config extension: $ext. Use .yaml or .yml"
    }
}

$options = Get-Content -Path $OptionsConfigFile -Raw | ConvertFrom-Yaml
$environments = Get-Content -Path $EnvConfigFile -Raw | ConvertFrom-Yaml
$layout = Get-Content -Path $LayoutConfigFile -Raw | ConvertFrom-Yaml

$stepSizeJson = $options.stepSizes | ConvertTo-Json -Depth 3

$envLookup = @{}
$environments | ForEach-Object {
    $sub = $_.subscriptionId
    $rg = $_.resourceGroup
    $tenant = $_.tenant
    $apim = $_.name
    $arm = "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.ApiManagement/service/$apim"
    $portal = "https://portal.azure.com/#@$tenant/resource$arm/apim-apis"
    $envLookup[$apim] = @{
        subscriptionId = $sub
        resourceGroup  = $rg
        tenant         = $tenant
        armPath        = $arm
        portalUrl      = $portal
    }
}

$queryTemplateCache = @{}
function Get-QueryTemplate([string]$RelPath) {
    if (-not $queryTemplateCache.ContainsKey($RelPath)) {
        $full = Join-Path $PSScriptRoot $RelPath
        $queryTemplateCache[$RelPath] = Get-Content -Path $full -Raw
    }
    $queryTemplateCache[$RelPath]
}

function New-ArmQueryJson {
    param([string]$ArmPath, [string]$Api, [hashtable]$ArmDef)

    $cols = $ArmDef.columns | ForEach-Object {
        @{ path = $_.path; columnid = $_.id }
    }
    @{
        version       = 'ARMEndpoint/1.0'
        data          = $null
        headers       = @()
        method        = 'GET'
        path          = "$ArmPath/apis/$Api$($ArmDef.pathSuffix)"
        urlParams     = @(@{ key = 'api-version'; value = $ArmDef.apiVersion })
        resultFormat  = 'table'
        transformers  = @(
            @{
                type     = 'jsonpath'
                settings = @{
                    tablePath = '$.value[*]'
                    columns   = $cols
                }
            }
        )
    } | ConvertTo-Json -Depth 10 -Compress
}

function New-PanelItem {
    param(
        [pscustomobject]$Panel,
        [string]$Apim,
        [string]$Api,
        [hashtable]$Env,
        [string]$PipelineUrl,
        [string]$RepoUrl
    )

    $armPath = $Env.armPath
    $portalUrl = $Env.portalUrl
    $rg = $Env.resourceGroup
    $apiPortalUrl = $portalUrl -replace '/apim-apis$', "/apis/$Api"
    $pipelineLink = if ($PipelineUrl) { " | [Pipeline]($PipelineUrl)" } else { '' }
    $repoLink = if ($RepoUrl) { " | [Repo]($RepoUrl)" } else { '' }

    switch ($Panel.type) {
        'kql' {
            $query = (Get-QueryTemplate $Panel.query).
                Replace('{APIM}', $Apim).Replace('{API}', $Api)
            $content = @{
                version      = 'KqlItem/1.0'
                resourceType = 'microsoft.insights/components'
                showAnalytics = $true
                query        = $query
            }
            if ($Panel.showLegend) {
                $content.chartSettings = @{ showLegend = $true }
            }
            @{
                type        = 3
                content     = $content
                customWidth = $Panel.width
                name        = $Panel.name
            }
        }
        'arm' {
            $armDef = $layout.armQueries[$Panel.armQuery]
            @{
                type    = 3
                content = @{
                    version       = 'KqlItem/1.0'
                    queryType     = 12
                    query         = (New-ArmQueryJson $armPath $Api $armDef)
                    resourceType  = 'microsoft.resources/resources'
                    visualization = 'table'
                }
                customWidth = $Panel.width
                name        = $Panel.name
            }
        }
        'markdown' {
            $tmpl = Get-QueryTemplate $Panel.template
            $md = $tmpl.
                Replace('{API}', $Api).
                Replace('{APIM}', $Apim).
                Replace('{PORTAL_URL}', $portalUrl).
                Replace('{API_PORTAL_URL}', $apiPortalUrl).
                Replace('{PIPELINE_LINK}', $pipelineLink).
                Replace('{REPO_LINK}', $repoLink).
                Replace('{RG}', $rg)
            @{
                type        = 1
                content     = @{ json = $md }
                customWidth = $Panel.width
                name        = $Panel.name
            }
        }
    }
}

function New-SectionItems {
    param(
        [string]$Apim,
        [string]$Api,
        [string]$PipelineUrl,
        [string]$RepoUrl
    )

    $env = $envLookup[$Apim]
    if (-not $env) { throw "No environment config for APIM: $Apim" }

    $layout.sections | ForEach-Object {
        $section = $_
        $panels = $section.panels | ForEach-Object {
            New-PanelItem $_ $Apim $Api $env $PipelineUrl $RepoUrl
        }
        @{
            type    = 12
            content = @{
                version    = 'NotebookGroup/1.0'
                groupType  = 'editable'
                title      = $section.title
                expandable = $section.expandable
                expanded   = $section.expanded
                items      = $panels
            }
            name = $section.name
        }
    }
}

function New-ApiGroup {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$ApiGroup
    )

    @{
        type = 12
        content = @{
            version = 'NotebookGroup/1.0'
            groupType = 'editable'
            title = $ApiGroup.title
            expandable = $true
            expanded = $false
            items = New-SectionItems -Apim $ApiGroup.apim -Api $ApiGroup.api `
                    -PipelineUrl $ApiGroup.pipelineUrl -RepoUrl $ApiGroup.repoUrl
        }
        customWidth = '100'
        name = $ApiGroup.name
        styleSettings = @{ showBorder = $true }
    }
}

$timeRangeValues = $options.timeRangeDurationsMs |
    ForEach-Object { @{ durationMs = $_ } }

$statusCodeJson = $options.statusCodes | ConvertTo-Json -Depth 3 -Compress

$parameterItem = @{
    type = 9
    content = @{
        version = 'KqlParameterItem/1.0'
        parameters = @(
            @{
                id = '8bc0d48d-0567-4399-a98e-57e435638482'
                version = 'KqlParameterItem/1.0'
                isRequired = $true
                name = 'TimeRange'
                type = 4
                description = 'Select the time range for all charts in this workbook'
                typeSettings = @{
                    selectableValues = $timeRangeValues
                    allowCustom = $true
                }
                timeContext = @{ durationMs = 86400000 }
                value = @{ durationMs = $options.defaultTimeRangeMs }
            },
            @{
                id = 'a3bc36d0-6cba-466b-9445-f99e7d5158b9'
                version = 'KqlParameterItem/1.0'
                isRequired = $true
                name = 'StepSize'
                type = 2
                typeSettings = @{ additionalResourceOptions = @() }
                jsonData = $stepSizeJson
                value = $options.defaultStepSize
            },
            @{
                id = 'c8e29f5a-2d1c-4b8f-9e6a-f3b7c8d5e9a2'
                version = 'KqlParameterItem/1.0'
                isRequired = $false
                name = 'StatusCodeFilter'
                label = 'Status Code'
                type = 2
                description = 'Filter charts by specific status code or show all'
                typeSettings = @{
                    additionalResourceOptions = @()
                }
                jsonData = $statusCodeJson
                value = $options.defaultStatusCode
            }
        )
        style = 'pills'
        resourceType = 'microsoft.insights/components'
    }
    name = 'Workbook Filters'
}

$apiGroups = Get-Content -Path $ApiConfigFile -Raw | ConvertFrom-Yaml

$groupItems = $apiGroups | ForEach-Object { New-ApiGroup -ApiGroup $_ }
$allItems = @($parameterItem) + $groupItems

$workbookBody = @{
    version = 'Notebook/1.0'
    items = $allItems
    isLocked = $false
}

$workbookBody |
    ConvertTo-Json -Depth 25 |
    Set-Content -Path $OutputFile -Encoding utf8

Write-Host "Generated workbook body: $OutputFile" -ForegroundColor Green

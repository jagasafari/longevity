# Usage: . lib/ensure-providers.ps1
#        Ensure-Providers -Namespaces @(...) -SubscriptionId <id>

function Ensure-Providers {
    param(
        [string[]]$Namespaces,
        [string]$SubscriptionId,
        [int]$MaxAttempts = 30,
        [int]$PollSeconds = 5
    )

    $Namespaces | ForEach-Object {
        $ns = $_
        $state = az provider show `
            --namespace $ns `
            --subscription $SubscriptionId `
            --query registrationState -o tsv
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to read provider state for $ns"
        }

        if ($state -eq 'Registered') {
            Write-Host "  Provider OK: $ns" `
                -ForegroundColor Green
            return
        }

        Write-Host "  Registering: $ns" `
            -ForegroundColor Cyan
        az provider register `
            --namespace $ns `
            --subscription $SubscriptionId `
            --wait
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to register provider $ns"
        }

        $attempt = 0
        while ($attempt -lt $MaxAttempts) {
            $attempt++
            $state = az provider show `
                --namespace $ns `
                --subscription $SubscriptionId `
                --query registrationState -o tsv
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to verify state for $ns"
            }
            if ($state -eq 'Registered') { break }
            Start-Sleep -Seconds $PollSeconds
        }

        if ($state -ne 'Registered') {
            throw (
                "Provider $ns is $state after " +
                "$MaxAttempts attempts"
            )
        }
        Write-Host "  Provider OK: $ns" `
            -ForegroundColor Green
    }
}

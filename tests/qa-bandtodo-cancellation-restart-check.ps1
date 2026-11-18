#requires -Version 5.1
param(
    [switch]$ImplementationReady
)

if (-not $ImplementationReady) {
    throw 'Run only after the cancellation implementation and additive BandTodoDb migration are ready.'
}

$ErrorActionPreference = 'Stop'
$apiProject = (Resolve-Path (Join-Path $PSScriptRoot '..\src\Todo.API')).Path
$apiDll = (Resolve-Path (Join-Path $apiProject 'bin\Debug\net9.0\Todo.API.dll')).Path
$baseUrl = 'http://127.0.0.1:5087'
$oldConnection = $env:ConnectionStrings__TodoDb
$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$env:ConnectionStrings__TodoDb = 'Server=(localdb)\MSSQLLocalDB;Database=BandTodoDb;Trusted_Connection=True;TrustServerCertificate=True'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$apiProcess = $null
$todoId = $null
$cancelled = $false
$title = 'QA cancellation restart ' + [guid]::NewGuid().ToString('N')
$updatedTitle = $title + ' updated'

function Start-Api {
    if (Get-NetTCPConnection -LocalPort 5087 -State Listen -ErrorAction SilentlyContinue) {
        throw 'Dedicated QA port 5087 is already in use; refusing to contact another process.'
    }
    $process = Start-Process -FilePath 'dotnet' -ArgumentList @("`"$apiDll`"", '--urls', $baseUrl) `
        -WorkingDirectory $apiProject -WindowStyle Hidden -PassThru
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($process.HasExited) { throw "QA API process exited with code $($process.ExitCode)." }
        try {
            $ready = Invoke-WebRequest -Uri "$baseUrl/api/todos" -Method Get -TimeoutSec 2 -UseBasicParsing
            if ($ready.StatusCode -eq 200 -and -not $process.HasExited) { return $process }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
    throw 'QA API did not become ready.'
}

function Get-HttpStatus {
    param([string]$Uri, [string]$Method)
    try {
        $response = Invoke-WebRequest -Uri $Uri -Method $Method -UseBasicParsing
        return [int]$response.StatusCode
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        return [int]$_.Exception.Response.StatusCode
    }
}

try {
    $schema = & sqlcmd -S '(localdb)\MSSQLLocalDB' -d BandTodoDb -E -b -h -1 -W -Q "SELECT CASE WHEN COL_LENGTH('dbo.TodoItems','IsCancelled') IS NOT NULL AND COL_LENGTH('dbo.TodoItems','CancelledAtUtc') IS NOT NULL THEN 1 ELSE 0 END"
    if ($LASTEXITCODE -ne 0 -or -not ($schema -match '^1$')) {
        throw 'BandTodoDb cancellation columns are not present; the additive migration must be applied first.'
    }

    $apiProcess = Start-Api
    $create = Invoke-WebRequest -Uri "$baseUrl/api/todos" -Method Post -ContentType 'application/json' -UseBasicParsing `
        -Body (@{ title = $title; priority = 'High' } | ConvertTo-Json)
    if ($create.StatusCode -ne 201) { throw "Expected 201 create, got $($create.StatusCode)." }
    $created = $create.Content | ConvertFrom-Json
    if ($created.title -ne $title) { throw 'Create response did not match the unique QA title.' }
    $todoId = [guid]$created.id
    Write-Output "Created QA todo: id=$todoId title=$title"

    $update = Invoke-WebRequest -Uri "$baseUrl/api/todos/$todoId" -Method Put -ContentType 'application/json' -UseBasicParsing `
        -Body (@{ title = $updatedTitle; priority = 'High'; isCompleted = $true } | ConvertTo-Json)
    if ($update.StatusCode -ne 200) { throw "Expected 200 update, got $($update.StatusCode)." }
    $updated = $update.Content | ConvertFrom-Json
    if ($updated.id -ne $todoId.ToString() -or $updated.title -ne $updatedTitle -or -not $updated.isCompleted) {
        throw 'Update response did not match the exact QA row.'
    }
    $completedAt = $updated.completedAtUtc

    Stop-Process -Id $apiProcess.Id
    $apiProcess.WaitForExit()
    $apiProcess = $null
    $apiProcess = Start-Api

    $afterRestart = Invoke-WebRequest -Uri "$baseUrl/api/todos/$todoId" -Method Get -UseBasicParsing
    if ($afterRestart.StatusCode -ne 200) { throw "Expected 200 after restart, got $($afterRestart.StatusCode)." }
    $reloaded = $afterRestart.Content | ConvertFrom-Json
    if ($reloaded.id -ne $todoId.ToString() -or $reloaded.title -ne $updatedTitle -or -not $reloaded.isCompleted) {
        throw 'The same QA row was not persisted after API restart.'
    }

    $cancelStatus = Get-HttpStatus -Uri "$baseUrl/api/todos/$todoId" -Method Delete
    if ($cancelStatus -ne 204) { throw "Expected 204 cancellation, got $cancelStatus." }
    $cancelled = $true
    $readCancelled = Invoke-RestMethod -Uri "$baseUrl/api/todos/$todoId" -Method Get
    if (-not $readCancelled.isCancelled -or $readCancelled.id -ne $todoId.ToString() -or
        $readCancelled.title -ne $updatedTitle -or $readCancelled.completedAtUtc -ne $completedAt -or
        -not $readCancelled.cancelledAtUtc.EndsWith('Z')) {
        throw 'Cancelled QA row lost its identity, prior fields, or UTC cancellation timestamp.'
    }
    $cancelledList = Invoke-RestMethod -Uri "$baseUrl/api/todos?isCancelled=true" -Method Get
    if (-not @($cancelledList | Where-Object { $_.id -eq $todoId.ToString() }).Count) {
        throw 'The exact QA row was not visible in the cancelled filter.'
    }
    $defaultList = Invoke-RestMethod -Uri "$baseUrl/api/todos" -Method Get
    if (@($defaultList | Where-Object { $_.id -eq $todoId.ToString() }).Count) {
        throw 'Cancelled QA row remained visible in the default list.'
    }

    $sqlRow = & sqlcmd -S '(localdb)\MSSQLLocalDB' -d BandTodoDb -E -b -h -1 -W -Q "SELECT CONVERT(varchar(36), Id) + '|' + CONVERT(varchar(1), IsCancelled) + '|' + CONVERT(varchar(33), CancelledAtUtc, 126) FROM dbo.TodoItems WHERE Id = '$todoId'"
    if ($LASTEXITCODE -ne 0 -or -not ($sqlRow -match "^$todoId\|1\|")) {
        throw "Exact QA row $todoId was not preserved as cancelled in BandTodoDb."
    }
    Write-Output "PASS: id=$todoId survived API restart and remains cancelled in BandTodoDb; cancelledAtUtc=$($readCancelled.cancelledAtUtc)"
}
finally {
    if ($null -ne $todoId -and -not $cancelled) {
        try {
            if ($null -eq $apiProcess -or $apiProcess.HasExited) { $apiProcess = Start-Api }
            $status = Get-HttpStatus -Uri "$baseUrl/api/todos/$todoId" -Method Delete
            if ($status -eq 204) { Write-Warning "The QA row was cancelled after a prior check failed: id=$todoId" }
            else { Write-Warning "Manual cancellation check needed for exact QA id=$todoId; HTTP $status" }
        } catch { Write-Warning "Manual cancellation check needed for exact QA id=${todoId}: $_" }
    }
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
    $env:ConnectionStrings__TodoDb = $oldConnection
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
}

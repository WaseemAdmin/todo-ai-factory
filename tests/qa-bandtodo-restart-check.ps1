#requires -Version 5.1
param(
    [switch]$OwnerApproved
)

if (-not $OwnerApproved) {
    throw 'Owner approval is required before writing to BandTodoDb. Do not run this script without it.'
}

$ErrorActionPreference = 'Stop'
$apiProject = (Resolve-Path (Join-Path $PSScriptRoot '..\src\Todo.API')).Path
$apiDll = (Resolve-Path (Join-Path $apiProject 'bin\Debug\net9.0\Todo.API.dll')).Path
$baseUrl = 'http://127.0.0.1:5087'
$connection = 'Server=(localdb)\MSSQLLocalDB;Database=BandTodoDb;Trusted_Connection=True;TrustServerCertificate=True'
$oldConnection = $env:ConnectionStrings__TodoDb
$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$env:ConnectionStrings__TodoDb = $connection
$env:ASPNETCORE_ENVIRONMENT = 'Development'

$firstProcess = $null
$secondProcess = $null
$todoId = $null
$deleted = $false
$title = 'QA restart check ' + [guid]::NewGuid().ToString('N')
$updatedTitle = $title + ' updated'

function Start-Api {
    if (Get-NetTCPConnection -LocalPort 5087 -State Listen -ErrorAction SilentlyContinue) {
        throw 'Dedicated QA port 5087 is already in use; refusing to contact another API process.'
    }
    $process = Start-Process -FilePath 'dotnet' -ArgumentList @("`"$apiDll`"", '--urls', $baseUrl) `
        -WorkingDirectory $apiProject -WindowStyle Hidden -PassThru
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($process.HasExited) { throw "API process exited with code $($process.ExitCode)." }
        try {
            $response = Invoke-WebRequest -Uri "$baseUrl/api/todos" -Method Get -TimeoutSec 2 -UseBasicParsing
            if ($response.StatusCode -eq 200) { return $process }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
    throw 'API did not become ready on the dedicated QA port.'
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

function Remove-OwnedTodo {
    if ($null -eq $todoId -or $deleted) { return }
    $status = Get-HttpStatus -Uri "$baseUrl/api/todos/$todoId" -Method Delete
    if ($status -ne 204 -and $status -ne 404) {
        throw "Cleanup of owned todo $todoId failed with HTTP $status."
    }
    $script:deleted = $true
}

try {
    $firstProcess = Start-Api
    $createResponse = Invoke-WebRequest -Uri "$baseUrl/api/todos" -Method Post -ContentType 'application/json' -UseBasicParsing `
        -Body (@{ title = $title; priority = 'Normal' } | ConvertTo-Json)
    if ($createResponse.StatusCode -ne 201) { throw "Expected 201 from create; got $($createResponse.StatusCode)." }
    $created = $createResponse.Content | ConvertFrom-Json
    $todoId = [guid]$created.id
    if ($created.title -ne $title) { throw 'Create response title did not match the QA title.' }
    Write-Output "Created QA todo: title=$title id=$todoId"

    $updateResponse = Invoke-WebRequest -Uri "$baseUrl/api/todos/$todoId" -Method Put -ContentType 'application/json' -UseBasicParsing `
        -Body (@{ title = $updatedTitle; isCompleted = $true } | ConvertTo-Json)
    if ($updateResponse.StatusCode -ne 200) { throw "Expected 200 from update; got $($updateResponse.StatusCode)." }
    $updated = $updateResponse.Content | ConvertFrom-Json
    if ($updated.id -ne $todoId.ToString() -or $updated.title -ne $updatedTitle -or -not $updated.isCompleted) {
        throw 'Update response did not match the exact QA todo.'
    }

    Stop-Process -Id $firstProcess.Id
    $firstProcess.WaitForExit()
    $firstProcess = $null
    $secondProcess = Start-Api

    $reloadResponse = Invoke-WebRequest -Uri "$baseUrl/api/todos/$todoId" -Method Get -UseBasicParsing
    if ($reloadResponse.StatusCode -ne 200) { throw "Expected 200 after restart; got $($reloadResponse.StatusCode)." }
    $reloaded = $reloadResponse.Content | ConvertFrom-Json
    if ($reloaded.id -ne $todoId.ToString() -or $reloaded.title -ne $updatedTitle -or -not $reloaded.isCompleted) {
        throw 'The same QA todo was not persisted after API restart.'
    }
    $listed = Invoke-RestMethod -Uri "$baseUrl/api/todos" -Method Get
    if (-not @($listed | Where-Object { $_.id -eq $todoId.ToString() }).Count) {
        throw 'The exact QA todo ID was missing from the list after restart.'
    }

    Remove-OwnedTodo
    $afterDeleteStatus = Get-HttpStatus -Uri "$baseUrl/api/todos/$todoId" -Method Get
    if ($afterDeleteStatus -ne 404) { throw "Expected 404 after deleting QA todo $todoId." }
    Write-Output "PASS: same id=$todoId survived restart, update persisted, and deletion returned 404 on reload."
}
finally {
    if ($null -ne $todoId -and -not $deleted) {
        try {
            if ($null -eq $secondProcess -or $secondProcess.HasExited) { $secondProcess = Start-Api }
            Remove-OwnedTodo
        } catch { Write-Warning "Manual cleanup may be needed for exact QA id=${todoId}: $_" }
    }
    if ($null -ne $firstProcess -and -not $firstProcess.HasExited) { Stop-Process -Id $firstProcess.Id }
    if ($null -ne $secondProcess -and -not $secondProcess.HasExited) { Stop-Process -Id $secondProcess.Id }
    $env:ConnectionStrings__TodoDb = $oldConnection
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
}

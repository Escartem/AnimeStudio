$ErrorActionPreference = 'Stop'

# prepare patcher
dotnet build AnimeStudio.Patcher -c Release -f net10.0
if ($LASTEXITCODE -ne 0) { throw "Patcher build failed" }
$patcher = "AnimeStudio.Patcher\bin\Release\net10.0\AnimeStudio.Patcher.exe"
if (-not (Test-Path $patcher)) { throw "Patcher not found at $patcher" }

function Reset-Dir([string]$path) {
    if (Test-Path $path) {
        try {
            Remove-Item $path -Recurse -Force -ErrorAction Stop
        } catch {
            # Directory may be locked (Explorer preview, running app). Clear contents instead.
            Write-Warning "Could not remove '$path' wholesale; clearing contents. $_"
            Get-ChildItem $path -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    New-Item -ItemType Directory -Force $path | Out-Null
}

function Remove-EmptyDirectories([string]$path) {
    if (-not (Test-Path $path)) { return }

    while ($true) {
        $empty = @(Get-ChildItem $path -Directory -Recurse -Force |
            Where-Object { -not (Get-ChildItem $_.FullName -Force | Select-Object -First 1) })
        if ($empty.Count -eq 0) { break }

        $empty | Remove-Item -Force -ErrorAction SilentlyContinue

        $stuck = @($empty | Where-Object { Test-Path $_.FullName })
        if ($stuck.Count -eq $empty.Count) {
            Write-Warning "Could not remove empty directories: $($stuck.FullName -join ', ')"
            break
        }
    }
}

foreach ($tfm in 'net9.0-windows', 'net10.0-windows') {
    # config
    $outputDir = ".\dist\$tfm"
    $configuration = 'Release'

    # prepare paths
    $guiOut = "AnimeStudio.GUI/bin/$configuration/$tfm"
    $cliOut = "AnimeStudio.CLI/bin/$configuration/$tfm"

    $guiExe = "$guiOut/AnimeStudio.GUI.exe"
    $cliExe = "$cliOut/AnimeStudio.CLI.exe"

    # Force a fresh apphost so we never re-patch an already-patched exe in-place
    # (old patcher stacked bin\ on each run; even fixed patcher needs a clean host once).
    if (Test-Path $cliExe) { Remove-Item $cliExe -Force }
    if (Test-Path $guiExe) { Remove-Item $guiExe -Force }

    # build cli and gui & patch them
    dotnet build AnimeStudio.CLI -c $configuration -f $tfm
    if ($LASTEXITCODE -ne 0) { throw "CLI build failed ($tfm)" }
    & $patcher $cliExe -d bin
    if ($LASTEXITCODE -ne 0) { throw "CLI patch failed ($tfm)" }

    dotnet build AnimeStudio.GUI -c $configuration -f $tfm
    if ($LASTEXITCODE -ne 0) { throw "GUI build failed ($tfm)" }
    & $patcher $guiExe -d bin
    if ($LASTEXITCODE -ne 0) { throw "GUI patch failed ($tfm)" }

    # prepare output dir
    Reset-Dir $outputDir
    New-Item -ItemType Directory -Force "$outputDir/bin" | Out-Null

    # copy to output
    Copy-Item "$cliOut/*" "$outputDir/bin" -Recurse -Force
    Copy-Item "$guiOut/*" "$outputDir/bin" -Recurse -Force

    # move launcher exes next to bin/
    foreach ($exe in 'AnimeStudio.GUI.exe', 'AnimeStudio.CLI.exe') {
        $from = "$outputDir/bin/$exe"
        if (Test-Path $from) {
            Move-Item $from $outputDir -Force
        } else {
            throw "Expected '$from' after copy"
        }
    }
    if (Test-Path "$outputDir/bin/LICENSE") {
        Move-Item "$outputDir/bin/LICENSE" $outputDir -Force
    } elseif (Test-Path ".\LICENSE") {
        Copy-Item ".\LICENSE" $outputDir -Force
    }

    Remove-EmptyDirectories $outputDir

    # sanity: apphost must point at bin\<dll>, not bin\bin\...
    foreach ($exe in 'AnimeStudio.GUI.exe', 'AnimeStudio.CLI.exe') {
        $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path "$outputDir/$exe"))
        $text = [System.Text.Encoding]::UTF8.GetString($bytes)
        if ($text -match 'bin\\bin\\') {
            throw "$exe still embeds a stacked bin\\ path — patcher failed"
        }
        $dllName = [System.IO.Path]::ChangeExtension($exe, '.dll')
        if ($text -notmatch [regex]::Escape("bin\$dllName")) {
            Write-Warning "$exe may not embed expected path bin\$dllName"
        }
    }

    Write-Host "Built $outputDir" -ForegroundColor Green
}

# prepare patcher
dotnet build AnimeStudio.Patcher -c Release -f net10.0
$patcher = "AnimeStudio.Patcher\bin\Release\net10.0\AnimeStudio.Patcher.exe"

foreach ($tfm in 'net10.0-windows') {
    # config
    $outputDir = ".\dist\$tfm"
    $configuration = 'Release'

    # prepare paths
    $guiOut = "AnimeStudio.GUI/bin/$configuration/$tfm"
    $cliOut = "AnimeStudio.CLI/bin/$configuration/$tfm"

    $guiExe = "$guiOut/AnimeStudio.GUI.exe"
    $cliExe = "$cliOut/AnimeStudio.CLI.exe"

    # build cli and gui & patch them
    dotnet build AnimeStudio.CLI -c $configuration -f $tfm
    & $patcher $cliExe -d bin
    dotnet build AnimeStudio.GUI -c $configuration -f $tfm
    & $patcher $guiExe -d bin

    # prepare output dir
    if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force }
    New-Item -ItemType Directory $outputDir
    New-Item -ItemType Directory "$outputDir/bin"

    # copy to output
    Copy-Item "$cliOut/*" "$outputDir/bin" -Recurse
    Copy-Item "$guiOut/*" "$outputDir/bin" -Recurse -Force

    # move files out
    foreach ($exe in 'AnimeStudio.GUI.exe', 'AnimeStudio.CLI.exe') {
        Move-Item "$outputDir/bin/$exe" $outputDir
    }
    Move-Item "$outputDir/bin/LICENSE" $outputDir
}

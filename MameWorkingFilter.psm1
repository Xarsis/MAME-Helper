# MAME Working ROM Filter - Playnite Script Extension

# *** SET YOUR MAME PATH HERE ***
$MameExePath = "S:\MAME\MAME\mame.exe"

function GetMainMenuItems {
    param($menuArgs)

    $menuItem1 = New-Object Playnite.SDK.Plugins.ScriptMainMenuItem
    $menuItem1.Description = "MAME: Tag All Games by Status"
    $menuItem1.FunctionName = "MameFilter_TagByStatus"
    $menuItem1.MenuSection = "@MAME Working Filter"

    $menuItem2 = New-Object Playnite.SDK.Plugins.ScriptMainMenuItem
    $menuItem2.Description = "MAME: Hide Imperfect ROMs (reversible)"
    $menuItem2.FunctionName = "MameFilter_HideImperfect"
    $menuItem2.MenuSection = "@MAME Working Filter"

    $menuItem3 = New-Object Playnite.SDK.Plugins.ScriptMainMenuItem
    $menuItem3.Description = "MAME: Hide Non-Working ROMs (reversible)"
    $menuItem3.FunctionName = "MameFilter_HideNonWorking"
    $menuItem3.MenuSection = "@MAME Working Filter"

    $menuItem4 = New-Object Playnite.SDK.Plugins.ScriptMainMenuItem
    $menuItem4.Description = "MAME: REMOVE Non-Working ROMs (permanent)"
    $menuItem4.FunctionName = "MameFilter_RemoveNonWorking"
    $menuItem4.MenuSection = "@MAME Working Filter"

    $menuItem5 = New-Object Playnite.SDK.Plugins.ScriptMainMenuItem
    $menuItem5.Description = "MAME: Diagnose GameId Format"
    $menuItem5.FunctionName = "MameFilter_DiagnoseGameId"
    $menuItem5.MenuSection = "@MAME Working Filter"

    return $menuItem1, $menuItem2, $menuItem3, $menuItem4, $menuItem5
}

function MameFilter_GetDriverStatus {
    if (-not $MameExePath -or -not (Test-Path $MameExePath)) {
        [void]$PlayniteApi.Dialogs.ShowMessage(
            "Could not find mame.exe at: '$MameExePath'`nPlease update the `$MameExePath variable at the top of the script.",
            "MAME Working ROM Filter"
        )
        return $null
    }

    $tempXml = [System.IO.Path]::GetTempFileName() + ".xml"

    [void]$PlayniteApi.Dialogs.ShowMessage(
        "Generating MAME ROM list...`nThis may take 30-60 seconds. Click OK to start.",
        "MAME Working ROM Filter"
    )

    try {
        [void](Start-Process -FilePath $MameExePath -ArgumentList "-listxml" `
            -RedirectStandardOutput $tempXml -NoNewWindow -PassThru -Wait)
    } catch {
        [void]$PlayniteApi.Dialogs.ShowMessage(
            "Failed to run mame.exe: $_", "MAME Working ROM Filter"
        )
        return $null
    }

    if (-not (Test-Path $tempXml) -or (Get-Item $tempXml).Length -eq 0) {
        [void]$PlayniteApi.Dialogs.ShowMessage(
            "No output from mame -listxml. Check your MAME path.", "MAME Working ROM Filter"
        )
        return $null
    }

    # Build a hashtable keyed by ROM name (lowercase) -> driver status
    $statusMap = @{}
    $reader = $null

    try {
        $settings = New-Object System.Xml.XmlReaderSettings
        $settings.DtdProcessing    = [System.Xml.DtdProcessing]::Parse
        $settings.IgnoreWhitespace = $true
        $settings.IgnoreComments   = $true
        $reader = [System.Xml.XmlReader]::Create($tempXml, $settings)

        $currentMachine = $null
        $isDevice       = $false
        $isBios         = $false

        while ($reader.Read()) {
            if ($reader.NodeType -eq [System.Xml.XmlNodeType]::Element) {
                switch ($reader.Name) {
                    "machine" {
                        $currentMachine = $reader.GetAttribute("name").ToLower()
                        $isDevice       = $reader.GetAttribute("isdevice") -eq "yes"
                        $isBios         = $reader.GetAttribute("isbios")   -eq "yes"
                    }
                    "driver" {
                        if ($currentMachine -and -not $isDevice -and -not $isBios) {
                            $statusMap[$currentMachine] = $reader.GetAttribute("status")
                        }
                        $currentMachine = $null
                    }
                }
            }
        }
    } finally {
        if ($reader) { $reader.Close() }
        Remove-Item $tempXml -ErrorAction SilentlyContinue
    }

    return $statusMap
}

function MameFilter_HideNonWorking {
    param($actionArgs)

    $statusMap = MameFilter_GetDriverStatus
    if (-not $statusMap) { return }

    $hidden  = 0
    $skipped = 0

    foreach ($game in $PlayniteApi.Database.Games) {
        $key = $game.Name.ToLower().Trim()
        if ($statusMap.ContainsKey($key)) {
            if ($statusMap[$key] -eq "preliminary") {
                $game.Hidden = $true
                $PlayniteApi.Database.Games.Update($game)
                $hidden++
            }
        } else {
            $skipped++
        }
    }

    [void]$PlayniteApi.Dialogs.ShowMessage(
        "Done!`nHidden (non-working): $hidden`nNo MAME match found: $skipped",
        "MAME Working ROM Filter"
    )
}

function MameFilter_HideImperfect {
    param($actionArgs)

    $statusMap = MameFilter_GetDriverStatus
    if (-not $statusMap) { return }

    $hidden  = 0
    $skipped = 0

    foreach ($game in $PlayniteApi.Database.Games) {
        $key = $game.Name.ToLower().Trim()
        if ($statusMap.ContainsKey($key)) {
            if ($statusMap[$key] -eq "imperfect") {
                $game.Hidden = $true
                $PlayniteApi.Database.Games.Update($game)
                $hidden++
            }
        } else {
            $skipped++
        }
    }

    [void]$PlayniteApi.Dialogs.ShowMessage(
        "Done!`nHidden (imperfect): $hidden`nNo MAME match found: $skipped",
        "MAME Working ROM Filter"
    )
}

function MameFilter_RemoveNonWorking {
    param($actionArgs)

    $confirm = $PlayniteApi.Dialogs.ShowMessage(
        "This will PERMANENTLY REMOVE all non-working MAME ROMs from your Playnite library.`nThis cannot be undone. Continue?",
        "MAME Working ROM Filter", "YesNo"
    )
    if ($confirm -ne "Yes") { return }

    $statusMap = MameFilter_GetDriverStatus
    if (-not $statusMap) { return }

    $toRemove = New-Object System.Collections.Generic.List[System.Guid]

    foreach ($game in $PlayniteApi.Database.Games) {
        $key = $game.Name.ToLower().Trim()
        if ($statusMap.ContainsKey($key) -and $statusMap[$key] -eq "preliminary") {
            $toRemove.Add($game.Id)
        }
    }

    $PlayniteApi.Database.Games.Remove($toRemove)

    [void]$PlayniteApi.Dialogs.ShowMessage(
        "Removed $($toRemove.Count) non-working MAME ROMs.", "MAME Working ROM Filter"
    )
}

function MameFilter_TagByStatus {
    param($actionArgs)

    $statusMap = MameFilter_GetDriverStatus
    if (-not $statusMap) { return }

    $tagGood      = $PlayniteApi.Database.Tags.Add("MAME: Working")
    $tagImperfect = $PlayniteApi.Database.Tags.Add("MAME: Imperfect")
    $tagPrelim    = $PlayniteApi.Database.Tags.Add("MAME: Non-Working")

    $count = 0
    foreach ($game in $PlayniteApi.Database.Games) {
        $key = $game.Name.ToLower().Trim()
        if (-not $statusMap.ContainsKey($key)) { continue }

        if ($null -eq $game.TagIds) {
            $game.TagIds = New-Object System.Collections.Generic.List[System.Guid]
        }

        switch ($statusMap[$key]) {
            "good"        { if ($game.TagIds -notcontains $tagGood.Id)      { $game.TagIds.Add($tagGood.Id) } }
            "imperfect"   { if ($game.TagIds -notcontains $tagImperfect.Id) { $game.TagIds.Add($tagImperfect.Id) } }
            "preliminary" { if ($game.TagIds -notcontains $tagPrelim.Id)    { $game.TagIds.Add($tagPrelim.Id) } }
        }

        $PlayniteApi.Database.Games.Update($game)
        $count++
    }

    [void]$PlayniteApi.Dialogs.ShowMessage(
        "Tagged $count MAME games by driver status.", "MAME Working ROM Filter"
    )
}

function MameFilter_DiagnoseGameId {
    param($actionArgs)

    $lines = @()
    $count = 0
    foreach ($game in $PlayniteApi.Database.Games) {
        if ($count -ge 10) { break }
        $src = if ($game.Source) { $game.Source.Name } else { "(no source)" }
        $lines += "Name      : $($game.Name)"
        $lines += "GameId    : $($game.GameId)"
        $lines += "Source    : $src"
        $lines += "ImagePath : $($game.GameImagePath)"
        $lines += "---"
        $count++
    }

    $outFile = "$env:USERPROFILE\Desktop\MameFilter_Diagnose.txt"
    $lines | Out-File -FilePath $outFile -Encoding utf8

    $npp = "C:\Program Files\Notepad++\notepad++.exe"
    if (Test-Path $npp) {
        $cmd = "`"$npp`" `"$outFile`""
        Start-Process -FilePath "cmd.exe" -ArgumentList "/c", $cmd -WindowStyle Hidden
    } else {
        [void]$PlayniteApi.Dialogs.ShowMessage(
            "Notepad++ not found at: $npp`nDiagnostic file written to:`n$outFile",
            "MAME Working ROM Filter"
        )
    }
}

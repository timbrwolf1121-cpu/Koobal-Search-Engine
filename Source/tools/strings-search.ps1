$bytes = [IO.File]::ReadAllBytes('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$text = [Text.Encoding]::ASCII.GetString($bytes)
$patterns = @('SearchField_OnValueChange','searchTimer','UpdateDaemon','SearchStart','onValueChanged')
foreach ($p in $patterns) {
    $idx = 0; $count = 0
    while (($idx = $text.IndexOf($p, $idx)) -ge 0 -and $count -lt 3) {
        $start = [Math]::Max(0, $idx - 40)
        $len = [Math]::Min(120, $text.Length - $start)
        Write-Output "--- $p @ $idx ---"
        Write-Output $text.Substring($start, $len).Replace("`0",' ')
        $idx++; $count++
    }
}

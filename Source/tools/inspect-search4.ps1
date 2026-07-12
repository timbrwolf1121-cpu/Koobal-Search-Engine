Add-Type -Path 'F:\SteamLibrary\steamapps\common\Kerbal Space Program\GameData\000_Harmony\0Harmony.dll' -ErrorAction SilentlyContinue
$asm = [Reflection.Assembly]::LoadFrom('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$pc = $asm.GetType('KSP.UI.Screens.PartCategorizer')
foreach ($m in $pc.GetMethods([Reflection.BindingFlags]'Instance,NonPublic,Public,DeclaredOnly')) {
    Write-Output $m.ToString()
}
Write-Output '--- BasePartCategorizer search-related ---'
$bpc = $asm.GetType('KSP.UI.Screens.BasePartCategorizer')
foreach ($m in $bpc.GetMethods([Reflection.BindingFlags]'Instance,NonPublic,Public')) {
    if ($m.Name -match 'Search|search|Filter|Daemon|Update') { Write-Output $m.ToString() }
}

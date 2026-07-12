$asm = [Reflection.Assembly]::LoadFrom('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$bpc = $asm.GetType('KSP.UI.Screens.BasePartCategorizer')
foreach ($n in @('SearchField_OnValueChange','SearchStart','SearchRoutine','Update')) {
    $m = $bpc.GetMethod($n, [Reflection.BindingFlags]'Instance,NonPublic,Public')
    if ($m) { Write-Output "$n`: $($m.ToString())" } else { Write-Output "$n`: missing" }
}
$pc = $asm.GetType('KSP.UI.Screens.PartCategorizer')
$epl = $asm.GetType('KSP.UI.Screens.EditorPartList')
foreach ($m in $epl.GetMethods([Reflection.BindingFlags]'Instance,NonPublic,Public')) {
    if ($m.Name -eq 'Refresh') { Write-Output "Refresh: $($m.ToString())" }
}
$cat = $pc.GetNestedType('Category', [Reflection.BindingFlags]'NonPublic')
foreach ($m in $cat.GetMethods([Reflection.BindingFlags]'Instance,NonPublic,Public')) {
    if ($m.Name -eq 'AddPart') { Write-Output "AddPart: $($m.ToString())" }
}

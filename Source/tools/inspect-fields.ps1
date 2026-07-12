$asm = [Reflection.Assembly]::LoadFrom('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$pc = $asm.GetType('KSP.UI.Screens.PartCategorizer')
$cat = $pc.GetNestedType('Category', [Reflection.BindingFlags]'NonPublic,Public')
foreach ($m in $cat.GetMethods([Reflection.BindingFlags]'Instance,NonPublic,Public')) {
    if ($m.Name -match 'AddPart|AcceptNew|DeleteSub') { Write-Output $m.ToString() }
}
$bpc = $asm.GetType('KSP.UI.Screens.BasePartCategorizer')
foreach ($f in $bpc.GetFields([Reflection.BindingFlags]'Instance,NonPublic,Public')) {
    if ($f.Name -match 'refresh|search|Search') { Write-Output ("Base." + $f.Name + " : " + $f.FieldType.Name) }
}
foreach ($f in $pc.GetFields([Reflection.BindingFlags]'Instance,NonPublic,Public')) {
    if ($f.Name -match 'refresh|search|Search') { Write-Output ("PC." + $f.Name + " : " + $f.FieldType.Name) }
}

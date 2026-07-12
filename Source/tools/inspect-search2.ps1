$asm = [Reflection.Assembly]::LoadFrom('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$bpc = $asm.GetType('KSP.UI.Screens.BasePartCategorizer')
$pc = $asm.GetType('KSP.UI.Screens.PartCategorizer')
foreach ($n in @('SearchField_OnValueChange','SearchStart','SearchRoutine','SearchStop','SearchFilterResult')) {
    $base = $bpc.GetMethod($n, [Reflection.BindingFlags]'Instance,NonPublic,Public')
    $derived = $pc.GetMethod($n, [Reflection.BindingFlags]'Instance,NonPublic,Public')
    Write-Output "=== $n ==="
    Write-Output "  base: $(if($base){$base.DeclaringType.Name + ' virtual=' + $base.IsVirtual}else{'missing'})"
    Write-Output "  derived: $(if($derived){$derived.DeclaringType.Name + ' virtual=' + $derived.IsVirtual}else{'inherits base'})"
}
$nested = $pc.GetNestedTypes([Reflection.BindingFlags]'NonPublic')
Write-Output "Nested types: $($nested.Name -join ', ')"
$catName = ($nested | Where-Object { $_.Name -eq 'Category' } | Select-Object -First 1)
if ($catName) {
    foreach ($m in $catName.GetMethods([Reflection.BindingFlags]'Instance,NonPublic,Public')) {
        if ($m.Name -eq 'AddPart') { Write-Output "AddPart: $($m.ToString())" }
    }
}

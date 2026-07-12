$asm = [Reflection.Assembly]::LoadFrom('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$pc = $asm.GetType('KSP.UI.Screens.PartCategorizer')
$cat = $pc.GetNestedType('Category',[Reflection.BindingFlags]'NonPublic,Public')
Write-Output '=== Category fields ==='
$cat.GetFields([Reflection.BindingFlags]'Instance,NonPublic,Public') | ForEach-Object { Write-Output ($_.Name + ' : ' + $_.FieldType.Name) }
Write-Output '=== Category OnTrueSUB signature ==='
$m = $cat.GetMethod('OnTrueSUB',[Reflection.BindingFlags]'Instance,NonPublic,Public')
Write-Output $m.ToString()
Write-Output '=== Category OnTrueCATEGORY ==='
$m2 = $cat.GetMethod('OnTrueCATEGORY',[Reflection.BindingFlags]'Instance,NonPublic,Public')
Write-Output $m2.ToString()
Write-Output '=== PartCategorizerButton OnTrue ==='
$pcb = $asm.GetType('KSP.UI.Screens.PartCategorizerButton')
$m3 = $pcb.GetMethod('OnTrue',[Reflection.BindingFlags]'Instance,NonPublic,Public')
Write-Output $m3.ToString()
Write-Output '=== LoadCustomSubassemblyCategories IL (first 40) ==='
$load = $pc.GetMethod('LoadCustomSubassemblyCategories',[Reflection.BindingFlags]'Instance,NonPublic,Public')
if ($load) {
    $body = $load.GetMethodBody()
    $il = $body.GetILAsByteArray()
    # just list string literals via reflection on Cecil alternative - use disassembler
    $module = $load.Module
    $reader = [System.Reflection.MethodBody]
}
Write-Output '=== AddCustomCategorySubassembly ==='
$add = $cat.GetMethod('AddCustomCategorySubassembly',[Reflection.BindingFlags]'Instance,NonPublic,Public')
if ($add) { Write-Output $add.ToString() }
Write-Output '=== AcceptNewSubcategorySubassembly ==='
$acc = $cat.GetMethod('AcceptNewSubcategorySubassembly',[Reflection.BindingFlags]'Instance,NonPublic,Public')
if ($acc) { Write-Output $acc.ToString() }

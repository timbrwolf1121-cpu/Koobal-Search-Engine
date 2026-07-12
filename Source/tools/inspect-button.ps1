$asm = [Reflection.Assembly]::LoadFrom('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$pcb = $asm.GetType('KSP.UI.Screens.PartCategorizerButton')
Write-Output '=== PartCategorizerButton Methods ==='
$pcb.GetMethods([Reflection.BindingFlags]'Instance,NonPublic,Public') | ForEach-Object { Write-Output $_.ToString() }
Write-Output '=== PartCategorizerButton Fields ==='
$pcb.GetFields([Reflection.BindingFlags]'Instance,NonPublic,Public') | ForEach-Object { Write-Output ($_.Name + ' : ' + $_.FieldType.Name) }
$pc = $asm.GetType('KSP.UI.Screens.PartCategorizer')
$cat = $pc.GetNestedType('Category',[Reflection.BindingFlags]'NonPublic,Public')
Write-Output '=== Category OnTrue/Select methods ==='
$cat.GetMethods([Reflection.BindingFlags]'Instance,NonPublic,Public') | Where-Object { $_.Name -match 'OnTrue|Select|Click|Activate' } | ForEach-Object { Write-Output $_.ToString() }
Write-Output '=== PartCategorizer subassembly fields ==='
$pc.GetFields([Reflection.BindingFlags]'Instance,NonPublic,Public') | Where-Object { $_.Name -match 'subassembl|subcategor|filterSub' } | ForEach-Object { Write-Output ($_.Name + ' : ' + $_.FieldType.Name) }
$urb = $asm.GetType('KSP.UI.UIRadioButton')
Write-Output '=== UIRadioButton methods ==='
$urb.GetMethods([Reflection.BindingFlags]'Instance,NonPublic,Public') | Where-Object { $_.Name -match 'SetState|OnClick|Click' } | ForEach-Object { Write-Output $_.ToString() }

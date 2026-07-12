$asm = [Reflection.Assembly]::LoadFrom('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$pc = $asm.GetType('KSP.UI.Screens.PartCategorizer')
$all = $pc.GetNestedTypes([Reflection.BindingFlags]'NonPublic,Public')
Write-Output "All nested ($($all.Count)):"
$all | ForEach-Object { Write-Output "  $($_.Name)" }
$cat = $pc.GetNestedType('Category', [Reflection.BindingFlags]'NonPublic,Public')
Write-Output "Category type: $cat"

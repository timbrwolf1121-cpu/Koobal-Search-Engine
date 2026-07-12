Add-Type -Path 'F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll'
Add-Type -Path 'F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\RUI.dll'

Write-Host '=== IconLoader methods ==='
[RUI.Icons.Selectable.IconLoader].GetMethods([Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly') |
    ForEach-Object { Write-Host $_.ToString() }

Write-Host '=== Icon fields/properties ==='
[RUI.Icons.Selectable.Icon].GetFields([Reflection.BindingFlags]'Public,NonPublic,Instance') |
    ForEach-Object { Write-Host "field: $($_.FieldType.Name) $($_.Name)" }
[RUI.Icons.Selectable.Icon].GetProperties([Reflection.BindingFlags]'Public,NonPublic,Instance') |
    ForEach-Object { Write-Host "prop: $($_.PropertyType.Name) $($_.Name)" }

Write-Host '=== RUI Texture (from iconNormal field type) ==='
$ruiTextureType = [RUI.Icons.Selectable.Icon].GetField('iconNormal').FieldType
Write-Host "type: $($ruiTextureType.FullName)"
$ruiTextureType.GetFields([Reflection.BindingFlags]'Public,NonPublic,Instance') |
    ForEach-Object { Write-Host "field: $($_.FieldType.Name) $($_.Name)" }
$ruiTextureType.GetProperties([Reflection.BindingFlags]'Public,NonPublic,Instance') |
    ForEach-Object { Write-Host "prop: $($_.PropertyType.Name) $($_.Name)" }
Write-Host '=== UnityEngine.Texture has main? ==='
[UnityEngine.Texture].GetProperty('main', [Reflection.BindingFlags]'Public,NonPublic,Instance,Static')
Write-Host '=== Which Texture does C# typeof resolve to in helper? ==='
Write-Host 'RUI Texture full name:' ([RUI.Icons.Selectable.Icon].GetField('iconNormal').FieldType).FullName

Write-Host '=== PartCategorizerButton icon fields ==='
[KSP.UI.Screens.PartCategorizerButton].GetFields([Reflection.BindingFlags]'Public,NonPublic,Instance') |
    Where-Object { $_.Name -match 'icon|sprite|Image' } |
    ForEach-Object { Write-Host "field: $($_.FieldType.Name) $($_.Name)" }

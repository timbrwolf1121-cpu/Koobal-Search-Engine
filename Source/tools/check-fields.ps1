$asm = [System.Reflection.Assembly]::LoadFrom('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$types = $asm.GetTypes() | Where-Object { $_.Name -eq 'PartCategorizer' }
$lines = @('PartCategorizer count: ' + @($types).Count)
if ($types.Count -gt 0) {
    $t = $types[0]
    $lines += 'FullName: ' + $t.FullName
    $cat = $t.GetNestedType('Category', 'Public,NonPublic')
    $lines += 'Category type: ' + ($cat -ne $null)
    if ($cat) {
        $lines += 'button NonPublic: ' + ($null -ne $cat.GetField('button', 'Instance,NonPublic'))
        $lines += 'button Public: ' + ($null -ne $cat.GetField('button', 'Instance,Public'))
        $lines += 'button Both: ' + ($null -ne $cat.GetField('button', 'Instance,Public,NonPublic'))
        foreach ($f in $cat.GetFields('Instance,Public,NonPublic')) {
            if ($f.Name -match 'subcategor|button|parent|available') {
                $lines += ($f.Name + ' public=' + $f.IsPublic)
            }
        }
    }
}
$lines | Set-Content 'F:\SteamLibrary\steamapps\common\Kerbal Space Program\Source\PartSearchSuggest\reflect-out.txt'

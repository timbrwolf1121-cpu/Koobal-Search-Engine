// Temporary reflection probe — delete after use
using System;
using System.Linq;
using System.Reflection;

class ReflectSearch
{
    static void Main()
    {
        var asm = Assembly.LoadFrom(@"F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll");
        var baseCat = asm.GetType("KSP.UI.Screens.BasePartCategorizer");
        var editorList = asm.GetType("KSP.UI.Screens.EditorPartList");
        var filterType = asm.GetType("EditorPartListFilter`1").MakeGenericType(asm.GetType("AvailablePart"));

        Console.WriteLine("=== BasePartCategorizer search methods ===");
        foreach (var m in baseCat.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name.Contains("Search") || m.Name.Contains("PartMatch")))
            Console.WriteLine(m);

        Console.WriteLine("\n=== BasePartCategorizer search fields ===");
        foreach (var f in baseCat.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.Name.ToLower().Contains("search")))
            Console.WriteLine(f.FieldType.Name + " " + f.Name);

        Console.WriteLine("\n=== EditorPartList methods (filter/refresh) ===");
        foreach (var m in editorList.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name.Contains("Filter") || m.Name.Contains("Refresh") || m.Name.Contains("Search") || m.Name.Contains("Rebuild") || m.Name.Contains("Update")))
            Console.WriteLine(m);

        Console.WriteLine("\n=== EditorPartList fields (filter/search) ===");
        foreach (var f in editorList.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.Name.Contains("Filter") || f.Name.Contains("Search") || f.Name.Contains("Part")))
            Console.WriteLine(f.FieldType.Name + " " + f.Name);

        Console.WriteLine("\n=== EditorPartListFilter members ===");
        foreach (var m in filterType.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            Console.WriteLine(m.MemberType + " " + m.Name + (m is MethodInfo mi ? "(" + string.Join(",", mi.GetParameters().Select(p => p.ParameterType.Name)) + ")" : ""));
    }
}

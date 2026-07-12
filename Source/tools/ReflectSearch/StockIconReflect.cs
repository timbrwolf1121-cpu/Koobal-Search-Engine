using System;
using System.Linq;
using Mono.Cecil;

class StockIconReflect
{
    static void Main()
    {
        var asm = AssemblyDefinition.ReadAssembly(
            @"F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll");

        var pc = asm.MainModule.Types.First(t => t.Name == "PartCategorizer");
        Console.WriteLine("=== PartCategorizer fields (icon/category/manufacturer/filter) ===");
        foreach (var f in pc.Fields.OrderBy(x => x.Name))
        {
            string n = f.Name.ToLowerInvariant();
            if (n.Contains("icon") || n.Contains("categor") || n.Contains("manufact")
                || n.Contains("filter") || n.Contains("diameter") || n.Contains("module")
                || n.Contains("resource") || n.Contains("tech") || n.Contains("tag")
                || n.Contains("button") || n.Contains("subcategor"))
            {
                Console.WriteLine(f.FieldType.Name + " " + f.Name);
            }
        }

        var cat = pc.NestedTypes.First(t => t.Name == "Category");
        Console.WriteLine("\n=== Category fields ===");
        foreach (var f in cat.Fields.OrderBy(x => x.Name))
        {
            Console.WriteLine(f.FieldType.Name + " " + f.Name);
        }

        var pcb = asm.MainModule.Types.First(t => t.Name == "PartCategorizerButton");
        Console.WriteLine("\n=== PartCategorizerButton fields ===");
        foreach (var f in pcb.Fields.OrderBy(x => x.Name))
        {
            Console.WriteLine(f.FieldType.Name + " " + f.Name);
        }
        Console.WriteLine("\n=== PartCategorizerButton properties ===");
        foreach (var p in pcb.Properties.OrderBy(x => x.Name))
        {
            Console.WriteLine(p.PropertyType.Name + " " + p.Name);
        }
    }
}

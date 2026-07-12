using System;
using System.Linq;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Metadata;

class P
{
    static void Main()
    {
        string asm = @"F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll";
        var settings = new DecompilerSettings(LanguageVersion.CSharp7_3) { ThrowOnAssemblyResolveErrors = false };
        var decompiler = new CSharpDecompiler(asm, settings);
        var ts = decompiler.TypeSystem;

        // EditorPartList.State enum
        var stateEnum = ts.MainModule.TypeDefinitions.FirstOrDefault(t => t.Name == "State" && t.DeclaringType != null && t.DeclaringType.Name == "EditorPartList");
        Console.WriteLine("=== EditorPartList.State enum ===");
        if (stateEnum != null)
            foreach (var f in stateEnum.Fields.Where(f => f.IsStatic))
                Console.WriteLine("  " + f.Name + " = " + f.GetConstantValue());
        else Console.WriteLine("  (enum not found)");

        // PartCategorizer.Category
        var cat = ts.MainModule.TypeDefinitions.FirstOrDefault(t => t.Name == "Category" && t.DeclaringType != null && t.DeclaringType.Name == "PartCategorizer");
        Console.WriteLine("\n=== Category fields (displayType etc) ===");
        if (cat != null)
            foreach (var f in cat.Fields)
                Console.WriteLine("  " + f.Type.Name + " " + f.Name);

        foreach (var mname in new[] { "OnTrueCATEGORY", "OnFalseCATEGORY", "OnTrueSUB", "OnFalseSUB",
                                       "OnFalseFilterOrCategory", "InsertSubcategoryButtons", "FlipAllFilterButtons",
                                       "CompileExclusionFilter", "CompileExclusionFilterSubassembly" })
        {
            var m = cat?.Methods.FirstOrDefault(x => x.Name == mname);
            if (m == null) { Console.WriteLine("\n=== Category." + mname + " (NOT FOUND) ==="); continue; }
            Console.WriteLine("\n=== Category." + mname + " ===");
            try { Console.WriteLine(decompiler.DecompileAsString(m.MetadataToken)); }
            catch (Exception ex) { Console.WriteLine("  decompile failed: " + ex.Message); }
        }

        foreach (var mname in new[] { "SetSimpleMode", "SetAdvancedMode", "Setup", "AddCustomFilter",
                                       "InstantiatePartCategorizerButton", "UpdateSubcategoryStates",
                                       "RemoveSubcategoryButtons" })
        {
            var m = cat?.Methods.FirstOrDefault(x => x.Name == mname)
                    ?? ts.MainModule.TypeDefinitions.FirstOrDefault(t => t.Name == "PartCategorizer")?.Methods.FirstOrDefault(x => x.Name == mname);
            if (m == null) { Console.WriteLine("\n=== " + mname + " (NOT FOUND) ==="); continue; }
            Console.WriteLine("\n=== " + mname + " ===");
            try { Console.WriteLine(decompiler.DecompileAsString(m.MetadataToken)); }
            catch (Exception ex) { Console.WriteLine("  decompile failed: " + ex.Message); }
        }

        // EditorPartList.Refresh(State)
        var epl = ts.MainModule.TypeDefinitions.FirstOrDefault(t => t.Name == "EditorPartList");
        foreach (var m in epl.Methods.Where(x => x.Name == "Refresh"))
        {
            Console.WriteLine("\n=== EditorPartList.Refresh(" + string.Join(",", m.Parameters.Select(p => p.Type.Name)) + ") ===");
            try { Console.WriteLine(decompiler.DecompileAsString(m.MetadataToken)); }
            catch (Exception ex) { Console.WriteLine("  decompile failed: " + ex.Message); }
        }
    }
}

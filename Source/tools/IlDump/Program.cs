using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class P {
  static void Main() {
    var asm = AssemblyDefinition.ReadAssembly(@"F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll");

    // 1) EditorPartList.State enum values
    var epl = asm.MainModule.Types.First(t => t.Name == "EditorPartList");
    var stateEnum = epl.NestedTypes.First(t => t.Name == "State");
    Console.WriteLine("=== EditorPartList.State enum ===");
    foreach (var f in stateEnum.Fields.Where(f => f.IsStatic)) {
      Console.WriteLine("  " + f.Name + " = " + f.Constant);
    }

    // 2) EditorSubassemblyItem delete-flow methods
    var esi = asm.MainModule.Types.First(t => t.Name == "EditorSubassemblyItem");
    foreach (var name in new[] { "MouseInput_Delete", "DeleteSubassembly", "OnDeleteConfirm", "OnDismiss", "OnDismissCoroutine" }) {
      var m = esi.Methods.FirstOrDefault(x => x.Name == name);
      if (m == null) { Console.WriteLine("=== EditorSubassemblyItem." + name + " (NOT FOUND) ==="); continue; }
      DumpMethod("EditorSubassemblyItem." + name, m);
    }

    // 3) EditorSubassemblyItem fields
    Console.WriteLine("=== EditorSubassemblyItem fields ===");
    foreach (var f in esi.Fields) Console.WriteLine("  " + f.FieldType.Name + " " + f.Name);

    // 4) OnDismissCoroutine state machine MoveNext
    var sm = esi.NestedTypes.FirstOrDefault(t => t.Name.Contains("OnDismissCoroutine"));
    if (sm != null) {
      var mn = sm.Methods.FirstOrDefault(x => x.Name == "MoveNext");
      if (mn != null) DumpMethod("OnDismissCoroutine.MoveNext", mn);
    }

    // 5) PartCategorizer.RemoveSubassemblyFromSelectedCategory
    var pc = asm.MainModule.Types.First(t => t.Name == "PartCategorizer");
    var rem = pc.Methods.FirstOrDefault(x => x.Name == "RemoveSubassemblyFromSelectedCategory");
    if (rem != null) DumpMethod("PartCategorizer.RemoveSubassemblyFromSelectedCategory", rem);

    // 6) EditorPartList.Refresh(State)
    var refresh = epl.Methods.FirstOrDefault(x => x.Name == "Refresh" && x.Parameters.Count == 1);
    if (refresh != null) DumpMethod("EditorPartList.Refresh(State)", refresh);

    var cat = pc.NestedTypes.First(t => t.Name == "Category");
    var remSub = cat.Methods.FirstOrDefault(x => x.Name == "RemoveSubassembly");
    if (remSub != null) DumpMethod("Category.RemoveSubassembly", remSub);

    var upd = pc.Methods.FirstOrDefault(x => x.Name == "Update");
    if (upd != null) DumpMethod("PartCategorizer.Update", upd);

    var refSub = epl.Methods.FirstOrDefault(x => x.Name == "RefreshSubassemblies");
    if (refSub != null) DumpMethod("EditorPartList.RefreshSubassemblies", refSub);

    var refPriv = epl.Methods.FirstOrDefault(x => x.Name == "Refresh" && x.Parameters.Count == 0);
    if (refPriv != null) DumpMethod("EditorPartList.Refresh()", refPriv);

    var rsl = epl.Methods.FirstOrDefault(x => x.Name == "RefreshSubassemblyList");
    if (rsl != null) DumpMethod("EditorPartList.RefreshSubassemblyList", rsl);

    // Where are subassembliesAll / subassemblies assigned? Scan all PartCategorizer methods for stfld.
    Console.WriteLine("=== stfld subassembliesAll / subassemblies locations ===");
    foreach (var mm in pc.Methods) {
      if (mm.Body == null) continue;
      foreach (var ins in mm.Body.Instructions) {
        if (ins.OpCode == OpCodes.Stfld && ins.Operand is FieldReference fr &&
            (fr.Name == "subassembliesAll" || fr.Name == "subassemblies")) {
          Console.WriteLine("  " + mm.Name + " -> stfld " + fr.Name);
        }
      }
    }

    var setup = pc.Methods.FirstOrDefault(x => x.Name == "Setup");
    if (setup != null) DumpMethod("PartCategorizer.Setup", setup);

    var catType = pc.NestedTypes.First(t => t.Name == "Category");
    var ctor = catType.Methods.FirstOrDefault(x => x.IsConstructor && x.Parameters.Count > 3);
    if (ctor != null) {
      Console.WriteLine("=== Category..ctor params ===");
      foreach (var p in ctor.Parameters) Console.WriteLine("  " + p.ParameterType.Name + " " + p.Name);
      DumpMethod("Category..ctor", ctor);
    }
    var addSub = catType.Methods.FirstOrDefault(x => x.Name == "AddSubcategory");
    if (addSub != null) DumpMethod("Category.AddSubcategory", addSub);
    var onFalseSub = catType.Methods.FirstOrDefault(x => x.Name == "OnFalseSUB");
    if (onFalseSub != null) DumpMethod("Category.OnFalseSUB", onFalseSub);
  }

  static void DumpMethod(string title, MethodDefinition m) {
    Console.WriteLine("=== " + title + " ===");
    if (m.Body == null) { Console.WriteLine("  (no body)"); return; }
    foreach (var ins in m.Body.Instructions) {
      string ex = "";
      if ((ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt || ins.OpCode == OpCodes.Newobj) && ins.Operand is MethodReference mr)
        ex = " " + mr.DeclaringType.Name + "." + mr.Name;
      else if ((ins.OpCode == OpCodes.Ldfld || ins.OpCode == OpCodes.Stfld || ins.OpCode == OpCodes.Ldsfld || ins.OpCode == OpCodes.Stsfld) && ins.Operand is FieldReference fr)
        ex = " " + fr.Name;
      else if (ins.OpCode == OpCodes.Ldstr) ex = " \"" + ins.Operand + "\"";
      else if (ins.Operand is int) ex = " " + ins.Operand;
      Console.WriteLine("  " + ins.OpCode + ex);
    }
  }
}

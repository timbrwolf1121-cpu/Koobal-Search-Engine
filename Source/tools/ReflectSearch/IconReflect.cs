using System;
using System.Linq;
using Mono.Cecil;
class IconReflect { static void Main() {
var asm = AssemblyDefinition.ReadAssembly(@"F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll");
var pc = asm.MainModule.Types.First(t => t.Name == "PartCategorizer");
Console.WriteLine("=== PartCategorizer icon methods ===");
foreach (var m in pc.Methods.Where(x => x.Name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0)) Console.WriteLine(m.FullName);
var pcb = asm.MainModule.Types.First(t => t.Name == "PartCategorizerButton");
Console.WriteLine("\n=== PartCategorizerButton ===");
foreach (var f in pcb.Fields) if (f.Name.IndexOf("icon", StringComparison.OrdinalIgnoreCase)>=0||f.Name.IndexOf("sprite", StringComparison.OrdinalIgnoreCase)>=0) Console.WriteLine("field: "+f.FieldType.Name+" "+f.Name);
foreach (var p in pcb.Properties) if (p.Name.IndexOf("icon", StringComparison.OrdinalIgnoreCase)>=0||p.Name.IndexOf("sprite", StringComparison.OrdinalIgnoreCase)>=0) Console.WriteLine("prop: "+p.PropertyType.Name+" "+p.Name);
var cat = pc.NestedTypes.First(t => t.Name == "Category");
Console.WriteLine("\n=== Category ===");
foreach (var f in cat.Fields) if (f.Name.IndexOf("icon", StringComparison.OrdinalIgnoreCase)>=0) Console.WriteLine("field: "+f.FieldType.Name+" "+f.Name);
var load = pc.Methods.FirstOrDefault(m => m.Name == "LoadCustomPartCategories");
if (load?.Body != null) { Console.WriteLine("\n=== LoadCustomPartCategories ===");
foreach (var ins in load.Body.Instructions) {
if (ins.OpCode.Name=="Ldstr" && ins.Operand is string s && (s.IndexOf("icon", StringComparison.OrdinalIgnoreCase)>=0||s.StartsWith("stockIcon"))) Console.WriteLine(" STR: "+s);
if ((ins.OpCode.Name=="Call"||ins.OpCode.Name=="Callvirt") && ins.Operand is MethodReference mr && mr.Name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase)>=0) Console.WriteLine(" CALL: "+mr.DeclaringType.Name+"."+mr.Name);
}}}}

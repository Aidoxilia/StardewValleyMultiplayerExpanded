using System.Reflection;
var asm = Assembly.LoadFrom(@"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll");
var t = asm.GetType("StardewValley.ItemStockInformation");
Console.WriteLine(t);
foreach(var c in t!.GetConstructors(BindingFlags.Public|BindingFlags.Instance))
  Console.WriteLine(c);
foreach(var p in t.GetProperties(BindingFlags.Public|BindingFlags.Instance))
  Console.WriteLine($"P {p.PropertyType.Name} {p.Name}");

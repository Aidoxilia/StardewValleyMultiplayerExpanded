using System.Reflection;

var svPath = @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll";
var mgPath = @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\MonoGame.Framework.dll";

var asm = Assembly.LoadFrom(svPath);
var mga = Assembly.LoadFrom(mgPath);

// Confirm tabs / pages field types
var gameMenuType = asm.GetType("StardewValley.Menus.GameMenu")!;
var tabsF = gameMenuType.GetField("tabs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
var pagesF = gameMenuType.GetField("pages", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
Console.WriteLine("tabs field type  : " + tabsF?.FieldType);
Console.WriteLine("pages field type : " + pagesF?.FieldType);

// List all ClickableComponent and ClickableTextureComponent public members
var ccType = asm.GetType("StardewValley.Menus.ClickableComponent")!;
var ctcType = asm.GetType("StardewValley.Menus.ClickableTextureComponent")!;
Console.WriteLine($"\nClickableComponent fields:");
foreach (var f in ccType.GetFields(BindingFlags.Public | BindingFlags.Instance))
  Console.WriteLine($"  {f.FieldType.Name} {f.Name}");
Console.WriteLine($"\nClickableTextureComponent fields:");
foreach (var f in ctcType.GetFields(BindingFlags.Public | BindingFlags.Instance))
  Console.WriteLine($"  {f.FieldType.Name} {f.Name}");

// Check if ClickableTextureComponent has a draw(SpriteBatch) method
var sbType = mga.GetType("Microsoft.Xna.Framework.Graphics.SpriteBatch")!;
foreach (var m in ctcType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
  if (m.Name == "draw") Console.WriteLine($"CTC.{m.Name}({string.Join(",", System.Linq.Enumerable.Select(m.GetParameters(), p => p.ParameterType.Name))})");

var spriteBatchType = monogame.GetType(""Microsoft.Xna.Framework.Graphics.SpriteBatch"")!;
var drawMethod = t.GetMethod(""draw"", BindingFlags.Public | BindingFlags.Instance, null, new[] { spriteBatchType }, null)!;
var body = drawMethod.GetMethodBody()!;
var il = body.GetILAsByteArray()!;
var module = t.Module;

int pos = 0;
int seq = 0;
while (pos < il.Length)
{
  int startPos = pos;
  byte op = il[pos++];
  OpCode opcode;
  if (op == 0xFE) { byte op2 = il[pos++]; opcode = (OpCode)typeof(OpCodes).GetFields().First(f => f.FieldType == typeof(OpCode) && ((OpCode)f.GetValue(null)!).Value == (short)(0xFE00 | op2)).GetValue(null)!; }
  else { opcode = (OpCode)typeof(OpCodes).GetFields().First(f => f.FieldType == typeof(OpCode) && ((OpCode)f.GetValue(null)!).Value == op && ((OpCode)f.GetValue(null)!).Size == 1).GetValue(null)!; }

  int operandSize = opcode.OperandType switch
  {
    OperandType.InlineNone => 0,
    OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
    OperandType.InlineVar => 2,
    OperandType.InlineI or OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok or OperandType.InlineBrTarget or OperandType.InlineString or OperandType.InlineSig => 4,
    OperandType.InlineI8 or OperandType.InlineR => 8,
    _ => 4
  };

  string operandStr = """";
    if (operandSize == 4 && (opcode.OperandType == OperandType.InlineField || opcode.OperandType == OperandType.InlineMethod || opcode.OperandType == OperandType.InlineType))
  {
    int token = BitConverter.ToInt32(il, pos);
    try
    {
      if (opcode.OperandType == OperandType.InlineField) operandStr = module.ResolveField(token)?.ToString() ?? token.ToString();
      else if (opcode.OperandType == OperandType.InlineMethod) operandStr = module.ResolveMethod(token)?.ToString() ?? token.ToString();
      else operandStr = module.ResolveType(token)?.ToString() ?? token.ToString();
    }
    catch { operandStr = token.ToString(""X8""); }
  }
  else if (operandSize > 0)
  {
    operandStr = BitConverter.ToInt32(il.Skip(pos).Take(Math.Min(4, operandSize)).ToArray(), 0).ToString();
  }
  pos += operandSize;

  Console.WriteLine($""{ startPos,5}: { opcode.Name,-20}
  { operandStr}
  "");
  seq++;
  if (seq > 500) { Console.WriteLine(""... truncated""); break; }
}

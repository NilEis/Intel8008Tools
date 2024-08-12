using Intel8008Tools;

var cpu = new Intel8008();
var cyc = (uint)0;
// for (var i = 0; i <= byte.MaxValue; i++)
// {
//     Console.Out.WriteLine(Intel8008.Disassemble(0, [(byte)i, 0, 0], out var offset, ref cyc));
// }

var prefix = @"";
cpu.LoadMemory(Path.Join(prefix, "invaders.h"), 0)
    .LoadMemory(Path.Join(prefix, "invaders.g"), 0x800)
    .LoadMemory(Path.Join(prefix, "invaders.f"), 0x1000)
    .LoadMemory(Path.Join(prefix, "invaders.e"), 0x1800)
    ;
Console.Out.WriteLine(cpu.Disassemble(0, 0x07ff));
while (cpu.run(0))
{
    ;
}
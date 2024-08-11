using Intel8008Tools;

var cpu = new Intel8008();
cpu.LoadMemory(@"C:\Users\Nils_Eisenach\Desktop\dev\CS\Intel8008Tools\invaders\invaders.h", 0);
Console.Out.WriteLine(cpu.Disassemble(0, 0x07ff));
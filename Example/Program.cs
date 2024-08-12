using Intel8008Tools;

var cpu = new Intel8008();
cpu.LoadMemory(@"Path", 0);
Console.Out.WriteLine(cpu.Disassemble(0, 0x07ff));
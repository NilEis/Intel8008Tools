using Intel8008Tools;

uint c = 0;
Intel8008.Disassemble(0, [0x06, 0], out _, ref c);

var cpu = new Intel8008();
var cyc = (uint)0;
// for (var i = 0; i <= byte.MaxValue; i++)
// {
//     Console.Out.WriteLine(Intel8008.Disassemble(0, [(byte)i, 0, 0], out var offset, ref cyc));
// }
Intel8008.RunTestSuite(true, false);
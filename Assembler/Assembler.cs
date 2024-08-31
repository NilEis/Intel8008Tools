namespace Assembler;

using System.CommandLine;

class Assembler
{
    static void Main(string[] args)
    {
        var rootCmd = new RootCommand("A tool to (dis-)assemble intel 8080 code")
        {
            BuildCmd((output, input) => { Console.Out.WriteLine(input); }),
            DisasmCmd((input) => { Console.Out.WriteLine(input); })
        };
        rootCmd.Invoke(args);
    }

    private static Command DisasmCmd(Action<FileInfo> handler)
    {
        var fileArg = new Argument<FileInfo>("bin file", "the binary file to disassemble");
        var disasmCmd = new Command("dasm", "disassemble a binary file")
        {
            fileArg
        };
        disasmCmd.AddAlias("disasm");
        disasmCmd.AddAlias("d");
        disasmCmd.SetHandler(handler, fileArg);
        return disasmCmd;
    }

    private static Command BuildCmd(Action<FileInfo, FileInfo> handler)
    {
        var outputOption = new Option<FileInfo>(["-o", "--output"], () => new FileInfo("a.bin"),
            "the destination for the assembled binary")
        {
            IsRequired = false
        };
        var fileArg = new Argument<FileInfo>("file", "assembly file");
        var buildCmd = new Command("build", "assemble an assembler file")
        {
            outputOption,
            fileArg
        };
        buildCmd.AddAlias("b");
        buildCmd.SetHandler(handler, outputOption, fileArg);
        return buildCmd;
    }
}
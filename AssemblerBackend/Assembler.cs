using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AssemblerBackend;

public static partial class Assembler
{
    public static bool Assemble(string code, [NotNullWhen(true)] out byte[]? buf)
    {
        var res = new Queue<byte>();
        var lines = CommentRegEx().Replace(code, "\n").Split('\n',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Console.Out.WriteLine(string.Join('\n', lines));
        foreach (var line in lines)
        {
        }

        buf = res.ToArray();
        return true;
    }

    [GeneratedRegex(@";.*$", RegexOptions.Multiline)]
    private static partial Regex CommentRegEx();
}
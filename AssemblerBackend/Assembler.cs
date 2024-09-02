using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Sprache;

namespace AssemblerBackend;

public static partial class Assembler
{
    public static bool Assemble(string code, [NotNullWhen(true)] out byte[]? buf)
    {
        var memoryBuffer = new List<byte>();
        var codeWithoutComments = CommentRegEx().Replace(code, "\n");
        var codeWithNormalizedLabels = LabelRegEx().Replace(codeWithoutComments, "\n");
        var lines = codeWithNormalizedLabels.ToUpper().Split('\n',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var numberParser = (
                // Hexadecimal format: 1234h
                from digits in Parse.Chars("0123456789ABCDEF").AtLeastOnce().Text()
                from suffix in Parse.Char('H')
                from end in Parse.WhiteSpace.Many().End()
                select Convert.ToInt64(digits, 16)
            )
            .Or
            (
                // Hexadecimal format: 0x1234
                from prefix in Parse.String("0X")
                from digits in Parse.Chars("0123456789ABCDEF").AtLeastOnce().Text()
                from end in Parse.WhiteSpace.Many().End()
                select Convert.ToInt64(digits, 16)
            )
            .Or
            (
                // Binary format: 1010b
                from digits in Parse.Chars("01").AtLeastOnce().Text()
                from suffix in Parse.Char('B')
                from end in Parse.WhiteSpace.Many().End()
                select Convert.ToInt64(digits, 2)
            )
            .Or
            (
                // Binary format: 0b1010
                from prefix in Parse.String("0B")
                from digits in Parse.Chars("01").AtLeastOnce().Text()
                from end in Parse.WhiteSpace.Many().End()
                select Convert.ToInt64(digits, 2)
            )
            .Or
            (
                // Decimal format: 1234
                Parse.Digit.AtLeastOnce().Text().Select(digits => Convert.ToInt64(digits, 10)
                )
            );
        var labelParser =
            from labelName in Parse.CharExcept(':').AtLeastOnce().Text()
            from end in Parse.Char(':')
            from trailing in Parse.WhiteSpace.Many()
            select labelName;
        var reserveParser =
            from prefix in Parse.String("RES").Once()
            from typeSize in Parse.Char('B').Once().Return(1)
                .Or(
                    Parse.Char('W').Once().Return(2)
                )
                .Or(
                    Parse.Char('D').Once().Return(4)
                )
                .Or(
                    Parse.Char('Q').Once().Return(8)
                )
            from infix in Parse.WhiteSpace.AtLeastOnce()
            from num in numberParser
            select Enumerable.Repeat((byte)0, (int)num * typeSize).ToArray();
        var declareParser =
            from prefix in Parse.Char('D').Once()
            from typeSize in Parse.Char('B').Once().Return(1)
                .Or(
                    Parse.Char('W').Once().Return(2)
                )
                .Or(
                    Parse.Char('D').Once().Return(4)
                )
                .Or(
                    Parse.Char('Q').Once().Return(8)
                )
            from infix in Parse.WhiteSpace.AtLeastOnce()
            from data in from buf in
                (
                    from num in numberParser
                    select NumToByteArray(typeSize, num)
                ).Or(
                    from prefix in Parse.Char('"').Once()
                    from str in Parse.CharExcept('"').Many().Text()
                    from postfix in Parse.Char('"').Once()
                    select NumToByteArray(typeSize, str)
                ).DelimitedBy(Parse.Char(',').Token()).Select(v => v.ToArray())
                select buf.SelectMany(b => b).ToArray()
            select data;
        var orgParser =
            from org in Parse.String("ORG").Once()
            from whiteSpace in Parse.WhiteSpace.Many()
            from num in numberParser
            from whiteSpaceEnd in Parse.WhiteSpace.Many()
            select num;

        var asmParser =
            from cmd in Parse.Upper.Repeat(2, 4).Token().Text()
            from args in numberParser.Select(v => $"{v}").Or(Parse.Upper.AtLeastOnce().Text())
                .DelimitedBy(Parse.Char(',').Token()).Repeat(0, 1).Select(v => v)
            select (cmd: cmd, args: args.SelectMany(v => v).ToArray());
        var addr = 0L;
        var labels = new Dictionary<string, long>();
        var firstPassRes = new List<(string, string[])>();
        foreach (var line in lines)
        {
            var label = labelParser.TryParse(line);
            if (label.WasSuccessful)
            {
                labels.Add(label.Value, addr);
                Console.Out.WriteLine($"label: {label.Value}");
                continue;
            }

            var org = orgParser.TryParse(line);
            if (org.WasSuccessful)
            {
                addr = org.Value;
                Console.Out.WriteLine($"org: {org.Value}");
                continue;
            }

            var dec = declareParser.TryParse(line);
            if (dec.WasSuccessful)
            {
                addr += dec.Value.Length;
                memoryBuffer.AddRange(dec.Value);
                Console.Out.WriteLine($"dec: [{string.Join(", ", dec.Value)}]");
                continue;
            }

            var res = reserveParser.TryParse(line);
            if (res.WasSuccessful)
            {
                addr += res.Value.Length;
                memoryBuffer.AddRange(res.Value);
                Console.Out.WriteLine($"res: [{string.Join(", ", res.Value)}]");
                continue;
            }

            var cmd = asmParser.TryParse(line);
            if (cmd.WasSuccessful)
            {
                firstPassRes.Add(cmd.Value);
                Console.Out.WriteLine(
                    $"line: {cmd.Value.cmd} - {string.Join(" & ", cmd.Value.args)}");
                continue;
            }

            Console.Out.WriteLine(line);
        }

        buf = memoryBuffer.ToArray();
        return true;
    }

    private static byte[] NumToByteArray(int typeSize, string str)
    {
        var res = new List<byte>();
        foreach (var c in str)
        {
            res.AddRange(NumToByteArray(typeSize, (long)((byte)c)));
        }

        return res.ToArray();
    }

    private static byte[] NumToByteArray(int typeSize, long num)
    {
        return typeSize switch
        {
            1 => (byte[]) [(byte)num],
            2 => [(byte)((ushort)num >> 8), (byte)num],
            4 => [(byte)((uint)num >> 24), (byte)((uint)num >> 16), (byte)((ushort)num >> 8), (byte)num],
            8 =>
            [
                (byte)((ulong)num >> 56), (byte)((ulong)num >> 48), (byte)((ulong)num >> 40),
                (byte)((ulong)num >> 32), (byte)((uint)num >> 24), (byte)((uint)num >> 16),
                (byte)((ushort)num >> 8), (byte)num
            ],
            _ => throw new ConstraintException("Cannot declare region")
        };
    }

    [GeneratedRegex(@";.*$", RegexOptions.Multiline)]
    private static partial Regex CommentRegEx();

    [GeneratedRegex(@"(?<=\w+:)(?!\s*$)", RegexOptions.Multiline)]
    private static partial Regex LabelRegEx();
}
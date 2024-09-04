using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Sprache;

namespace AssemblerBackend;

public static partial class Assembler
{
    public static bool Assemble(string code, [NotNullWhen(true)] out byte[]? buf)
    {
        var memoryBuffer = new MemoryBuffer<byte>();
        var codeWithoutComments = CommentRegEx().Replace(code, "\n");
        var codeWithNormalizedLabels = LabelRegEx().Replace(codeWithoutComments, "\n");
        var lines = codeWithNormalizedLabels.ToUpper().Split('\n',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var addr = 0L;
        var lineToAddr = new Queue<long>();
        foreach (var line in lines)
        {
            var label = LabelParser.TryParse(line);
            if (label.WasSuccessful)
            {
                Labels.Add(label.Value, addr);
                continue;
            }

            var org = OrgParser.TryParse(line);
            if (org.WasSuccessful)
            {
                addr = org.Value;
                continue;
            }

            var dec = DeclareParser.TryParse(line);
            if (dec.WasSuccessful)
            {
                lineToAddr.Enqueue(addr);
                addr += dec.Value.Length;
                continue;
            }

            var res = ReserveParser.TryParse(line);
            if (res.WasSuccessful)
            {
                lineToAddr.Enqueue(addr);
                addr += res.Value.Length;
                continue;
            }

            var size = MnemonicSizeParser.TryParse(line);
            if (size.WasSuccessful)
            {
                lineToAddr.Enqueue(addr);
                addr += size.Value;
                continue;
            }
        }

        foreach (var line in lines)
        {
            var dec = DeclareParser.TryParse(line);
            if (dec.WasSuccessful)
            {
                addr = lineToAddr.Dequeue();
                for (var i = 0; i < dec.Value.Length; i++)
                {
                    memoryBuffer[(int)(addr + i)] = dec.Value[i];
                }

                continue;
            }

            var res = ReserveParser.TryParse(line);
            if (res.WasSuccessful)
            {
                addr = lineToAddr.Dequeue();
                for (var i = 0; i < res.Value.Length; i++)
                {
                    memoryBuffer[(int)(addr + i)] = res.Value[i];
                }

                continue;
            }

            var mnem = MnemonicParser.TryParse(line);
            if (mnem.WasSuccessful)
            {
                addr = lineToAddr.Dequeue();
                for (var i = 0; i < mnem.Value.Length; i++)
                {
                    memoryBuffer[(int)(addr + i)] = mnem.Value[i];
                }

                continue;
            }
        }

        buf = memoryBuffer.ToArray();
        return true;
    }

    private static readonly Parser<long> NumberParser =
        (
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

    private static readonly Parser<long> AddrParser =
        Parse.Upper.AtLeastOnce().Select((s) => Labels[s.ToString()]).Or(NumberParser);

    private static readonly Parser<Reg> RegParser = Parse.Chars("BCDEHLMA").Token().Select(v =>
        Enum.TryParse($"{v}", out Reg reg) ? reg : throw new ConstraintException("Invalid reg"));

    private static readonly Parser<Rp> DRegParser =
        from dreg in Parse.String("BC").Token().Text()
            .Or(Parse.String("DE").Token().Text())
            .Or(Parse.String("HL").Token().Text())
            .Or(Parse.String("SP").Token().Text())
            .Or(Parse.String("PSW").Token().Return("SP"))
        select Enum.TryParse($"{dreg}", out Rp reg) ? reg : throw new ConstraintException("Invalid dreg");

    private static readonly Parser<(string cmd, string[] args)> AsmLineParser =
        from cmd in Parse.Upper.Repeat(2, 4).Token().Text()
        from args in NumberParser.Select(v => $"{v}").Or(Parse.Upper.AtLeastOnce().Text())
            .DelimitedBy(Parse.Char(',').Token()).Repeat(0, 1).Select(v => v)
        select (cmd: cmd, args: args.SelectMany(v => v).ToArray());

    private static readonly Parser<CompareCondition> CompareConditionParser =
        from cc in Parse.String("NZ").Text()
            .Or(Parse.String("Z").Text())
            .Or(Parse.String("NC").Text())
            .Or(Parse.String("C").Text())
            .Or(Parse.String("PO").Text())
            .Or(Parse.String("PE").Text())
            .Or(Parse.String("P").Text())
            .Or(Parse.String("N").Text())
        select Enum.TryParse($"{cc}", out CompareCondition reg)
            ? reg
            : throw new ConstraintException("Invalid compare condition");

    private static readonly Parser<int> MnemonicSizeParser =
        Parse.String("NOP").Token().Return(1)
            .Or(Parse.String("LXI").Token().Return(3))
            .Or(Parse.String("STAX").Token().Return(1))
            .Or(Parse.String("INX").Token().Return(1))
            .Or(Parse.String("INR").Token().Return(1))
            .Or(Parse.String("DCR").Token().Return(1))
            .Or(Parse.String("MVI").Token().Return(2))
            .Or(Parse.String("DAD").Token().Return(1))
            .Or(Parse.String("LDAX").Token().Return(1))
            .Or(Parse.String("DCX").Token().Return(1))
            .Or(Parse.String("RLC").Token().Return(1))
            .Or(Parse.String("RRC").Token().Return(1))
            .Or(Parse.String("RAL").Token().Return(1))
            .Or(Parse.String("RAR").Token().Return(1))
            .Or(Parse.String("SHLD").Token().Return(3))
            .Or(Parse.String("DAA").Token().Return(1))
            .Or(Parse.String("LHLD").Token().Return(3))
            .Or(Parse.String("CMA").Token().Return(1))
            .Or(Parse.String("STA").Token().Return(3))
            .Or(Parse.String("STC").Token().Return(1))
            .Or(Parse.String("LDA").Token().Return(3))
            .Or(Parse.String("CMC").Token().Return(1))
            .Or(Parse.String("MOV").Token().Return(1))
            .Or(Parse.String("HLT").Token().Return(1))
            .Or(Parse.String("ADD").Token().Return(1))
            .Or(Parse.String("ADC").Token().Return(1))
            .Or(Parse.String("SUB").Token().Return(1))
            .Or(Parse.String("SBB").Token().Return(1))
            .Or(Parse.String("ANA").Token().Return(1))
            .Or(Parse.String("XRA").Token().Return(1))
            .Or(Parse.String("ORA").Token().Return(1))
            .Or(Parse.String("CMP").Token().Return(1))
            .Or(
                (
                    from s in Parse.String("R")
                    from cc in CompareConditionParser
                    select cc
                ).Token().Return(1)
            )
            .Or(Parse.String("POP").Token().Return(1))
            .Or(
                (
                    from s in Parse.String("J")
                    from cc in CompareConditionParser
                    select cc
                ).Token().Return(3)
            )
            .Or(Parse.String("JMP").Token().Return(3))
            .Or(
                (
                    from s in Parse.String("C")
                    from cc in CompareConditionParser
                    select cc
                ).Token().Return(3)
            )
            .Or(Parse.String("PUSH").Token().Return(1))
            .Or(Parse.String("ADI").Token().Return(2))
            .Or(Parse.String("ACI").Token().Return(2))
            .Or(Parse.String("SUI").Token().Return(2))
            .Or(Parse.String("SBI").Token().Return(2))
            .Or(Parse.String("ANI").Token().Return(2))
            .Or(Parse.String("XRI").Token().Return(2))
            .Or(Parse.String("ORI").Token().Return(2))
            .Or(Parse.String("CPI").Token().Return(2))
            .Or(Parse.String("RST").Token().Return(1))
            .Or(Parse.String("RET").Token().Return(1))
            .Or(Parse.String("CALL").Token().Return(3))
            .Or(Parse.String("OUT").Token().Return(2))
            .Or(Parse.String("IN").Token().Return(2))
            .Or(Parse.String("XTHL").Token().Return(1))
            .Or(Parse.String("PCHL").Token().Return(1))
            .Or(Parse.String("XCHG").Token().Return(1))
            .Or(Parse.String("DI").Token().Return(1))
            .Or(Parse.String("SPHL").Token().Return(1))
            .Or(Parse.String("EI").Token().Return(1));

    private static readonly Parser<byte[]> MnemonicParser =
        Parse.String("NOP").Token().Return((byte[]) [0b00000000])
            .Or(Parse.String("RLC").Token().Return((byte[]) [0b00000111]))
            .Or(Parse.String("RRC").Token().Return((byte[]) [0b00001111]))
            .Or(Parse.String("RAL").Token().Return((byte[]) [0b00010111]))
            .Or(Parse.String("RAR").Token().Return((byte[]) [0b00011111]))
            .Or(Parse.String("DAA").Token().Return((byte[]) [0b00100111]))
            .Or(Parse.String("CMA").Token().Return((byte[]) [0b00101111]))
            .Or(Parse.String("STC").Token().Return((byte[]) [0b00110111]))
            .Or(Parse.String("CMC").Token().Return((byte[]) [0b00111111]))
            .Or(Parse.String("HLT").Token().Return((byte[]) [0b01110110]))
            .Or(Parse.String("RET").Token().Return((byte[]) [0b11001001]))
            .Or(Parse.String("XTHL").Token().Return((byte[]) [0b11100011]))
            .Or(Parse.String("PCHL").Token().Return((byte[]) [0b11101001]))
            .Or(Parse.String("XCHG").Token().Return((byte[]) [0b11101011]))
            .Or(Parse.String("DI").Token().Return((byte[]) [0b11110011]))
            .Or(Parse.String("SPHL").Token().Return((byte[]) [0b11111001]))
            .Or(Parse.String("EI").Token().Return((byte[]) [0b11111011]))
            .Or(
                from cmd in Parse.String("LXI").Token().Return((byte)0b00000001)
                from rp in DRegParser
                from infix in Parse.Char(',').Token()
                from num in NumberParser
                select (byte[])
                [
                    (byte)(cmd | (byte)((byte)rp << 5)),
                    (byte)num,
                    (byte)(num >> 8)
                ]
            )
            .Or(
                from cmd in Parse.String("STAX").Token().Return((byte)0b00000010)
                from rp in DRegParser
                select (byte[]) [(byte)(cmd | (byte)((byte)rp << 4))]
            )
            .Or(
                from cmd in Parse.String("INX").Token().Return((byte)0b00000011)
                from rp in DRegParser
                select (byte[]) [(byte)(cmd | (byte)((byte)rp << 4))]
            )
            .Or(
                from cmd in Parse.String("INR").Token().Return((byte)0b00000100)
                from reg in RegParser
                select (byte[]) [(byte)(cmd | (byte)((byte)reg << 3))]
            )
            .Or(
                from cmd in Parse.String("DCR").Token().Return((byte)0b00000101)
                from reg in RegParser
                select (byte[]) [(byte)(cmd | (byte)((byte)reg << 3))]
            )
            .Or(
                from cmd in Parse.String("MVI").Token().Return((byte)0b00000110)
                from reg in RegParser
                from infix in Parse.Char(',').Token()
                from num in NumberParser
                select (byte[]) [(byte)(cmd | (byte)((byte)reg << 3)), (byte)num]
            )
            .Or(
                from cmd in Parse.String("DAD").Token().Return((byte)0b00001001)
                from rp in DRegParser
                select (byte[]) [(byte)(cmd | (byte)((byte)rp << 4))]
            )
            .Or(
                from cmd in Parse.String("LDAX").Token().Return((byte)0b00001010)
                from rp in DRegParser
                select (byte[]) [(byte)(cmd | (byte)((byte)rp << 4))]
            )
            .Or(
                from cmd in Parse.String("DCX").Token().Return((byte)0b00001011)
                from rp in DRegParser
                select (byte[]) [(byte)(cmd | (byte)((byte)rp << 4))]
            )
            .Or(
                from cmd in Parse.String("SHLD").Token().Return((byte)0b00100010)
                from num in NumberParser
                select (byte[]) [cmd, (byte)num, (byte)(num >> 8)]
            )
            .Or(
                from cmd in Parse.String("LHLD").Token().Return((byte)0b00101010)
                from num in NumberParser
                select (byte[]) [cmd, (byte)num, (byte)(num >> 8)]
            )
            .Or(
                from cmd in Parse.String("STA").Token().Return((byte)0b00110010)
                from num in NumberParser
                select (byte[]) [cmd, (byte)num, (byte)(num >> 8)]
            )
            .Or(
                from cmd in Parse.String("LDA").Token().Return((byte)0b00111010)
                from num in NumberParser
                select (byte[]) [cmd, (byte)num, (byte)(num >> 8)]
            )
            .Or(
                from cmd in Parse.String("MOV").Token().Return((byte)0b01000000)
                from destReg in RegParser
                from infix in Parse.Char(',').Token()
                from srcReg in RegParser
                select (byte[]) [(byte)((cmd | (byte)srcReg) | (byte)((byte)destReg << 3))]
            );

    private static readonly Parser<long> OrgParser =
        from org in Parse.String("ORG").Once()
        from whiteSpace in Parse.WhiteSpace.Many()
        from num in NumberParser
        from whiteSpaceEnd in Parse.WhiteSpace.Many()
        select num;

    private static readonly Parser<byte[]> DeclareParser =
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
                from num in NumberParser
                select NumToByteArray(typeSize, num)
            ).Or(
                from prefix in Parse.Char('"').Once()
                from str in Parse.CharExcept('"').Many().Text()
                from postfix in Parse.Char('"').Once()
                select NumToByteArray(typeSize, str)
            ).DelimitedBy(Parse.Char(',').Token()).Select(v => v.ToArray())
            select buf.SelectMany(b => b).ToArray()
        select data;

    private static readonly Parser<byte[]> ReserveParser =
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
        from num in NumberParser
        select Enumerable.Repeat((byte)0, (int)num * typeSize).ToArray();

    private static readonly Parser<string> LabelParser =
        from labelName in Parse.CharExcept(':').AtLeastOnce().Text()
        from end in Parse.Char(':')
        from trailing in Parse.WhiteSpace.Many()
        select labelName;

    private static readonly Dictionary<string, long> Labels = new();

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
            2 => [(byte)num, (byte)((ushort)num >> 8)],
            4 => [(byte)num, (byte)((ushort)num >> 8), (byte)((uint)num >> 16), (byte)((uint)num >> 24)],
            8 =>
            [
                (byte)num, (byte)((ushort)num >> 8), (byte)((uint)num >> 16), (byte)((uint)num >> 24),
                (byte)((ulong)num >> 32), (byte)((ulong)num >> 40), (byte)((ulong)num >> 48), (byte)((ulong)num >> 56)
            ],
            _ => throw new ConstraintException("Cannot declare region")
        };
    }

    [GeneratedRegex(@";.*$", RegexOptions.Multiline)]
    private static partial Regex CommentRegEx();

    [GeneratedRegex(@"(?<=\w+:)(?!\s*$)", RegexOptions.Multiline)]
    private static partial Regex LabelRegEx();
}

public class MemoryBuffer<T>
{
    private List<T> internalBuffer = [];

    public T this[int index]
    {
        get => internalBuffer[index];
        set
        {
            if (index < internalBuffer.Count)
            {
                internalBuffer[index] = value;
            }
            else
            {
                internalBuffer.AddRange(Enumerable.Repeat(default(T), 1 + (index - internalBuffer.Count)).ToArray()!);
                internalBuffer[index] = value;
            }
        }
    }

    public T[] ToArray()
    {
        return internalBuffer.ToArray();
    }
}
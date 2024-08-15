// ReSharper disable InconsistentNaming

using System.Data;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace Intel8008Tools;

public class Intel8008
{
    private readonly record struct State(
        uint cycles,
        ulong pins,
        byte[] ports,
        byte A,
        ushort BC,
        ushort DE,
        ushort HL,
        ushort SP,
        ushort PC,
        byte ConditionState,
        byte[] mem);

    private readonly Stack<State> states = [];
    private int iterations;
    private uint cycles;
    private ulong pins;
    public Func<int, byte>[] inPorts;
    public Action<int, byte>[] outPorts;

    public byte[] Ports = new byte[256];

    private byte A;
    private ushort BC;
    private bool willFail;

    private byte B
    {
        get => (byte)(BC >> 8);
        set => BC = (ushort)((BC & 0xFF) | (value << 8));
    }

    private byte C
    {
        get => (byte)(BC & 0xFF);
        set => BC = (ushort)((BC & (0xFF << 8)) | value);
    }

    private ushort DE;

    private byte D
    {
        get => (byte)(DE >> 8);
        set => DE = (ushort)((DE & 0xFF) | (value << 8));
    }

    private byte E
    {
        get => (byte)(DE & 0xFF);
        set => DE = (ushort)((DE & (0xFF << 8)) | value);
    }

    private ushort HL;

    private byte H
    {
        get => (byte)(HL >> 8);
        set => HL = (ushort)((HL & 0xFF) | (value << 8));
    }

    public byte L
    {
        get => (byte)(HL & 0xFF);
        set => HL = (ushort)((HL & (0xFF << 8)) | value);
    }

    private byte M
    {
        get => LoadByteFromMemory(HL);
        set => WriteToMemory(HL, value);
    }

    public byte Z
    {
        get => (byte)(Cc.Z ? 1 : 0);
        set => Cc.Z = value == 0;
    }

    public byte S
    {
        get => (byte)(Cc.S ? 1 : 0);
        set => Cc.S = (value & 0b10000000) == 0b10000000;
    }

    public byte P
    {
        get => (byte)(Cc.P ? 1 : 0);
        set => Cc.P = BitOperations.PopCount(value) % 2 == 0;
    }

    private byte Cy
    {
        get => (byte)(Cc.Cy ? 1 : 0);
        set => Cc.Cy = value != 0;
    }

    public byte Ac
    {
        get => (byte)(Cc.Ac ? 1 : 0);
        set => Cc.Ac = value != 0;
    }

    private ushort SP;
    private bool JmpWasExecuted;

    private ushort PC;

    private readonly byte[] Memory = new byte[0x10000];
    private ConditionCodes Cc;

    private Intel8008(byte[] memory)
    {
        Array.Clear(Memory);
        InitRegisters();
        inPorts = Enumerable.Repeat((int port) => Ports[port], 256).ToArray();
        outPorts = Enumerable.Repeat((int port, byte value) => { Ports[port] = value; }, 256).ToArray();
        LoadMemory(memory, 0);
    }

    public Intel8008 LoadMemory(string filePath, int offset)
    {
        return LoadMemory(File.ReadAllBytes(filePath), offset);
    }

    public Intel8008 LoadMemory(byte[] memory, int offset)
    {
        memory.CopyTo(Memory, offset);
        return this;
    }

    private void InitRegisters()
    {
        A = 0;
        BC = 0;
        DE = 0;
        HL = 0;
        SP = 0;
        PC = 0;
        Cc.Init();
    }

    public string Disassemble(ushort start, ushort end)
    {
        var i = start;
        var res = new StringBuilder();
        var cyc = (uint)0;
        while (i < end)
        {
            res.AppendLine($"0x{i:X}: {Disassemble(i, out var offset, ref cyc)}");
            i += (ushort)offset;
        }

        return res.ToString();
    }

    private string Disassemble(ushort pos, out short offset, ref uint cyc)
    {
        return Disassemble(pos, Memory, out offset, ref cyc);
    }

    public static string Disassemble(ushort pos, byte[] mem, out short offset, ref uint cyc)
    {
        var res = $"NOT IMPLEMENTED: 0x{mem[pos]:X} - 0b{mem[pos]:B}";
        offset = 0;
        switch ((mem[pos] & 0b11000000) >> 6)
        {
            case 0b00:
                switch (mem[pos] & 0b00111111)
                {
                    case 0b00000000:
                        res = "NOP";
                        cyc += 4;
                        break;
                    case 0b00000111:
                        cyc += 4;
                        res = "RLC // Cy = A >> 7; A = A << 1; A = (A & 0b11111110) | Cy";
                        break;
                    case 0b00001111:
                        cyc += 4;
                        res = "RRC // Cy = A & 0b1; A = A >> 1; A = (A & 0b01111111) | (Cy << 7)";
                        break;
                    case 0b00010111:
                        cyc += 4;
                        res = "RAL // tmp = A >> 7; A = A << 1; A = (A & 0b11111110) | Cy; Cy = tmp";
                        break;
                    case 0b00011111:
                        cyc += 4;
                        res = "RAR // tmp = A & 0b1; A = A >> 1; A = (A & 0b01111111) | (Cy << 7); Cy = tmp";
                        break;
                    case 0b00100010:
                    {
                        cyc += 16;
                        var addr = (mem[pos + 2] << 8) | mem[pos + 1];
                        offset += 2;
                        res = $"SHLD 0x{addr:X4} // Memory[0x{addr:X4}] = HL";
                    }
                        break;
                    case 0b00100111:
                    {
                        cyc += 4;
                        res = "DAA // A = ((A & 0b1111) > 9 || Ac ) ? A+6 : A; A = ((A >> 4) > 9 || Cy) ? A + 0x60 : A";
                    }
                        break;
                    case 0b00101010:
                    {
                        cyc += 13;
                        var addr = (mem[pos + 2] << 8) | mem[pos + 1];
                        offset += 2;
                        res = $"LHLD 0x{addr:X4} // HL = Memory[0x{addr:X4}]";
                    }
                        break;
                    case 0b00101111:
                    {
                        cyc += 4;
                        res = "CMA // A = ~A";
                    }
                        break;
                    case 0b00110010:
                    {
                        cyc += 13;
                        var addr = (mem[pos + 2] << 8) | mem[pos + 1];
                        offset += 2;
                        res = $"STA 0x{addr:X4} // Memory[0x{addr:X4}] = A";
                    }
                        break;
                    case 0b00110111:
                    {
                        cyc += 4;
                        res = "STC // Cy = 1";
                    }
                        break;
                    case 0b00111010:
                    {
                        cyc += 13;
                        var addr = (mem[pos + 2] << 8) | mem[pos + 1];
                        offset += 2;
                        res = $"LDA 0x{addr:X4} // A = Memory[0x{addr:X4}]";
                    }
                        break;
                    case 0b00111111:
                    {
                        cyc += 4;
                        res = "CMC // Cy = ~Cy";
                    }
                        break;
                    default:
                        switch (mem[pos] & 0b1111)
                        {
                            case 0b0001:
                            {
                                cyc += 10;
                                var rp = "";
                                var data = (mem[pos + 2] << 8) | mem[pos + 1];
                                offset += 2;
                                switch ((Rp)((mem[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        rp = "BC";
                                        break;
                                    case Rp.DE:
                                        rp = "DE";
                                        break;
                                    case Rp.HL:
                                        rp = "HL";
                                        break;
                                    case Rp.SP:
                                        rp = "SP";
                                        break;
                                }

                                res = $"LXI {rp}, 0x{data:X4} // {rp} = 0x{data:X4}";
                            }
                                break;
                            case 0b0010:
                            {
                                cyc += 7;
                                var rp = "";
                                switch ((Rp)((mem[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        rp = "BC";
                                        break;
                                    case Rp.DE:
                                        rp = "DE";
                                        break;
                                    case Rp.HL:
                                        rp = "INVALID_REGISTER -> HL";
                                        break;
                                    case Rp.SP:
                                        rp = "INVALID_REGISTER -> SP";
                                        break;
                                }

                                res = $"STAX {rp} // {rp} = A";
                            }
                                break;
                            case 0b0011:
                            {
                                cyc += 5;
                                var rp = "";
                                switch ((Rp)((mem[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        rp = "BC";
                                        break;
                                    case Rp.DE:
                                        rp = "DE";
                                        break;
                                    case Rp.HL:
                                        rp = "HL";
                                        break;
                                    case Rp.SP:
                                        rp = "SP";
                                        break;
                                }

                                res = $"INX {rp} // {rp} += 1";
                            }
                                break;
                            case 0b1001:
                            {
                                cyc += 10;
                                var rp = "";
                                switch ((Rp)((mem[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        rp = "BC";
                                        break;
                                    case Rp.DE:
                                        rp = "DE";
                                        break;
                                    case Rp.HL:
                                        rp = "HL";
                                        break;
                                    case Rp.SP:
                                        rp = "SP";
                                        break;
                                }

                                res = $"DAD {rp} // HL += {rp}";
                            }
                                break;
                            case 0b1010:
                            {
                                cyc += 7;
                                var rp = "";
                                switch ((Rp)((mem[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        rp = "BC";
                                        break;
                                    case Rp.DE:
                                        rp = "DE";
                                        break;
                                    case Rp.HL:
                                        rp = "INVALID_REGISTER -> HL";
                                        break;
                                    case Rp.SP:
                                        rp = "INVALID_REGISTER -> SP";
                                        break;
                                }

                                res = $"LDAX {rp} // A = Memory[{rp}]";
                            }
                                break;
                            case 0b1011:
                            {
                                cyc += 5;
                                var rp = "";
                                switch ((Rp)((mem[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        rp = "BC";
                                        break;
                                    case Rp.DE:
                                        rp = "DE";
                                        break;
                                    case Rp.HL:
                                        rp = "HL";
                                        break;
                                    case Rp.SP:
                                        rp = "SP";
                                        break;
                                }

                                res = $"DCX {rp} // {rp} -= 1";
                            }
                                break;
                            default:
                                switch (mem[pos] & 0b111)
                                {
                                    case 0b100:
                                    {
                                        var ddd = "";
                                        switch ((Reg)((mem[pos] >> 3) & 0b111))
                                        {
                                            case Reg.B:
                                                cyc += 9;
                                                ddd = "B";
                                                break;
                                            case Reg.C:
                                                cyc += 5;
                                                ddd = "C";
                                                break;
                                            case Reg.D:
                                                cyc += 9;
                                                ddd = "D";
                                                break;
                                            case Reg.E:
                                                cyc += 5;
                                                ddd = "E";
                                                break;
                                            case Reg.H:
                                                cyc += 9;
                                                ddd = "H";
                                                break;
                                            case Reg.L:
                                                cyc += 5;
                                                ddd = "L";
                                                break;
                                            case Reg.M:
                                                cyc += 10;
                                                ddd = $"Memory[HL]";
                                                break;
                                            case Reg.A:
                                                cyc += 5;
                                                ddd = "A";
                                                break;
                                        }

                                        res = $"INR {ddd} // {ddd} += 1";
                                    }
                                        break;
                                    case 0b101:
                                    {
                                        var ddd = "";
                                        switch ((Reg)((mem[pos] >> 3) & 0b111))
                                        {
                                            case Reg.B:
                                                cyc += 9;
                                                ddd = "B";
                                                break;
                                            case Reg.C:
                                                cyc += 5;
                                                ddd = "C";
                                                break;
                                            case Reg.D:
                                                cyc += 9;
                                                ddd = "D";
                                                break;
                                            case Reg.E:
                                                cyc += 5;
                                                ddd = "E";
                                                break;
                                            case Reg.H:
                                                cyc += 9;
                                                ddd = "H";
                                                break;
                                            case Reg.L:
                                                cyc += 5;
                                                ddd = "L";
                                                break;
                                            case Reg.M:
                                                cyc += 10;
                                                ddd = "Memory[HL]";
                                                break;
                                            case Reg.A:
                                                cyc += 5;
                                                ddd = "A";
                                                break;
                                        }

                                        res = $"DCR {ddd} // {ddd} -= 1";
                                    }
                                        break;
                                    case 0b110:
                                    {
                                        var ddd = "";
                                        var data = mem[pos + 1];
                                        offset += 1;
                                        switch ((Reg)((mem[pos] >> 3) & 0b111))
                                        {
                                            case Reg.B:
                                                cyc += 9;
                                                ddd = "B";
                                                break;
                                            case Reg.C:
                                                cyc += 5;
                                                ddd = "C";
                                                break;
                                            case Reg.D:
                                                cyc += 9;
                                                ddd = "D";
                                                break;
                                            case Reg.E:
                                                cyc += 5;
                                                ddd = "E";
                                                break;
                                            case Reg.H:
                                                cyc += 9;
                                                ddd = "H";
                                                break;
                                            case Reg.L:
                                                cyc += 5;
                                                ddd = "L";
                                                break;
                                            case Reg.M:
                                                cyc += 10;
                                                ddd = "Memory[HL]";
                                                break;
                                            case Reg.A:
                                                cyc += 5;
                                                ddd = "A";
                                                break;
                                        }

                                        res = $"MVI {ddd}, 0x{data:X2} // {ddd} = 0x{data:X2}";
                                    }
                                        break;
                                }

                                break;
                        }

                        break;
                }

                break;
            case 0b01:
                switch (mem[pos])
                {
                    case 0b01110110:
                        cyc += 7;
                        res = "HLT // offset = -1 //wait until interrupt";
                        break;
                    default:
                    {
                        cyc += 6; // 5-7
                        var src = "";
                        var dest = "";
                        switch ((Reg)(mem[pos] & 0b111))
                        {
                            case Reg.B:
                                src = "B";
                                break;
                            case Reg.C:
                                src = "C";
                                break;
                            case Reg.D:
                                src = "D";
                                break;
                            case Reg.E:
                                src = "E";
                                break;
                            case Reg.H:
                                src = "H";
                                break;
                            case Reg.L:
                                src = "L";
                                break;
                            case Reg.M:
                                src = "M";
                                break;
                            case Reg.A:
                                src = "A";
                                break;
                        }

                        switch ((Reg)((mem[pos] >> 3) & 0b111))
                        {
                            case Reg.B:
                                dest = "B";
                                break;
                            case Reg.C:
                                dest = "C";
                                break;
                            case Reg.D:
                                dest = "D";
                                break;
                            case Reg.E:
                                dest = "E";
                                break;
                            case Reg.H:
                                dest = "H";
                                break;
                            case Reg.L:
                                dest = "L";
                                break;
                            case Reg.M:
                                dest = "M";
                                break;
                            case Reg.A:
                                dest = "A";
                                break;
                        }

                        res = $"MOV {dest}, {src} // {dest} = {src}";
                    }
                        break;
                }

                break;
            case 0b10:
            {
                cyc += 5; //4 - 7
                var src = "";
                switch ((Reg)(mem[pos] & 0b111))
                {
                    case Reg.B:
                        src = "B";
                        break;
                    case Reg.C:
                        src = "C";
                        break;
                    case Reg.D:
                        src = "D";
                        break;
                    case Reg.E:
                        src = "E";
                        break;
                    case Reg.H:
                        src = "H";
                        break;
                    case Reg.L:
                        src = "L";
                        break;
                    case Reg.M:
                        src = "M";
                        break;
                    case Reg.A:
                        src = "A";
                        break;
                }

                switch ((Alu)((mem[pos] >> 3) & 0b111))
                {
                    case Alu.ADD:
                        res = $"ADD {src} // A = A + {src};";
                        break;
                    case Alu.ADC:
                        res = $"ADC {src} // A = A + {src} + Cy";
                        break;
                    case Alu.SUB:
                        res = $"SUB {src} // A = A - {src}";
                        break;
                    case Alu.SBB:
                        res = $"SBB {src} // A = A - {src} - Cy";
                        break;
                    case Alu.ANA:
                        res = $"ANA {src} // A = A & {src}";
                        break;
                    case Alu.XRA:
                        res = $"XRA {src} // A = A ^ {src}";
                        break;
                    case Alu.ORA:
                        res = $"ORA {src} // A = A | {src}";
                        break;
                    case Alu.CMP:
                        res = $"CMP {src} // A = A - {src}";
                        break;
                }
            }
                break;
            case 0b11:
                switch (mem[pos])
                {
                    case 0b11111011:
                        cyc += 4;
                        res = "EI // Enable interrupts";
                        break;
                    case 0b11111001:
                        res = "SPHL // SP = HL";
                        cyc += 5;
                        break;
                    case 0b11110011:
                        cyc += 4;
                        res = "DI // Disable interrupts";
                        break;
                    case 0b11101011:
                        cyc += 4;
                        res = "XCHG // tmp = HL; HL = DE; DE = tmp;";
                        break;
                    case 0b11101001:
                        cyc += 5;
                        res = "PCHL // PC = HL";
                        break;
                    case 0b11100011:
                        cyc += 18;
                        res =
                            "XTHL // tmp = HL; HL = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); Memory[SP+1] = (byte)(tmp>>8); Memory[SP] = (byte)(tmp&0xFF);";
                        break;
                    case 0b11011011:
                    {
                        cyc += 10;
                        var port = mem[pos + 1];
                        offset += 1;
                        res = $"IN 0x{port:X} // A = port;";
                    }
                        break;
                    case 0b11010011:
                    {
                        cyc += 10;
                        var port = mem[pos + 1];
                        offset += 1;
                        res = $"OUT 0x{port:X} // port = A;";
                    }
                        break;
                    case 0b11001101:
                    {
                        cyc += 17;
                        var addr = (short)((mem[pos + 2] << 8) | mem[pos + 1]);
                        offset += 2;
                        res =
                            $"CALL 0x{addr:X4} // SP -= 2; Memory[SP] = (byte)(PC&0xFF); Memory[SP+1] = (byte)((PC>>8)&0xFF); PC = 0x{addr:X}";
                    }
                        break;
                    case 0b11001001:
                        cyc += 10;
                        res = "RET // PC = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); SP += 2;";
                        break;
                    case 0b11000011:
                    {
                        cyc += 10;
                        var addr = (short)((mem[pos + 2] << 8) | mem[pos + 1]);
                        offset += 2;
                        res = $"JMP 0x{addr:X4} // PC = 0x{addr:X4}";
                    }
                        break;
                    default:
                        switch (mem[pos] & 0b111)
                        {
                            case 0b000:
                            {
                                cyc += 10; // 5 -  11
                                switch ((CompareCondition)((mem[pos] >> 3) & 0b111))
                                {
                                    case CompareCondition.NZ:
                                        res =
                                            "RNZ // if(!Cc.Z){PC = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); SP += 2;}";
                                        break;
                                    case CompareCondition.Z:
                                        res =
                                            "RZ // if(Cc.Z){PC = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); SP += 2;}";
                                        break;
                                    case CompareCondition.NC:
                                        res =
                                            "RNC // if(!Cc.Cy){PC = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); SP += 2;}";
                                        break;
                                    case CompareCondition.C:
                                        res =
                                            "RC // if(Cc.Cy){PC = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); SP += 2;}";
                                        break;
                                    case CompareCondition.PO:
                                        res =
                                            "RPO // if(!Cc.P){PC = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); SP += 2;}";
                                        break;
                                    case CompareCondition.PE:
                                        res =
                                            "RPE // if(Cc.P){PC = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); SP += 2;}";
                                        break;
                                    case CompareCondition.P:
                                        res =
                                            "RP // if(!Cc.S){PC = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); SP += 2;}";
                                        break;
                                    case CompareCondition.N:
                                        res =
                                            "RN // if(Cc.S){PC = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); SP += 2;}";
                                        break;
                                }
                            }
                                break;
                            case 0b001:
                            {
                                var reg = "";
                                cyc += 10;
                                switch ((Rp)((mem[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        reg = "BC";
                                        break;
                                    case Rp.DE:
                                        reg = "DE";
                                        break;
                                    case Rp.HL:
                                        reg = "HL";
                                        break;
                                    case Rp.SP:
                                        reg = "PSW";
                                        break;
                                }

                                res = $"POP {reg} // {reg} = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); SP += 2;";
                            }
                                break;
                            case 0b010:
                            {
                                cyc += 10;
                                var addr = (short)((mem[pos + 2] << 8) | mem[pos + 1]);
                                offset += 2;
                                switch ((CompareCondition)((mem[pos] >> 3) & 0b111))
                                {
                                    case CompareCondition.NZ:
                                        res =
                                            $"JNZ 0x{addr:X4} // if(!Cc.Z){{PC = 0x{addr:X4};}}";
                                        break;
                                    case CompareCondition.Z:
                                        res = $"JZ 0x{addr:X4} // if(Cc.Z){{PC = 0x{addr:X4};}}";
                                        break;
                                    case CompareCondition.NC:
                                        res =
                                            $"JNC 0x{addr:X4} // if(!Cc.Cy){{PC = 0x{addr:X4};}}";
                                        break;
                                    case CompareCondition.C:
                                        res = $"JC 0x{addr:X4} // if(Cc.Cy){{PC = 0x{addr:X4};}}";
                                        break;
                                    case CompareCondition.PO:
                                        res = $"JPO 0x{addr:X4} // if(!Cc.P){{PC = 0x{addr:X4};}}";
                                        break;
                                    case CompareCondition.PE:
                                        res = $"JPE 0x{addr:X4} // if(Cc.P){{PC = 0x{addr:X4};}}";
                                        break;
                                    case CompareCondition.P:
                                        res = $"JP 0x{addr:X4} // if(Cc.S){{PC = 0x{addr:X4};}}";
                                        break;
                                    case CompareCondition.N:
                                        res = $"JN 0x{addr:X4} // if(!Cc.S){{PC = 0x{addr:X4};}}";
                                        break;
                                }
                            }
                                break;
                            case 0b100:
                            {
                                cyc += 15; // 11 - 17
                                var addr = (short)((mem[pos + 2] << 8) | mem[pos + 1]);
                                offset += 2;
                                switch ((CompareCondition)((mem[pos] >> 3) & 0b111))
                                {
                                    case CompareCondition.NZ:
                                        res =
                                            $"CNZ 0x{addr:X}// if(!Cc.Z){{SP -= 2; Memory[SP] = (byte)(PC&0xFF); Memory[SP+1] = (byte)((PC>>8)&0xFF); PC = 0x{addr:X}}}";
                                        break;
                                    case CompareCondition.Z:
                                        res =
                                            $"CZ 0x{addr:X}// if(Cc.Z){{SP -= 2; Memory[SP] = (byte)(PC&0xFF); Memory[SP+1] = (byte)((PC>>8)&0xFF); PC = 0x{addr:X}}}";
                                        break;
                                    case CompareCondition.NC:
                                        res =
                                            $"CNC 0x{addr:X}// if(!Cc.Cy){{SP -= 2; Memory[SP] = (byte)(PC&0xFF); Memory[SP+1] = (byte)((PC>>8)&0xFF); PC = 0x{addr:X}}}";
                                        break;
                                    case CompareCondition.C:
                                        res =
                                            $"CC 0x{addr:X}// if(Cc.Cy){{SP -= 2; Memory[SP] = (byte)(PC&0xFF); Memory[SP+1] = (byte)((PC>>8)&0xFF); PC = 0x{addr:X}}}";
                                        break;
                                    case CompareCondition.PO:
                                        res =
                                            $"CPO 0x{addr:X}// if(!Cc.P){{SP -= 2; Memory[SP] = (byte)(PC&0xFF); Memory[SP+1] = (byte)((PC>>8)&0xFF); PC = 0x{addr:X}}}";
                                        break;
                                    case CompareCondition.PE:
                                        res =
                                            $"CPE 0x{addr:X}// if(Cc.P){{SP -= 2; Memory[SP] = (byte)(PC&0xFF); Memory[SP+1] = (byte)((PC>>8)&0xFF); PC = 0x{addr:X}}}";
                                        break;
                                    case CompareCondition.P:
                                        res =
                                            $"CP 0x{addr:X}// if(Cc.S){{SP -= 2; Memory[SP] = (byte)(PC&0xFF); Memory[SP+1] = (byte)((PC>>8)&0xFF); PC = 0x{addr:X}}}";
                                        break;
                                    case CompareCondition.N:
                                        res =
                                            $"CN 0x{addr:X}// if(!Cc.S){{SP -= 2; Memory[SP] = (byte)(PC&0xFF); Memory[SP+1] = (byte)((PC>>8)&0xFF); PC = 0x{addr:X}}}";
                                        break;
                                }
                            }
                                break;
                            case 0b101:
                            {
                                cyc += 11;
                                var reg = (Rp)((mem[pos] >> 4) & 0b11) switch
                                {
                                    Rp.BC => "BC",
                                    Rp.DE => "DE",
                                    Rp.HL => "HL",
                                    Rp.SP => "PSW",
                                    _ => ""
                                };

                                res =
                                    $"PUSH {reg} // SP -= 2; Memory[SP] = (byte)({reg}&0xFF); Memory[SP+1] = (byte)(({reg}>>8)&0xFF);";
                            }
                                break;
                            case 0b110:
                            {
                                cyc += 7; //4 - 7
                                var data = mem[pos + 1];
                                offset += 1;

                                switch ((Alu)((mem[pos] >> 3) & 0b111))
                                {
                                    case Alu.ADD:
                                        res = $"ADI 0x{data:X2} // A = A + 0x{data:X2};";
                                        break;
                                    case Alu.ADC:
                                        res = $"ACI 0x{data:X2} // A = A + 0x{data:X2} + Cy";
                                        break;
                                    case Alu.SUB:
                                        res = $"SUI 0x{data:X2} // A = A - 0x{data:X2}";
                                        break;
                                    case Alu.SBB:
                                        res = $"SBI 0x{data:X2} // A = A - 0x{data:X2} - Cy";
                                        break;
                                    case Alu.ANA:
                                        res = $"ANI 0x{data:X2} // A = A & 0x{data:X2}";
                                        break;
                                    case Alu.XRA:
                                        res = $"XRI 0x{data:X2} // A = A ^ 0x{data:X2}";
                                        break;
                                    case Alu.ORA:
                                        res = $"ORI 0x{data:X2} // A = A | 0x{data:X2}";
                                        break;
                                    case Alu.CMP:
                                        res = $"CPI 0x{data:X2} // A = A - 0x{data:X2}";
                                        break;
                                }
                            }
                                break;
                            case 0b111:
                            {
                                cyc += 11;
                                var v = (mem[pos] >> 3) & 0b111;
                                res =
                                    $"RST {v} // SP -= 2; Memory[SP] = (byte)(PC&0xFF); Memory[SP+1] = (byte)((PC>>8)&0xFF); PC = {v * 8};";
                            }
                                break;
                        }

                        break;
                }

                break;
        }

        offset++;
        return res;
    }

    public bool run(uint numCycles = 1, bool cpudiag = false, bool safe = false, bool print_debug = false)
    {
        var startCycles = cycles;
        short offset;
        StringBuilder? cout = null;
        if (print_debug)
        {
            cout = new StringBuilder();
        }

        var u = (cycles - startCycles);
        do
        {
            u = (cycles - startCycles);
            if (print_debug)
            {
                cout!.AppendLine(GetCurrentInstrAsString());
            }

            if (safe)
            {
                ExecuteSafe(PC, out offset, cpudiag);
            }
            else
            {
                Execute(PC, out offset, cpudiag);
            }

            if (!JmpWasExecuted)
            {
                PC += (ushort)offset;
            }
        } while (offset != 0 && (cycles - startCycles) <= numCycles);

        if (print_debug)
        {
            Console.Out.WriteLine(cout!.ToString());
        }

        return offset != 0;
    }

    private void Execute(ushort pos, out short offset, bool cpudiag = false)
    {
        offset = 0;
        ExecuteTick(pos, ref offset, cpudiag);
        offset++;
        iterations++;
    }

    private void ExecuteSafe(ushort pos, out short offset, bool cpudiag = false)
    {
        if (willFail)
        {
            PopState();
            uint c = 0;
            Console.Out.WriteLine($"Failed at 0x{PC:X}\n{Disassemble(PC, Memory, out _, ref c)}");
            Console.Out.WriteLine("Add breakpoint HERE to debug");
            willFail = false;
        }

        PushState();
        offset = 0;
        try
        {
            ExecuteTick(pos, ref offset, cpudiag);
            offset++;
            iterations++;
        }
        catch
        {
            offset = 1;
            willFail = true;
        }
    }

    private void ExecuteTick(ushort pos, ref short offset, bool cpudiag = false)
    {
        JmpWasExecuted = false;
        switch ((Memory[pos] & 0b11000000) >> 6)
        {
            case 0b00:
                switch (Memory[pos] & 0b00111111)
                {
                    case 0b00000000:
                        cycles += 4;
                        break;
                    case 0b00000111:
                        cycles += 4;
                        Cy = (byte)(A >> 7);
                        A <<= 1;
                        A = (byte)((A & 0b11111110) | Cy);
                        break;
                    case 0b00001111:
                        cycles += 4;
                        Cy = (byte)(A & 0b1);
                        A >>= 1;
                        A = (byte)((A & 0b01111111) | (Cy << 7));
                        break;
                    case 0b00010111:
                    {
                        cycles += 4;
                        var tmp = (byte)(A >> 7);
                        A <<= 1;
                        A = (byte)((A & 0b11111110) | Cy);
                        Cy = tmp;
                    }
                        break;
                    case 0b00011111:
                    {
                        cycles += 4;
                        var tmp = (byte)(A & 0b1);
                        A >>= 1;
                        A = (byte)((A & 0b01111111) | (Cy << 7));
                        Cy = tmp;
                    }
                        break;
                    case 0b00100010:
                    {
                        cycles += 16;
                        var addr = LoadShortFromMemory(pos + 1);
                        offset += 2;
                        WriteToMemory(addr, HL);
                    }
                        break;
                    case 0b00100111:
                    {
                        cycles += 4;
                        A = (A & 0b1111) > 9 || Cc.Ac ? (byte)(A + 6) : A;
                        A = A >> 4 > 9 || Cc.Cy ? (byte)(A + 0x60) : A;
                    }
                        break;
                    case 0b00101010:
                    {
                        cycles += 13;
                        var addr = LoadShortFromMemory(pos + 1);
                        offset += 2;
                        HL = LoadShortFromMemory(addr);
                    }
                        break;
                    case 0b00101111:
                    {
                        cycles += 4;
                        A = (byte)~A;
                    }
                        break;
                    case 0b00110010:
                    {
                        cycles += 13;
                        var addr = LoadShortFromMemory(pos + 1);
                        offset += 2;
                        WriteToMemory(addr, A);
                    }
                        break;
                    case 0b00110111:
                    {
                        cycles += 4;
                        Cy = 1;
                    }
                        break;
                    case 0b00111010:
                    {
                        cycles += 13;
                        var addr = LoadShortFromMemory(pos + 1);
                        offset += 2;
                        A = LoadByteFromMemory(addr);
                    }
                        break;
                    case 0b00111111:
                    {
                        cycles += 4;
                        Cc.Cy = !Cc.Cy;
                    }
                        break;
                    default:
                        switch (Memory[pos] & 0b1111)
                        {
                            case 0b0001:
                            {
                                cycles += 10;
                                var data = LoadShortFromMemory(pos + 1);
                                offset += 2;
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        BC = data;
                                        break;
                                    case Rp.DE:
                                        DE = data;
                                        break;
                                    case Rp.HL:
                                        HL = data;
                                        break;
                                    case Rp.SP:
                                        SP = data;
                                        break;
                                }
                            }
                                break;
                            case 0b0010:
                            {
                                cycles += 7;
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        WriteToMemory(BC, A);
                                        break;
                                    case Rp.DE:
                                        WriteToMemory(DE, A);
                                        break;
                                    case Rp.HL:
                                        throw new UnreachableException("INVALID_REGISTER -> HL");
                                    case Rp.SP:
                                        throw new UnreachableException("INVALID_REGISTER -> SP");
                                }
                            }
                                break;
                            case 0b0011:
                            {
                                cycles += 5;
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        BC = AluAddX(BC, 1, false, Flags.NONE);
                                        break;
                                    case Rp.DE:
                                        DE = AluAddX(DE, 1, false, Flags.NONE);
                                        break;
                                    case Rp.HL:
                                        HL = AluAddX(HL, 1, false, Flags.NONE);
                                        break;
                                    case Rp.SP:
                                        SP = AluAddX(SP, 1, false, Flags.NONE);
                                        break;
                                }
                            }
                                break;
                            case 0b1001:
                            {
                                cycles += 10;
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        HL = AluAddX(HL, BC, true, Flags.CY);
                                        break;
                                    case Rp.DE:
                                        HL = AluAddX(HL, DE, true, Flags.CY);
                                        break;
                                    case Rp.HL:
                                        HL = AluAddX(HL, HL, true, Flags.CY);
                                        break;
                                    case Rp.SP:
                                        HL = AluAddX(HL, SP, true, Flags.CY);
                                        break;
                                }
                            }
                                break;
                            case 0b1010:
                            {
                                cycles += 7;
                                var addr = (Rp)((Memory[pos] >> 4) & 0b11) switch
                                {
                                    Rp.BC => BC,
                                    Rp.DE => DE,
                                    Rp.HL => throw new UnreachableException("INVALID_REGISTER -> HL"),
                                    Rp.SP => throw new UnreachableException("INVALID_REGISTER -> SP"),
                                    _ => throw new UnreachableException("INVALID_REGISTER")
                                };
                                A = LoadByteFromMemory(addr);
                            }
                                break;
                            case 0b1011:
                            {
                                cycles += 5;
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        BC = AluSubX(BC, 1, false, Flags.NONE);
                                        break;
                                    case Rp.DE:
                                        DE = AluSubX(DE, 1, false, Flags.NONE);
                                        break;
                                    case Rp.HL:
                                        HL = AluSubX(HL, 1, false, Flags.NONE);
                                        break;
                                    case Rp.SP:
                                        SP = AluSubX(SP, 1, false, Flags.NONE);
                                        break;
                                }
                            }
                                break;
                            default:
                                switch (Memory[pos] & 0b111)
                                {
                                    case 0b100:
                                    {
                                        switch ((Reg)((Memory[pos] >> 3) & 0b111))
                                        {
                                            case Reg.B:
                                                cycles += 9;
                                                B = AluAdd(B, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.C:
                                                cycles += 5;
                                                C = AluAdd(C, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.D:
                                                cycles += 9;
                                                D = AluAdd(D, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.E:
                                                cycles += 5;
                                                E = AluAdd(E, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.H:
                                                cycles += 9;
                                                H = AluAdd(H, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.L:
                                                cycles += 5;
                                                L = AluAdd(L, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.M:
                                                cycles += 10;
                                                M = AluAdd(M, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.A:
                                                cycles += 5;
                                                A = AluAdd(A, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                        }
                                    }
                                        break;
                                    case 0b101:
                                    {
                                        switch ((Reg)((Memory[pos] >> 3) & 0b111))
                                        {
                                            case Reg.B:
                                                cycles += 9;
                                                B = AluSub(B, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.C:
                                                cycles += 5;
                                                C = AluSub(C, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.D:
                                                cycles += 9;
                                                D = AluSub(D, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.E:
                                                cycles += 5;
                                                E = AluSub(E, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.H:
                                                cycles += 9;
                                                H = AluSub(H, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.L:
                                                cycles += 5;
                                                L = AluSub(L, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.M:
                                                cycles += 10;
                                                M = AluSub(M, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                            case Reg.A:
                                                cycles += 5;
                                                A = AluSub(A, 1, true, Flags.Z | Flags.S | Flags.P | Flags.AC);
                                                break;
                                        }
                                    }
                                        break;
                                    case 0b110:
                                    {
                                        var data = LoadByteFromMemory(pos + 1);
                                        offset += 1;
                                        switch ((Reg)((Memory[pos] >> 3) & 0b111))
                                        {
                                            case Reg.B:
                                                cycles += 9;
                                                B = data;
                                                break;
                                            case Reg.C:
                                                cycles += 5;
                                                C = data;
                                                break;
                                            case Reg.D:
                                                cycles += 9;
                                                D = data;
                                                break;
                                            case Reg.E:
                                                cycles += 5;
                                                E = data;
                                                break;
                                            case Reg.H:
                                                cycles += 9;
                                                H = data;
                                                break;
                                            case Reg.L:
                                                cycles += 5;
                                                L = data;
                                                break;
                                            case Reg.M:
                                                cycles += 10;
                                                M = data;
                                                break;
                                            case Reg.A:
                                                cycles += 5;
                                                A = data;
                                                break;
                                        }
                                    }
                                        break;
                                    default:
                                        throw new InvalidConstraintException(
                                            $"invalid instruction 0x{Memory[pos]:X}");
                                }

                                break;
                        }

                        break;
                }

                break;
            case 0b01:
                switch (Memory[pos])
                {
                    case 0b01110110:
                        cycles += 7;
                        offset = -1;
                        JmpWasExecuted = true;
                        break;
                    default:
                    {
                        cycles += 6; // 5-7
                        var v = (Reg)(Memory[pos] & 0b111) switch
                        {
                            Reg.B => B,
                            Reg.C => C,
                            Reg.D => D,
                            Reg.E => E,
                            Reg.H => H,
                            Reg.L => L,
                            Reg.M => M,
                            Reg.A => A,
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        switch ((Reg)((Memory[pos] >> 3) & 0b111))
                        {
                            case Reg.B:
                                B = v;
                                break;
                            case Reg.C:
                                C = v;
                                break;
                            case Reg.D:
                                D = v;
                                break;
                            case Reg.E:
                                E = v;
                                break;
                            case Reg.H:
                                H = v;
                                break;
                            case Reg.L:
                                L = v;
                                break;
                            case Reg.M:
                                M = v;
                                break;
                            case Reg.A:
                                A = v;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                        break;
                }

                break;
            case 0b10:
            {
                cycles += 5; //4 - 7
                var v = (Reg)(Memory[pos] & 0b111) switch
                {
                    Reg.B => B,
                    Reg.C => C,
                    Reg.D => D,
                    Reg.E => E,
                    Reg.H => H,
                    Reg.L => L,
                    Reg.M => M,
                    Reg.A => A,
                    _ => throw new UnreachableException()
                };

                switch ((Alu)((Memory[pos] >> 3) & 0b111))
                {
                    case Alu.ADD:
                        A = AluAdd(A, v, true);
                        break;
                    case Alu.ADC:
                        A = AluAdd(A, v, true, Flags.ALL, Cc.Cy);
                        break;
                    case Alu.SUB:
                        A = AluSub(A, v, true);
                        break;
                    case Alu.SBB:
                        A = AluSub(A, v, true, Flags.ALL, Cc.Cy);
                        break;
                    case Alu.ANA:
                        A = AluAnd(A, v);
                        break;
                    case Alu.XRA:
                        A = AluXor(A, v);
                        break;
                    case Alu.ORA:
                        A = AluOr(A, v);
                        break;
                    case Alu.CMP:
                        AluCmp(A, v);
                        break;
                    default:
                        break;
                }
            }
                break;
            case 0b11:
                switch (Memory[pos])
                {
                    case 0b11111011:
                        cycles += 4;
                        EnableInterrupts();
                        break;
                    case 0b11111001:
                        cycles += 5;
                        SP = HL;
                        break;
                    case 0b11110011:
                        cycles += 4;
                        DisableInterrupts();
                        break;
                    case 0b11101011:
                    {
                        cycles += 4;
                        (HL, DE) = (DE, HL);
                    }
                        break;
                    case 0b11101001:
                        cycles += 5;
                        PC = HL;
                        JmpWasExecuted = true;
                        break;
                    case 0b11100011:
                    {
                        cycles += 18;
                        var tmp = HL;
                        HL = LoadShortFromMemory(SP);
                        WriteToMemory(SP, tmp);
                    }
                        break;
                    case 0b11011011:
                    {
                        cycles += 10;
                        var port = LoadByteFromMemory(pos + 1);
                        offset += 1;
                        A = inPorts[port](port);
                    }
                        break;
                    case 0b11010011:
                    {
                        cycles += 10;
                        var port = LoadByteFromMemory(pos + 1);
                        offset += 1;
                        outPorts[port](port, A);
                    }
                        break;
                    case 0b11001101:
                    {
                        cycles += 17;
                        var addr = LoadShortFromMemory(pos + 1);
                        offset += 2;
                        switch (cpudiag)
                        {
                            case true when addr == 5:
                                switch (C)
                                {
                                    case 9:
                                    {
                                        var offs = DE;
                                        var builder = new StringBuilder();
                                        for (var i = offs + 3; (char)Memory[i] != '$'; i++)
                                        {
                                            builder.Append((char)Memory[i]);
                                        }

                                        Console.Out.WriteLine(builder.ToString());
                                        if (builder.ToString().Contains("CPU IS OPERATIONAL"))
                                        {
                                            offset = -1;
                                        }

                                        break;
                                    }
                                    case 2:
                                        Console.Out.WriteLine("print char routine called");
                                        break;
                                }

                                break;
                            case true when addr == 0:
                                Console.Out.WriteLine("Exiting cpudiag");
                                offset = -1;
                                break;
                            default:
                                SP -= 2;
                                WriteToMemory(SP, (ushort)(PC + 3));
                                PC = addr;
                                JmpWasExecuted = true;
                                break;
                        }
                    }
                        break;
                    case 0b11001001:
                        cycles += 10;
                        PC = LoadShortFromMemory(SP);
                        JmpWasExecuted = true;
                        SP += 2;
                        break;
                    case 0b11000011:
                    {
                        cycles += 10;
                        var addr = LoadShortFromMemory(pos + 1);
                        offset += 2;
                        PC = addr;
                        JmpWasExecuted = true;
                    }
                        break;
                    default:
                        switch (Memory[pos] & 0b111)
                        {
                            case 0b000:
                            {
                                cycles += 10; // 5 -  11
                                var cond = (CompareCondition)((Memory[pos] >> 3) & 0b111) switch
                                {
                                    CompareCondition.NZ => !Cc.Z,
                                    CompareCondition.Z => Cc.Z,
                                    CompareCondition.NC => !Cc.Cy,
                                    CompareCondition.C => Cc.Cy,
                                    CompareCondition.PO => !Cc.P,
                                    CompareCondition.PE => Cc.P,
                                    CompareCondition.P => !Cc.S,
                                    CompareCondition.N => Cc.S,
                                    _ => throw new InvalidConstraintException("invalid cmp instruction")
                                };
                                if (cond)
                                {
                                    PC = LoadShortFromMemory(SP);
                                    JmpWasExecuted = true;
                                    SP += 2;
                                }
                            }
                                break;
                            case 0b001:
                            {
                                cycles += 10;
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        BC = LoadShortFromMemory(SP);
                                        break;
                                    case Rp.DE:
                                        DE = LoadShortFromMemory(SP);
                                        break;
                                    case Rp.HL:
                                        HL = LoadShortFromMemory(SP);
                                        break;
                                    case Rp.SP:
                                        PSW = LoadShortFromMemory(SP);
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }

                                SP += 2;
                            }
                                break;
                            case 0b010:
                            {
                                cycles += 10;
                                var addr = LoadShortFromMemory(pos + 1);
                                offset += 2;
                                var cond = (CompareCondition)((Memory[pos] >> 3) & 0b111) switch
                                {
                                    CompareCondition.NZ => !Cc.Z,
                                    CompareCondition.Z => Cc.Z,
                                    CompareCondition.NC => !Cc.Cy,
                                    CompareCondition.C => Cc.Cy,
                                    CompareCondition.PO => !Cc.P,
                                    CompareCondition.PE => Cc.P,
                                    CompareCondition.P => !Cc.S,
                                    CompareCondition.N => Cc.S,
                                    _ => throw new InvalidConstraintException("invalid cmp instruction")
                                };
                                if (cond)
                                {
                                    PC = addr;
                                    JmpWasExecuted = true;
                                }
                            }
                                break;
                            case 0b100:
                            {
                                cycles += 15; // 11 - 17
                                var addr = LoadShortFromMemory(pos + 1);
                                offset += 2;
                                var cond = (CompareCondition)((Memory[pos] >> 3) & 0b111) switch
                                {
                                    CompareCondition.NZ => !Cc.Z,
                                    CompareCondition.Z => Cc.Z,
                                    CompareCondition.NC => !Cc.Cy,
                                    CompareCondition.C => Cc.Cy,
                                    CompareCondition.PO => !Cc.P,
                                    CompareCondition.PE => Cc.P,
                                    CompareCondition.P => !Cc.S,
                                    CompareCondition.N => Cc.S,
                                    _ => throw new InvalidConstraintException("invalid cmp instruction")
                                };
                                if (cond)
                                {
                                    SP -= 2;
                                    WriteToMemory(SP, (ushort)(PC + 3));
                                    PC = addr;
                                    JmpWasExecuted = true;
                                }
                            }
                                break;
                            case 0b101:
                            {
                                cycles += 11;
                                var reg = (Rp)((Memory[pos] >> 4) & 0b11) switch
                                {
                                    Rp.BC => BC,
                                    Rp.DE => DE,
                                    Rp.HL => HL,
                                    Rp.SP => PSW,
                                    _ => throw new InvalidConstraintException("invalid register")
                                };
                                SP -= 2;
                                WriteToMemory(SP, reg);
                            }
                                break;
                            case 0b110:
                            {
                                cycles += 7; //4 - 7
                                var data = LoadByteFromMemory(pos + 1);
                                offset += 1;

                                switch ((Alu)((Memory[pos] >> 3) & 0b111))
                                {
                                    case Alu.ADD:
                                        A = AluAdd(A, data, true);
                                        break;
                                    case Alu.ADC:
                                        A = AluAdd(A, data, true, Flags.ALL, Cc.Cy);
                                        break;
                                    case Alu.SUB:
                                        A = AluSub(A, data, true);
                                        break;
                                    case Alu.SBB:
                                        A = AluSub(A, data, true, Flags.ALL, Cc.Cy);
                                        break;
                                    case Alu.ANA:
                                        A = AluAnd(A, data);
                                        break;
                                    case Alu.XRA:
                                        A = AluXor(A, data);
                                        break;
                                    case Alu.ORA:
                                        A = AluOr(A, data);
                                        break;
                                    case Alu.CMP:
                                        AluCmp(A, data);
                                        break;
                                }
                            }
                                break;
                            case 0b111:
                            {
                                cycles += 11;
                                var v = (byte)((LoadByteFromMemory(pos) >> 3) & 0b111);
                                SP -= 2;
                                WriteToMemory(SP, PC);
                                PC = (ushort)(v * 8);
                                JmpWasExecuted = true;
                            }
                                break;
                            default: throw new InvalidConstraintException($"invalid instruction 0x{Memory[pos]:X}");
                        }

                        break;
                }

                break;
        }
    }

    private void EnableInterrupts()
    {
        SetPin(Pin.INTE, true);
        //Console.Out.WriteLine("Enable interrupts");
    }

    private void DisableInterrupts()
    {
        SetPin(Pin.INTE, false);
        //Console.Out.WriteLine("Disable interrupts");
    }

    private ushort PSW
    {
        get => (ushort)((A << 8) | Cc.GetAsValue());
        set
        {
            A = (byte)((value >> 8) & 0xFF);
            Cc.SetAsValue((byte)(value & 0xFF));
        }
    }

    public Intel8008(string pathToFile) : this(File.ReadAllBytes(pathToFile))
    {
    }

    private ushort AluAddX(ushort a, ushort b, bool setFlags, Flags flags = Flags.ALL, bool c = false)
    {
        var l = AluAdd((byte)(a & 0xFF), (byte)(b & 0xFF), setFlags, flags, c);
        var h = AluAdd((byte)((a >> 8) & 0xFF), (byte)((b >> 8) & 0xFF), setFlags, flags, l < (a & 0xFF));
        return (ushort)((h << 8) | l);
    }

    private ushort AluSubX(ushort a, ushort b, bool setFlags, Flags flags = Flags.ALL, bool c = false)
    {
        var tmp = Cc.Cy;
        var l = AluSub((byte)(a & 0xFF), (byte)(b & 0xFF), true, flags | Flags.CY);
        var h = AluSub((byte)((a >> 8) & 0xFF), (byte)((b >> 8) & 0xFF), setFlags, flags, Cc.Cy);
        if (!flags.HasFlag(Flags.CY))
        {
            Cc.Cy = tmp;
        }

        return (ushort)((h << 8) | l);
    }

    private byte AluAdd(byte a, byte b, bool setFlags, Flags flags = Flags.ALL, bool c = false)
    {
        var res = (ushort)(a + b + (c ? 1 : 0));
        if (setFlags)
        {
            SetArithFlags(res, flags);
        }

        Cc.Ac = (a & 0xF) + (b & 0xF) > 0xF;
        return (byte)(res & 0xFF);
    }

    private byte AluCmp(byte a, byte b) => AluSub(a, b, true);

    private byte AluSub(byte a, byte b, bool setFlags, Flags flags = Flags.ALL, bool c = false)
    {
        var res = (ushort)(a - b - (c ? 1 : 0));
        if (setFlags)
        {
            SetArithFlags(res, flags);
        }

        Cc.Ac = (a & 0xF) < (b & 0xF);
        return (byte)(res & 0xFF);
    }

    private byte AluAnd(byte a, byte b) => AluLogical((v1, v2) => (byte)(v1 & v2), a, b);
    private byte AluXor(byte a, byte b) => AluLogical((v1, v2) => (byte)(v1 ^ v2), a, b);
    private byte AluOr(byte a, byte b) => AluLogical((v1, v2) => (byte)(v1 | v2), a, b);

    private byte AluLogical(Func<byte, byte, byte> op, byte a, byte b)
    {
        var res = op(a, b);
        SetLogicFlags(res);
        return res;
    }

    private void SetLogicFlags(byte res)
    {
        Z = res;
        S = res;
        P = res;
        Cy = 0;
        Ac = 0;
    }

    private void SetArithFlags(ushort res, Flags flags)
    {
        if ((flags & Flags.Z) != 0)
        {
            Z = (byte)(res & 0xFF);
        }

        if ((flags & Flags.S) != 0)
        {
            S = (byte)(res & 0xFF);
        }

        if ((flags & Flags.P) != 0)
        {
            P = (byte)(res & 0xFF);
        }

        if ((flags & Flags.CY) != 0)
        {
            Cc.Cy = res > 0xFF;
        }
    }

    public void GenerateInterrupt(int num)
    {
        //Console.Out.WriteLine($"Int {num}");
        SP -= 2;
        WriteToMemory(SP, PC);
        PC = (ushort)(8 * num);
        DisableInterrupts();
    }

    public Intel8008() : this(Array.Empty<byte>())
    {
    }

    private void WriteToMemory(int addr, byte value)
    {
        Memory[(ushort)addr] = value;
        if (false)
        {
            switch (addr)
            {
                case < 0x2000:
                    Console.Out.WriteLine($"Writing ROM not allowed {addr}\n");
                    return;
                case >= 0x4000:
                    Console.Out.WriteLine($"Writing out of Space Invaders RAM not allowed {addr}\n");
                    return;
            }
        }
    }

    private void WriteToMemory(int addr, ushort value)
    {
        WriteToMemory(addr, (byte)(value & 0xFF));
        WriteToMemory(addr + 1, (byte)((value >> 8) & 0xFF));
    }

    private ushort LoadShortFromMemory(int addr)
    {
        return (ushort)((LoadByteFromMemory(addr + 1) << 8) | LoadByteFromMemory(addr));
    }

    private byte LoadByteFromMemory(int addr)
    {
        return Memory[addr];
    }

    public void SetPin(Pin pin, bool value)
    {
        switch (pin)
        {
            case Pin.A10:
            case Pin.D4:
            case Pin.D5:
            case Pin.D6:
            case Pin.D7:
            case Pin.D3:
            case Pin.D2:
            case Pin.D1:
            case Pin.D0:
            case Pin.INTE:
            case Pin.DBIN:
            case Pin.WR:
            case Pin.SYNC:
            case Pin.HLDA:
            case Pin.WAIT:
            case Pin.A0:
            case Pin.A1:
            case Pin.A2:
            case Pin.A3:
            case Pin.A4:
            case Pin.A5:
            case Pin.A6:
            case Pin.A7:
            case Pin.A8:
            case Pin.A9:
            case Pin.A15:
            case Pin.A12:
            case Pin.A13:
            case Pin.A14:
            case Pin.A11:
                pins &= ~((ulong)1 << (int)(pin - 1));
                if (value)
                {
                    pins |= (ulong)1 << (int)(pin - 1);
                }

                break;
            default:
                throw new InvalidConstraintException("Invalid pin: pin is not writable");
        }
    }

    public bool GetPin(Pin pin)
    {
        return ((pins >> (int)(pin - 1)) & 0b1) == 1;
    }

    private void PushState()
    {
        states.Push(new State(cycles, pins, Ports.ToArray(), A, BC, DE, HL, SP, PC, Cc.GetAsValue(), Memory.ToArray()));
    }

    private void PopState()
    {
        var state = states.Pop();
        cycles = state.cycles;
        pins = state.pins;
        Ports = state.ports;
        A = state.A;
        BC = state.BC;
        DE = state.DE;
        HL = state.HL;
        SP = state.SP;
        PC = state.PC;
        Cc.SetAsValue(state.ConditionState);
        state.mem.CopyTo(Memory, 0);
    }

    public static void RunTestSuite(bool cacheFile = false, bool print_debug = false)
    {
        byte[] f = [];
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var filePath = Path.Combine(desktopPath, "cpudiag.bin");
        if (File.Exists(filePath))
        {
            Console.Out.WriteLine("Using cached cpudiag.bin");
            f = File.ReadAllBytes(filePath);
        }
        else
        {
            Console.Out.WriteLine("Downloading cpudiag.bin");
            using var client = new HttpClient();
            try
            {
                f = client.GetByteArrayAsync("http://www.emulator101.com/files/cpudiag.bin").Result;

                // Optionally cache the file on the desktop
                if (cacheFile)
                {
                    File.WriteAllBytes(filePath, f);
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("Error downloading file: " + e.Message);
                return;
            }
        }

        var testCpu = new Intel8008().LoadMemory(f, 0x100)
            .LoadMemory([0xc3, 0x00, 0x01], 0)
            .LoadMemory([0x07], 368)
            .LoadMemory([0xc3, 0xc2, 0x05], 0x59c);
        do
        {
            /* on error:
             * 0x00C5: CALL 0x689
             * 0x0589: LXI HL, 0x018B
             * 0x058C: CALL 0x145
             * 0x0045: PUSH DE
             * 0x0046: XCHG
             * 0x0047: MVI C, 0x09
             * 0x0049: CALL 0x5
             */
            uint c = 0;
            if (testCpu.PC == 0x689)
            {
                Console.Out.WriteLine("ERROR IN PREV INSTR");
                while (true)
                {
                }
            }
        } while (testCpu.run(1, true, false, print_debug));
    }

    public string GetCurrentInstrAsString()
    {
        uint c = 0;
        var dismMsg = Disassemble(PC, out _, ref c).Split("//")[0];

        return new StringBuilder()
            .Append($"{iterations} - ")
            .Append("0x")
            .Append($"{PC:X4}")
            .Append(": 0x")
            .Append($"{Memory[PC]:X2}")
            // .Append(" - ")
            // .Append(((Opcode)Memory[PC]).ToString())
            .Append(" - ")
            .Append(dismMsg)
            .Append(" - A = 0x")
            .Append($"{A:X2}")
            .Append(", CC = ")
            .Append($"{Cc.GetAsValue():b8}")
            .Append(", BC = ")
            .Append($"{BC:X4}")
            .Append(", DE = ")
            .Append($"{DE:X4}")
            .Append(", HL = ")
            .Append($"{HL:X4}")
            .Append(", M = ")
            .Append($"{M:X2}")
            .Append(", SP = ")
            .Append($"{SP:X2}")
            .ToString();
    }

    public ArraySegment<byte> GetMemory(ushort start, ushort end)
    {
        return new ArraySegment<byte>(Memory, start, end - start + 1);
    }
}
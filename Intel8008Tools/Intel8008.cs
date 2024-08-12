// ReSharper disable InconsistentNaming

using System.IO;
using System.Numerics;
using System.Text;

namespace Intel8008Tools;

public class Intel8008
{
    private UInt32 cycles = 0;
    public byte A;
    public short BC;

    public byte B
    {
        get => (byte)(BC >> 8);
        set => BC = (short)((BC & 0xFF) | (value << 8));
    }

    public byte C
    {
        get => (byte)(BC & 0xFF);
        set => BC = (short)((BC & (0xFF << 8)) | value);
    }

    public short DE;

    public byte D
    {
        get => (byte)(DE >> 8);
        set => DE = (short)((DE & 0xFF) | (value << 8));
    }

    public byte E
    {
        get => (byte)(DE & 0xFF);
        set => DE = (short)((DE & (0xFF << 8)) | value);
    }

    public short HL;

    public byte H
    {
        get => (byte)(HL >> 8);
        set => HL = (short)((HL & 0xFF) | (value << 8));
    }

    public byte L
    {
        get => (byte)(HL & 0xFF);
        set => HL = (short)((HL & (0xFF << 8)) | value);
    }

    public byte M
    {
        get => Memory[HL];
        set => Memory[HL] = value;
    }

    public byte Z
    {
        get => (byte)(Cc.Z ? 1 : 0);
        set => Cc.Z = value == 0;
    }

    public byte S
    {
        get => (byte)(Cc.S ? 1 : 0);
        set => Cc.S = (sbyte)value < 0;
    }

    public byte P
    {
        get => (byte)(Cc.P ? 1 : 0);
        set => Cc.P = BitOperations.PopCount(value) % 2 == 0;
    }

    public byte Cy
    {
        get => (byte)(Cc.Cy ? 1 : 0);
        set => Cc.Cy = value != 0;
    }

    public byte Ac
    {
        get => (byte)(Cc.Ac ? 1 : 0);
        set => Cc.Ac = value != 0;
    }

    public ushort Sp;
    public ushort Pc;
    public readonly byte[] Memory = new byte[0x10000];
    public ConditionCodes Cc = new();

    public Intel8008(byte[] memory)
    {
        Array.Clear(Memory);
        InitRegisters();
        LoadMemory(memory, 0);
    }

    public void LoadMemory(string filePath, int offset)
    {
        LoadMemory(File.ReadAllBytes(filePath), offset);
    }

    public void LoadMemory(byte[] memory, int offset)
    {
        memory.CopyTo(Memory, offset);
    }

    private void InitRegisters()
    {
        A = 0;
        BC = 0;
        DE = 0;
        HL = 0;
        Sp = 0;
        Pc = 0;
        Cc.Init();
    }

    public string Disassemble(short start, short end)
    {
        var i = start;
        var res = new StringBuilder();
        while (i < end)
        {
            res.AppendLine(Disassemble(i, out var offset));
            i += offset;
        }

        return res.ToString();
    }

    public string Disassemble(short pos, out short offset)
    {
        var res = "NOT IMPLEMENTED";
        offset = 0;
        switch ((Memory[pos] & 0b11000000) >> 6)
        {
            case 0b00:
                switch (Memory[pos] & 0b00111111)
                {
                    case 0b00000000:
                        res = "NOP";
                        cycles += 4;
                        break;
                    case 0b00000111:
                        cycles += 4;
                        res = "RLC // Cy = A >> 7; A = A << 1; A = (A & 0b11111110) | Cy";
                        break;
                    case 0b00001111:
                        cycles += 4;
                        res = "RRC // Cy = A & 0b1; A = A >> 1; A = (A & 0b01111111) | (Cy << 7)";
                        break;
                    case 0b00010111:
                        cycles += 4;
                        res = "RAL // tmp = A >> 7; A = A << 1; A = (A & 0b11111110) | Cy; Cy = tmp";
                        break;
                    case 0b00011111:
                        cycles += 4;
                        res = "RAR // tmp = A & 0b1; A = A >> 1; A = (A & 0b01111111) | (Cy << 7); Cy = tmp";
                        break;
                    case 0b00100010:
                    {
                        cycles += 16;
                        var addr = (Memory[pos + 2] << 8) | Memory[pos + 1];
                        offset += 2;
                        res = $"SHLD {addr} // Memory[{addr}] = HL";
                    }
                        break;
                    case 0b00100111:
                    {
                        cycles += 4;
                        res = "DAA // A = ((A & 0b1111) > 9 || Ac ) ? A+6 : A; A = ((A >> 4) > 9 || Cy) ? A + 0x60 : A";
                    }
                        break;
                    case 0b00101010:
                    {
                        cycles += 13;
                        var addr = (Memory[pos + 2] << 8) | Memory[pos + 1];
                        offset += 2;
                        res = $"LHLD {addr} // HL = Memory[{addr}]";
                    }
                        break;
                    case 0b00101111:
                    {
                        cycles += 4;
                        res = "CMA // A = ~A";
                    }
                        break;
                    case 0b00110010:
                    {
                        cycles += 13;
                        var addr = (Memory[pos + 2] << 8) | Memory[pos + 1];
                        offset += 2;
                        res = $"STA {addr} // Memory[{addr}] = A";
                    }
                        break;
                    case 0b00110111:
                    {
                        cycles += 4;
                        res = "STC // Cy = 1";
                    }
                        break;
                    case 0b00111010:
                    {
                        cycles += 13;
                        var addr = (Memory[pos + 2] << 8) | Memory[pos + 1];
                        offset += 2;
                        res = $"LDA {addr} // A = Memory[{addr}]";
                    }
                        break;
                    case 0b00111111:
                    {
                        cycles += 4;
                        res = "CMC // Cy = ~Cy";
                    }
                        break;
                    default:
                        switch (Memory[pos] & 0b1111)
                        {
                            case 0b001:
                            {
                                cycles += 10;
                                var rp = "";
                                var data = (Memory[pos + 2] << 8) | Memory[pos + 1];
                                offset += 2;
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
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

                                res = $"LXI {rp}, {data} // {rp} = {data}";
                            }
                                break;
                            case 0b010:
                            {
                                cycles += 7;
                                var rp = "";
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        rp = "BC";
                                        break;
                                    case Rp.DE:
                                        rp = "DE";
                                        break;
                                    case Rp.HL:
                                    case Rp.SP:
                                        rp = "INVALID_REGISTER";
                                        break;
                                }

                                res = $"STAX {rp} // {rp} = A";
                            }
                                break;
                            case 0b011:
                            {
                                cycles += 5;
                                var rp = "";
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        rp = "BC";
                                        break;
                                    case Rp.DE:
                                        rp = "DE";
                                        break;
                                    case Rp.HL:
                                    case Rp.SP:
                                        rp = "INVALID_REGISTER";
                                        break;
                                }

                                res = $"INX {rp} // {rp} += 1";
                            }
                                break;
                            case 0b100:
                            {
                                var ddd = "";
                                switch ((Reg)((Memory[pos] >> 3) & 0b111))
                                {
                                    case Reg.B:
                                        cycles += 9;
                                        ddd = "B";
                                        break;
                                    case Reg.C:
                                        cycles += 5;
                                        ddd = "C";
                                        break;
                                    case Reg.D:
                                        cycles += 9;
                                        ddd = "D";
                                        break;
                                    case Reg.E:
                                        cycles += 5;
                                        ddd = "E";
                                        break;
                                    case Reg.H:
                                        cycles += 9;
                                        ddd = "H";
                                        break;
                                    case Reg.L:
                                        cycles += 5;
                                        ddd = "L";
                                        break;
                                    case Reg.M:
                                        cycles += 10;
                                        ddd = $"Memory[HL]";
                                        break;
                                    case Reg.A:
                                        cycles += 5;
                                        ddd = "A";
                                        break;
                                }

                                res = $"INR {ddd} // {ddd} += 1";
                            }
                                break;
                            case 0b101:
                            {
                                var ddd = "";
                                switch ((Reg)((Memory[pos] >> 3) & 0b111))
                                {
                                    case Reg.B:
                                        cycles += 9;
                                        ddd = "B";
                                        break;
                                    case Reg.C:
                                        cycles += 5;
                                        ddd = "C";
                                        break;
                                    case Reg.D:
                                        cycles += 9;
                                        ddd = "D";
                                        break;
                                    case Reg.E:
                                        cycles += 5;
                                        ddd = "E";
                                        break;
                                    case Reg.H:
                                        cycles += 9;
                                        ddd = "H";
                                        break;
                                    case Reg.L:
                                        cycles += 5;
                                        ddd = "L";
                                        break;
                                    case Reg.M:
                                        cycles += 10;
                                        ddd = "Memory[HL]";
                                        break;
                                    case Reg.A:
                                        cycles += 5;
                                        ddd = "A";
                                        break;
                                }

                                res = $"DCR {ddd} // {ddd} -= 1";
                            }
                                break;
                            case 0b110:
                            {
                                var ddd = "";
                                var data = Memory[pos + 1];
                                offset += 1;
                                switch ((Reg)((Memory[pos] >> 3) & 0b111))
                                {
                                    case Reg.B:
                                        cycles += 9;
                                        ddd = "B";
                                        break;
                                    case Reg.C:
                                        cycles += 5;
                                        ddd = "C";
                                        break;
                                    case Reg.D:
                                        cycles += 9;
                                        ddd = "D";
                                        break;
                                    case Reg.E:
                                        cycles += 5;
                                        ddd = "E";
                                        break;
                                    case Reg.H:
                                        cycles += 9;
                                        ddd = "H";
                                        break;
                                    case Reg.L:
                                        cycles += 5;
                                        ddd = "L";
                                        break;
                                    case Reg.M:
                                        cycles += 10;
                                        ddd = "Memory[HL]";
                                        break;
                                    case Reg.A:
                                        cycles += 5;
                                        ddd = "A";
                                        break;
                                }

                                res = $"MVI {ddd}, {data} // {ddd} = {data}";
                            }
                                break;
                            case 0b1001:
                            {
                                cycles += 10;
                                var rp = "";
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
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
                                cycles += 7;
                                var rp = "";
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
                                {
                                    case Rp.BC:
                                        rp = "BC";
                                        break;
                                    case Rp.DE:
                                        rp = "DE";
                                        break;
                                    case Rp.HL:
                                    case Rp.SP:
                                        rp = "INVALID_REGISTER";
                                        break;
                                }

                                res = $"LDAX {rp} // A = {rp}";
                            }
                                break;
                            case 0b1011:
                            {
                                cycles += 5;
                                var rp = "";
                                switch ((Rp)((Memory[pos] >> 4) & 0b11))
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
                        }

                        break;
                }

                break;
            case 0b01:
                switch (Memory[pos])
                {
                    case 0b01110110:
                        cycles += 7;
                        res = "HLT // offset = -1 //wait until interrupt";
                        break;
                    default:
                    {
                        cycles += 6; // 5-7
                        var src = "";
                        var dest = "";
                        switch ((Reg)(Memory[pos] & 0b111))
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

                        switch ((Reg)((Memory[pos] >> 3) & 0b111))
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
                cycles += 5; //4 - 7
                var src = "";
                switch ((Reg)(Memory[pos] & 0b111))
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

                switch ((Alu)((Memory[pos] >> 3) & 0b111))
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
                switch (Memory[pos])
                {
                    case 0b11111011:
                        cycles += 4;
                        res = "EI // Enable interrupts";
                        break;
                    case 0b11111001:
                        res = "SPHL // SP = HL";
                        cycles += 5;
                        break;
                    case 0b11110011:
                        cycles += 4;
                        res = "DI // Disable interrupts";
                        break;
                    case 0b11101011:
                        cycles += 4;
                        res = "XCHG // tmp = HL; HL = DE; DE = tmp;";
                        break;
                    case 0b11101001:
                        cycles += 5;
                        res = "PCHL // PC = HL";
                        break;
                    case 0b11100011:
                        cycles += 18;
                        res = "XTHL // tmp = HL; HL = Memory[SP]; Memory[SP] = tmp;";
                        break;
                    case 0b11011011:
                    {
                        cycles += 10;
                        var port = Memory[pos + 1];
                        offset += 1;
                        res = $"IN 0x{port:X} // A = port;";
                    }
                        break;
                    case 0b11010011:
                    {
                        cycles += 10;
                        var port = Memory[pos + 1];
                        offset += 1;
                        res = $"OUT 0x{port:X} // port = A;";
                    }
                        break;
                    case 0b11001101:
                    {
                        cycles += 17;
                        var addr = (short)((Memory[pos + 2] << 8) | Memory[pos + 1]);
                        offset += 2;
                        res =
                            $"CALL 0x{addr:X} // SP -= 2; Memory[SP] = (byte)(PC&0xFF); Memory[SP+1] = (byte)((PC>>8)&0xFF); PC = 0x{addr:X}";
                    }
                        break;
                    case 0b11001001:
                        cycles += 10;
                        res = "RET // PC = (short)((Memory[Sp + 1] << 8) | Memory[Sp]); SP += 2;";
                        break;
                }

                break;
        }

        offset++;
        return res;
    }

    public Intel8008(string pathToFile) : this(File.ReadAllBytes(pathToFile))
    {
    }

    public byte AluAdd(byte a, byte b, bool c = false)
    {
        var res = (ushort)(a + b + (c ? 1 : 0));
        SetArithFlags(res);
        Cc.Ac = (a & 0xF) + (b & 0xF) > 0xF;
        return (byte)(res & 0xFF);
    }

    public byte AluCmp(byte a, byte b) => AluSub(a, b);

    public byte AluSub(byte a, byte b, bool c = false)
    {
        var res = (ushort)(a - b - (c ? 1 : 0));
        SetArithFlags(res);
        Cc.Ac = (a & 0xF) < (b & 0xF);
        return (byte)(res & 0xFF);
    }

    public byte AluAnd(byte a, byte b) => AluLogical((v1, v2) => (byte)(v1 & v2), a, b);
    public byte AluXor(byte a, byte b) => AluLogical((v1, v2) => (byte)(v1 ^ v2), a, b);
    public byte AluOR(byte a, byte b) => AluLogical((v1, v2) => (byte)(v1 | v2), a, b);

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

    private void SetArithFlags(ushort res)
    {
        Z = (byte)(res & 0xFF);
        S = (byte)(res & 0xFF);
        P = (byte)(res & 0xFF);
        Cc.Cy = res > 0xFF;
    }

    public Intel8008() : this(Array.Empty<byte>())
    {
    }

    private void WriteToMemory(short addr, byte value)
    {
        Memory[addr] = value;
    }

    private void WriteToMemory(short addr, short value)
    {
        WriteToMemory(addr, (byte)(value & 0xFF));
        WriteToMemory((short)(addr + 1), (byte)((value >> 8) & 0xFF));
    }
}
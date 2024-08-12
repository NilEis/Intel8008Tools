// ReSharper disable InconsistentNaming

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

    public ushort Sp;
    public ushort Pc;
    public readonly byte[] Memory = new byte[0x10000];
    public readonly ConditionCodes Cc = new();

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
        switch (Memory[pos] & 0b11000000)
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
                                        ddd = $"Memory[HL]";
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
                        switch ((Reg)((Memory[pos]>>3) & 0b111))
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
                var alu = "";
                switch ((Alu)((Memory[pos] >> 3)&0b111))
                {
                    case Alu.ADD:
                        alu = "ADD";
                        break;
                    case Alu.ADC:
                        alu = "ADC";
                        break;
                    case Alu.SUB:
                        alu = "SUB";
                        break;
                    case Alu.SBB:
                        alu = "SBB";
                        break;
                    case Alu.ANA:
                        alu = "ANA";
                        break;
                    case Alu.XRA:
                        alu = "XRA";
                        break;
                    case Alu.ORA:
                        alu = "ORA";
                        break;
                    case Alu.CMP:
                        alu = "CMP";
                        break;
                }
            }
                break;
            case 0b11: break;
        }

        offset++;
        return res;
    }

    public Intel8008(string pathToFile) : this(File.ReadAllBytes(pathToFile))
    {
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
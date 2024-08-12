namespace Intel8008Tools;

public record struct ConditionCodes(bool Z, bool S, bool P, bool Cy, bool Ac, byte Pad)
{
    public byte GetAsValue()
    {
        return (byte)((Z ? 1 : 0) | ((S ? 1 : 0) << 1) | ((P ? 1 : 0) << 2) | ((Cy ? 1 : 0) << 3) |
                      ((Ac ? 1 : 0) << 4));
    }

    public void SetAsValue(byte v)
    {
        Z = (v & 0b1) == 1;
        S = ((v >> 1) & 0b1) == 1;
        P = ((v >> 2) & 0b1) == 1;
        Cy = ((v >> 3) & 0b1) == 1;
        Ac = ((v >> 4) & 0b1) == 1;
    }

    public void Init()
    {
        Z = false;
        S = false;
        P = true;
        Cy = false;
        Ac = false;
    }
}
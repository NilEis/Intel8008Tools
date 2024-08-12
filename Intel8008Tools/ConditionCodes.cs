namespace Intel8008Tools;

public record struct ConditionCodes(bool Z, bool S, bool P, bool Cy, bool Ac, byte Pad)
{
    public byte AsValue()
    {
        return (byte)((Z ? 1 : 0) | ((S ? 1 : 0) << 1) | ((P ? 1 : 0) << 2) | ((Cy ? 1 : 0) << 3) |
                      ((Ac ? 1 : 0) << 4) |
                      (Pad << 5));
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
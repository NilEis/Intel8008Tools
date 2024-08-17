namespace Intel8008Tools;

public record struct ConditionCodes(bool Z, bool S, bool P, bool Cy, bool Ac)
{
    public byte GetAsValue()
    {
        return (byte)(((Cy ? 1 : 0) << 0) |
                      ((P ? 1 : 0) << 2) |
                      ((Ac ? 1 : 0) << 4) |
                      ((Z ? 1 : 0) << 6) |
                      ((S ? 1 : 0) << 7)
            );
    }

    public void SetAsValue(byte v)
    {
        Cy = ((v >> 0) & 0b1) == 1;
        P = ((v >> 2) & 0b1) == 1;
        Ac = ((v >> 4) & 0b1) == 1;
        Z = ((v >> 6) & 0b1) == 1;
        S = ((v >> 7) & 0b1) == 1;
    }

    public void Init()
    {
        Z = false;
        S = false;
        P = true;
        Cy = false;
        Ac = false;
    }

    public override string ToString()
    {
        return
            $"CC: 0b{GetAsValue():b8} (Cy:{(Cy ? 1 : 0)} P:{(P ? 1 : 0)} Ac:{(Ac ? 1 : 0)} Z:{(Z ? 1 : 0)} S:{(S ? 1 : 0)})";
    }
}
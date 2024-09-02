namespace Intel8080Tools;

[Flags]
public enum Flags : byte
{
    NONE = 0b00000,
    S = 0b00001,
    Z = 0b00010,
    P = 0b00100,
    CY = 0b01000,
    AC = 0b10000,
    ALL = 0b11111
}
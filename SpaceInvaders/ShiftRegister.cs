namespace SpaceInvaders;

public partial class MainWindow
{
    public class ShiftRegister
    {
        private ShiftRegister()
        {
        }

        public static ShiftRegister Instance { get; } = new();
        private ushort _reg;

        public byte Reg
        {
            get
            {
                var res = _reg << Offset;
                res = (ushort)(res & 0xFF00);
                return (byte)(res >> 8);
            }
            set
            {
                var tmpA = (_reg >> 8) & 0x00FF;
                _reg = (ushort)((value << 8) | tmpA);
            }
        }

        public byte Offset
        {
            get => offset;

            set
            {
                if (value > 0x07)
                {
                    throw new ArgumentException("Offset must be less than or equal to 0x07");
                }
                offset = value;
            }
        }

        private byte offset;
    }
}
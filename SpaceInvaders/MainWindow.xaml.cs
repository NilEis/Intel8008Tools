using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Intel8008Tools;

namespace SpaceInvaders;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _updateFramebufferTimer;
    private readonly DispatcherTimer _updateCpuTimer;
    private readonly Intel8008 _cpu = new();
    private const int width = 224;
    private const int height = 256;

    private record struct lastTime(uint milli, uint micro);

    private struct shiftRegister
    {
        private byte a;
        private byte b;

        public byte Reg
        {
            get => (byte)((((b << 8) | a) >> (8 - offset)) & 0xFF);
            set
            {
                a = b;
                b = value;
            }
        }

        public byte offset;
    }

    private shiftRegister sR = new();

    private lastTime _lt = new();

    public MainWindow()
    {
        InitializeComponent();
        Image.RenderTransform = new RotateTransform(90, height / 2, width / 2);
        _updateFramebufferTimer = new DispatcherTimer();
        _updateCpuTimer = new DispatcherTimer();

        const string prefix = @"C:\Users\Nils_Eisenach\Desktop\dev\CS\Intel8008Tools\invaders";
        _cpu.LoadMemory(Path.Join(prefix, "invaders.h"), 0)
            .LoadMemory(Path.Join(prefix, "invaders.g"), 0x800)
            .LoadMemory(Path.Join(prefix, "invaders.f"), 0x1000)
            .LoadMemory(Path.Join(prefix, "invaders.e"), 0x1800)
            ;

        _cpu.Ports[0] = 0b00001110;
        _cpu.Ports[1] = 0b00001000;
        _cpu.Ports[2] = 0b10000000 | 0b00;
        _cpu.Ports[3] = 0;
        _cpu.inPorts[3] = _ =>
        {
            var shiftRegV = sR.Reg;
            return shiftRegV;
        };
        _cpu.outPorts[2] = (_, b) => { sR.offset = (byte)(b & 0x07); };
        _cpu.outPorts[4] = (_, b) => { sR.Reg = b; };

        KeyDown += (obj, e) => { SetKey(e.Key, true); };
        KeyUp += (obj, e) => { SetKey(e.Key, false); };


        _updateFramebufferTimer.Tick += OnUpdateFramebuffer;
        _updateFramebufferTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0);

        _updateCpuTimer.Tick += OnUpdateCpu;
        _updateCpuTimer.Interval = TimeSpan.Zero;

        _updateFramebufferTimer.Start();
        _updateCpuTimer.Start();
        return;
        // todo: http://www.emulator101.com/interrupts.html

        void OnUpdateCpu(object? sender, EventArgs e)
        {
            var dt = (uint)(DateTime.Now.Millisecond * 1000 + DateTime.Now.Microsecond) -
                     (_lt.milli * 1000 + _lt.micro);
            //Console.Out.WriteLine(cpu.GetCurrentInstrAsString());
            _cpu.run((uint)(dt / (1_000_000.0 / 2_000_000.0)));
            _lt.milli = (uint)DateTime.Now.Millisecond;
            _lt.micro = (uint)DateTime.Now.Microsecond;
        }

        void OnUpdateFramebuffer(object? sender, EventArgs e)
        {
            var buffer = _cpu.GetMemory(0x2400, 0x3FFF).ToArray();
            var bmp = BitmapSource.Create(height, width, 0, 0, PixelFormats.BlackWhite, null, buffer, height / 8);
            Image.Source = bmp;
        }
    }

    private void SetKey(Key k, bool pressed)
    {
        switch (k)
        {
            case Key.Space:
                _cpu.Ports[1] = (byte)((_cpu.Ports[1] & 0b11101111) | (pressed ? 0b00010000 : 0b00000000));
                break;
            case Key.Left:
                _cpu.Ports[1] = (byte)((_cpu.Ports[1] & 0b11011111) | (pressed ? 0b00100000 : 0b00000000));
                break;
            case Key.Right:
                _cpu.Ports[1] = (byte)((_cpu.Ports[1] & 0b10111111) | (pressed ? 0b01000000 : 0b00000000));
                break;
            default:
                break;
        }
    }
}
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Intel8008Tools;

namespace SpaceInvaders;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly Intel8008 _cpu = new();
    private const int width = 224;
    private const int height = 256;
    private readonly byte[] buffer = new byte[width * height / 8];

    private static readonly byte[] ReverseLookup =
    [
        0x0, 0x8, 0x4, 0xc, 0x2, 0xa, 0x6, 0xe,
        0x1, 0x9, 0x5, 0xd, 0x3, 0xb, 0x7, 0xf
    ];

    private struct shiftRegister
    {
        private byte a;
        private byte b;

        public byte Reg
        {
            get
            {
                var v = (ushort)((b << 8) | a);
                return (byte)((v >> (8 - offset)) & 0xFF);
            }
            set
            {
                b = a;
                b = value;
            }
        }

        public byte offset;
    }

    private shiftRegister sR;

    private readonly object _lock = new();

    private bool _running = true;

    public MainWindow()
    {
        InitializeComponent();
        var transformGroup = new TransformGroup();
        Image.RenderTransform = transformGroup;
        transformGroup.Children.Add(new RotateTransform(-90, height / 2, width / 2));
        // transformGroup.Children.Add(new ScaleTransform(1, -1, height / 2, width / 2));

        const string prefix = @"C:\Users\Nils_Eisenach\Desktop\dev\CS\Intel8008Tools\invaders";
        _cpu.LoadMemory(Path.Join(prefix, "invaders.h"), 0)
            .LoadMemory(Path.Join(prefix, "invaders.g"), 0x800)
            .LoadMemory(Path.Join(prefix, "invaders.f"), 0x1000)
            .LoadMemory(Path.Join(prefix, "invaders.e"), 0x1800)
            ;

        _cpu.Ports[0] = 1; //0b00001110;
        _cpu.Ports[1] = 0; //0b00001101;
        _cpu.Ports[2] = 0b00000000 | 0b00;
        _cpu.Ports[3] = 0;
        _cpu.inPorts[3] = _ => sR.Reg;
        _cpu.outPorts[2] = (_, b) => { sR.offset = (byte)(b & 0b00000111); };
        _cpu.outPorts[4] = (_, b) => { sR.Reg = b; };

        KeyDown += (obj, e) => { SetKey(e.Key, true); };
        KeyUp += (obj, e) => { SetKey(e.Key, false); };

        var t = new Thread(OnUpdateCpu);
        t.Start();
        return;

        void OnUpdateCpu()
        {
            var sw = new Stopwatch();
            var frameSw = Stopwatch.StartNew();
            var targetTicks = Stopwatch.Frequency / 2000000;
            while (_running)
            {
                lock (_lock)
                {
                    sw.Restart();
                    if (frameSw.ElapsedMilliseconds >= 1000.0 / 60.0)
                    {
                        OnUpdateFramebuffer();
                    }

                    _cpu.run();
                    while (sw.ElapsedTicks < targetTicks)
                    {
                        Thread.SpinWait(1);
                    }
                }
            }
        }

        void OnUpdateFramebuffer()
        {
            if (_cpu.GetPin(Pin.INTE))
            {
                _cpu.GenerateInterrupt(2);
            }

            var seg = _cpu.GetMemory(0x2400, 0x3FFF);
            for (var i = 0; i < seg.Count; i++)
            {
                buffer[i] = Reverse(seg[i]);
            }

            Dispatcher.Invoke(() =>
            {
                var bmp = BitmapSource.Create(height, width, 0, 0, PixelFormats.BlackWhite, null, buffer, height / 8);
                Image.Source = bmp;
            });
        }
    }

    private static byte Reverse(byte n)
    {
        return (byte)((ReverseLookup[n & 0b1111] << 4) | ReverseLookup[n >> 4]);
    }

    protected override void OnClosed(EventArgs e)
    {
        _running = false;
        base.OnClosed(e);
    }

    private void SetKey(Key k, bool pressed)
    {
        lock (_lock)
        {
            switch (k)
            {
                case Key.C:
                    _cpu.Ports[1] = (byte)((_cpu.Ports[1] & 0b11111110) | (pressed ? 0b00000001 : 0b00000000));
                    break;
                case Key.D1:
                    _cpu.Ports[1] = (byte)((_cpu.Ports[1] & 0b11111011) | (pressed ? 0b00000100 : 0b00000000));
                    break;
                case Key.Space:
                    _cpu.Ports[1] = (byte)((_cpu.Ports[1] & 0b11101111) | (pressed ? 0b00010000 : 0b00000000));
                    break;
                case Key.Left:
                    _cpu.Ports[1] = (byte)((_cpu.Ports[1] & 0b11011111) | (pressed ? 0b00100000 : 0b00000000));
                    break;
                case Key.Right:
                    _cpu.Ports[1] = (byte)((_cpu.Ports[1] & 0b10111111) | (pressed ? 0b01000000 : 0b00000000));
                    break;
                case Key.Escape:
                    _running = false;
                    Application.Current.Shutdown();
                    break;
            }
        }
    }
}
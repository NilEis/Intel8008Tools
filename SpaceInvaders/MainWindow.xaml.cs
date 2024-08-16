using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Intel8008Tools;

namespace SpaceInvaders;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly Intel8008 _cpu = new(0x4000);
    private readonly DispatcherTimer _updateFramebufferTimer;
    private const int width = 244;
    private const int height = 256;
    private readonly byte[] buffer = new byte[width * height / 8];
    private bool nextInt = true;

    private static readonly byte[] ReverseLookup =
    [
        0x0, 0x8, 0x4, 0xc, 0x2, 0xa, 0x6, 0xe,
        0x1, 0x9, 0x5, 0xd, 0x3, 0xb, 0x7, 0xf
    ];

    private ShiftRegister sR = ShiftRegister.Instance;

    private readonly object _lock = new();

    private bool _running = true;

    private record struct lastTime(uint milli, uint micro);

    private lastTime _lt;

    public MainWindow()
    {
        InitializeComponent();
        _updateFramebufferTimer = new DispatcherTimer();
        var transformGroup = new TransformGroup();
        Image.RenderTransform = transformGroup;
        var cpuStopWatch = new Stopwatch();
        transformGroup.Children.Add(new RotateTransform(-90, height / 2, width / 2));
        // transformGroup.Children.Add(new ScaleTransform(1, -1, height / 2, width / 2));

        var prefix = Environment.GetEnvironmentVariable("SPACE_INVADERS_DIR") ?? Directory.GetCurrentDirectory();
        _cpu.LoadMemory(Path.Join(prefix, "invaders.h"), 0)
            .LoadMemory(Path.Join(prefix, "invaders.g"), 0x800)
            .LoadMemory(Path.Join(prefix, "invaders.f"), 0x1000)
            .LoadMemory(Path.Join(prefix, "invaders.e"), 0x1800)
            ;
        _cpu.SetPin(Pin.INTE, false);
        _cpu.inPorts[0] = _ => 0b01110000;
        _cpu.inPorts[1] = _ => (byte)(_cpu.Ports[1] | 0b00001000);
        _cpu.inPorts[2] = _ => 0b00000000;
        _cpu.inPorts[3] = _ => sR.Reg;
        _cpu.outPorts[2] = (_, b) => { sR.Offset = (byte)(b & 0b00000111); };
        _cpu.outPorts[4] = (_, b) => { sR.Reg = b; };
        _cpu.outPorts[3] = (_, b) =>
        {
            /* sounds */
        };
        _cpu.outPorts[5] = (_, b) =>
        {
            /* sounds */
        };
        _cpu.outPorts[6] = (_, b) =>
        {
            /* watchdog */
        };

        KeyDown += (obj, e) => { SetKey(e.Key, true); };
        KeyUp += (obj, e) => { SetKey(e.Key, false); };

        _updateFramebufferTimer.Tick += OnUpdateFramebuffer;
        _updateFramebufferTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / 120.0);


        _updateFramebufferTimer.Start();
        cpuStopWatch.Restart();
        new Thread(OnUpdateCpu).Start();
        return;

        void OnUpdateCpu()
        {
            while (_running)
            {
                var dt = cpuStopWatch.ElapsedMilliseconds * 1000;
                var u = (uint)(dt / (1_000_000.0 / 2_000_000.0));
                u = u < 1 ? 1 : u;
                lock (_lock)
                {
                    _cpu.run(u, false, false, false);
                }

                cpuStopWatch.Restart();
                udelay(999);
            }
        }

        void OnUpdateFramebuffer(object? o, EventArgs e)
        {
            lock (_lock)
            {
                if (!_cpu.GetPin(Pin.INTE)) return;
                if (nextInt)
                {
                    _cpu.GenerateInterrupt(1);
                }
                else
                {
                    _cpu.GenerateInterrupt(2);
                    var seg = _cpu.GetMemory(0x2400, 0x3FFF);
                    for (var i = 0; i < seg.Count; i++)
                    {
                        buffer[i] = Reverse(seg[i]);
                    }

                    var bmp = BitmapSource.Create(height, width, 0, 0, PixelFormats.BlackWhite, null, buffer,
                        height / 8);
                    Image.Source = bmp;
                }

                nextInt = !nextInt;
            }
        }
    }

    private static void udelay(long us)
    {
        var sw = Stopwatch.StartNew();
        var v = (us * Stopwatch.Frequency) / 1000000;
        while (sw.ElapsedTicks < v)
        {
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
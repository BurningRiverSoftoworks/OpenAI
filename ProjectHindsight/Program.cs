using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Runtime.InteropServices;

namespace ProjectHindsight;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
        .ConfigureLogging((_, logging) =>
        {
            logging.ClearProviders();
            logging.AddDebug();
        })
        .ConfigureServices((_, services) =>
        {
            services.AddSingleton<App>();
        }).Build();
        var app = host.Services.GetRequiredService<App>();
        var result = await app.StartAsync();

        Console.WriteLine("Main thread ended");
        return result;

    }
}

class App
{
    private readonly ILogger<App> _logger;
    private static CancellationTokenSource _cts = new CancellationTokenSource();
    private static long _frameCount = 0;
    private static DateTime _lastFrameTime = DateTime.Now;
    private static float _fps = 0;
    private static Player _player = new Player(0, 0, '@');

    public App(ILogger<App> logger)
    {
        _logger = logger;
    }
    public Task<int> StartAsync()
    {
        Thread.CurrentThread.Name = "Main";
        var input = Task.Run(() => InputHandler(_cts.Token));
        var render = Task.Run(() => RenderLoop(_logger));
        var update = Task.Run(() => UpdateLoop(_logger));
        while (!_cts.Token.IsCancellationRequested)
        {
            Thread.Sleep(1000 / 60);
        }
        return Task.FromResult(0);

    }

    private void UpdateLoop(ILogger<App> logger)
    {
        Thread.CurrentThread.Name = "UpdateLoop";
        while (!_cts.Token.IsCancellationRequested)
        {
            Update(_logger);
        }
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteConsoleOutput(IntPtr hConsoleOutput, CharInfo[] lpBuffer, Coord dwBufferSize, Coord dwBufferCoord, ref SmallRect lpWriteRegion);


    private void RenderLoop(ILogger<App> logger)
    {
        Thread.CurrentThread.Name = "RenderLoop";

        SetupConsoleScreen();

        while (!_cts.Token.IsCancellationRequested)
        {
            for (int i = 0; i < _buffer.Length; ++i)
            {
                _buffer[i].Char.UnicodeChar = ' ';
                _buffer[i].Attributes = 0;
            }
            Render();
            //cap render speed at 60fps
            Thread.Sleep(16);
            WriteConsoleOutput(ConsoleOutputHandle, _buffer, new Coord((short)Console.WindowWidth, (short)Console.WindowHeight), new Coord(0, 0), ref _rect);
        }
        Console.Clear();
        Console.CursorVisible = true;
    }

    static void Update(ILogger<App> _logger)
    {
        Thread.CurrentThread.Name = "Update";
    }
    static void Render()
    {

        Console.SetCursorPosition(0, 0);
        Console.WriteLine($"Frame: {_frameCount++}");

        Console.SetCursorPosition(12, 0);
        Console.WriteLine($"X: {_player.X} Y: {_player.Y}");

        CalculateFps();


        _player.Render();



    }

    private static void CalculateFps()
    {

        var timeSinceLastFpsUpdate = (DateTime.Now - _lastFrameTime).TotalSeconds;
        if (timeSinceLastFpsUpdate > 1)
        {
            _fps = _frameCount;
            _frameCount = 0;
            _lastFrameTime = DateTime.Now;
        }

        Console.SetCursorPosition(35, 0);
        Console.WriteLine($"FPS: {_fps}");
    }

    static async Task InputHandler(CancellationToken token)
    {
        Thread.CurrentThread.Name = "InputHandler";
        while (!token.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        _player.Y--;
                        break;
                    case ConsoleKey.DownArrow:
                        _player.Y++;
                        break;
                    case ConsoleKey.LeftArrow:
                        _player.X--;
                        break;
                    case ConsoleKey.RightArrow:
                        _player.X++;
                        break;
                    case ConsoleKey.Escape:
                        _cts.Cancel();
                        break;
                }
            }
            await Task.Delay(10);
        }
    }

    private static CharInfo[] _buffer;
    private static SmallRect _rect;

    private static readonly IntPtr ConsoleOutputHandle = GetStdHandle(STD_OUTPUT_HANDLE);
    private const int STD_OUTPUT_HANDLE = -11;

    [StructLayout(LayoutKind.Sequential)]
    public struct CharInfo
    {
        public CharUnion Char;
        public ushort Attributes;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct CharUnion
    {
        [FieldOffset(0)] public char UnicodeChar;
        [FieldOffset(0)] public byte AsciiChar;
    }

    public struct Coord
    {
        public short X;
        public short Y;

        public Coord(short x, short y)
        {
            X = x;
            Y = y;
        }
    }

    public struct SmallRect
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(int nStdHandle);
    private static void CreateDoubleBuffer(int width, int height)
    {
        _buffer = new CharInfo[width * height];
        _rect = new SmallRect() { Left = 0, Top = 0, Right = (short)width, Bottom = (short)height };
    }

    private static void SetupConsoleScreen()
    {
        Console.WindowWidth = 80;
        Console.WindowHeight = 25;
        Console.BufferWidth = Console.WindowWidth;
        Console.BufferHeight = Console.WindowHeight;
        Console.CursorVisible = false;

        CreateDoubleBuffer(Console.WindowWidth, Console.WindowHeight);
    }

}

class Player
{
    public int X { get; set; }
    public int Y { get; set; }
    public char Symbol { get; set; }

    public Player(int x, int y, char symbol)
    {
        X = x;
        Y = y;
        Symbol = symbol;
    }
    public void Render()
    {
        try
        {
            Console.SetCursorPosition(X, Y);
            Console.Write(Symbol);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}
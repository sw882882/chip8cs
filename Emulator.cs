using System.Runtime.InteropServices;
using SDL2;

namespace Chip8
{
    class Emulator
    {
        private IntPtr window;
        private IntPtr renderer;

        // getter and setter
        public bool Running { get; set; }

        private SDL.SDL_AudioSpec audioSpec;

        // private int sampleFrames;
        private int duty;
        private bool isHigh;

        // eventually should be variable
        private int volume;
        private int frequency;
        private readonly int sampleRate = 44100;
        private readonly ushort framesPerBuffer = 4096;

        public uint Screen_hz { get; private set; }
        public uint Cpu_hz { get; private set; }

        private bool[] pixels;

        private int height;
        private int width;
        private int screenScale;

        public int SoundTimer { get; set; }

        // make dictionary with hex keyboard and sdl scancodes
        private readonly Dictionary<byte, SDL.SDL_Scancode> keyMap = new Dictionary<
            byte,
            SDL.SDL_Scancode
        >
        {
            { 0x1, SDL.SDL_Scancode.SDL_SCANCODE_1 },
            { 0x2, SDL.SDL_Scancode.SDL_SCANCODE_2 },
            { 0x3, SDL.SDL_Scancode.SDL_SCANCODE_3 },
            { 0xC, SDL.SDL_Scancode.SDL_SCANCODE_4 },
            { 0x4, SDL.SDL_Scancode.SDL_SCANCODE_Q },
            { 0x5, SDL.SDL_Scancode.SDL_SCANCODE_W },
            { 0x6, SDL.SDL_Scancode.SDL_SCANCODE_E },
            { 0xD, SDL.SDL_Scancode.SDL_SCANCODE_R },
            { 0x7, SDL.SDL_Scancode.SDL_SCANCODE_A },
            { 0x8, SDL.SDL_Scancode.SDL_SCANCODE_S },
            { 0x9, SDL.SDL_Scancode.SDL_SCANCODE_D },
            { 0xE, SDL.SDL_Scancode.SDL_SCANCODE_F },
            { 0xA, SDL.SDL_Scancode.SDL_SCANCODE_Z },
            { 0x0, SDL.SDL_Scancode.SDL_SCANCODE_X },
            { 0xB, SDL.SDL_Scancode.SDL_SCANCODE_C },
            { 0xF, SDL.SDL_Scancode.SDL_SCANCODE_V }
        };

        private List<byte> pressedKeys;

        public Emulator(uint screen_hz, uint cpu_hz, uint screenScale)
        {
            Screen_hz = screen_hz;
            Cpu_hz = cpu_hz;
            height = (int)(32 * screenScale);
            width = (int)(64 * screenScale);
            pixels = new bool[64 * 32];
            this.screenScale = (int)screenScale;
            pressedKeys = new List<byte>();

            Running = true;

            setup();
        }

        public bool isKeyPressed(byte key)
        {
            return pressedKeys.Contains(key);
        }

        public uint SDL_GetTicks()
        {
            return SDL.SDL_GetTicks();
        }

        // frequency, duration, volume
        public void playSound(int frequency, int volume)
        {
            this.frequency = frequency;
            this.volume = volume;
        }

        public bool setPixel(int x, int y)
        {
            // if beyond the screen, wrap around
            x = x % 64;
            y = y % 32;
            int loc = x + y * 64;
            pixels[loc] = !pixels[loc];
            return !pixels[loc];
        }

        public void clear()
        {
            // reinitialize
            pixels = new bool[64 * 32];
        }

        public void render()
        {
            SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
            SDL.SDL_RenderClear(renderer);

            SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    if (pixels[x + y * 64])
                    {
                        // draw a pixel (rectangle) while accounting for scale size
                        SDL.SDL_Rect rect = new SDL.SDL_Rect
                        {
                            x = x * screenScale,
                            y = y * screenScale,
                            w = screenScale,
                            h = screenScale
                        };
                        SDL.SDL_RenderFillRect(renderer, ref rect);
                    }
                }
            }

            SDL.SDL_RenderPresent(renderer);
        }

        public void PollEvents()
        {
            while (SDL.SDL_PollEvent(out SDL.SDL_Event e) == 1)
            {
                switch (e.type)
                {
                    case SDL.SDL_EventType.SDL_QUIT:
                        Running = false;
                        break;
                    case SDL.SDL_EventType.SDL_KEYDOWN:
                        foreach (KeyValuePair<byte, SDL.SDL_Scancode> entry in keyMap)
                        {
                            if (e.key.keysym.scancode == entry.Value)
                            {
                                Console.WriteLine("Key pressed: {0}", entry.Value);
                                // add the hex
                                pressedKeys.Add(entry.Key);
                            }
                        }
                        // handle wait conditions here in the future
                        break;
                    case SDL.SDL_EventType.SDL_KEYUP:
                        foreach (KeyValuePair<byte, SDL.SDL_Scancode> entry in keyMap)
                        {
                            if (e.key.keysym.scancode == entry.Value)
                            {
                                Console.WriteLine("Key released: {0}", entry.Value);
                                // remove the hex
                                pressedKeys.Remove(entry.Key);
                            }
                        }
                        break;
                }
            }
        }

        private void audioCallback(IntPtr userdata, IntPtr stream, int len)
        {
            sbyte[] buf = new sbyte[len];

            // one channel
            // later the condition should be based on cpu clocks
            for (int i = 0; i < len && SoundTimer > 0; i++)
            {
                if (isHigh)
                {
                    buf[i] = (sbyte)volume;
                }
                else
                {
                    buf[i] = 0;
                }
                if (duty >= sampleRate / (frequency * 2))
                {
                    isHigh = !isHigh;
                    duty = 0;
                }
                duty++;
            }

            byte[] byteData = (byte[])(Array)buf;
            Marshal.Copy(byteData, 0, stream, len);

            // sampleFrames += len;
            // if (sampleFrames >= sampleRate * duration)
            // {
            //     audioPlay = false;
            // }
        }

        private void setup()
        {
            // Initilizes SDL.
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                Console.WriteLine("Unable to initialize SDL: {0}", SDL.SDL_GetError());
                Environment.Exit(1);
            }

            // Create a window.
            window = SDL.SDL_CreateWindow(
                "Chip8 Emulator",
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                width,
                height,
                SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN
            );

            if (window == IntPtr.Zero)
            {
                Console.WriteLine("Unable to create window: {0}", SDL.SDL_GetError());
                Environment.Exit(1);
            }

            // Create a renderer.
            renderer = SDL.SDL_CreateRenderer(
                window,
                -1,
                SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED
            );

            if (renderer == IntPtr.Zero)
            {
                Console.WriteLine("Unable to create renderer: {0}", SDL.SDL_GetError());
                Environment.Exit(1);
            }

            // audio setup
            audioSpec = new SDL.SDL_AudioSpec();
            audioSpec.freq = sampleRate;
            audioSpec.channels = 1;
            audioSpec.samples = framesPerBuffer;
            audioSpec.format = SDL.AUDIO_U8;
            audioSpec.callback = new SDL.SDL_AudioCallback(audioCallback);
            // handle error
            if (SDL.SDL_OpenAudio(ref audioSpec, IntPtr.Zero) < 0)
            {
                Console.WriteLine("Unable to open audio: {0}", SDL.SDL_GetError());
                Environment.Exit(1);
            }
            SDL.SDL_PauseAudio(0);
        }
    }
}

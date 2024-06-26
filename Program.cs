namespace Chip8
{
    class Program
    {
        static void Main(string[] args)
        {
            Emulator emulator = new Emulator(screen_hz: 60, cpu_hz: 1000, screenScale: 10);
            // load rom as byte array
            // byte[] rom = File.ReadAllBytes("./test/3-corax+.ch8");
            byte[] rom = File.ReadAllBytes("./test/Astro Dodge [Revival Studios, 2008].ch8");
            // CPU cpu = new CPU(emulator, rom, false, true);
            CPU cpu = new CPU(emulator, rom, false, false);
            cpu.run();
        }
    }
}

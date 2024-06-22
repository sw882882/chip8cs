namespace Chip8
{
    class CPU
    {
        Emulator emulator;
        private float cycleDelay;
        private float frameDelay;
        private readonly float timerDelay = (float)1000 / 60;
        private float nextCycleTime = 0;
        private float nextFrameTime = 0;
        private float nextTimerTime = 0;

        // ambiguous instructions
        private bool shift8XYSetVXtoVY;

        private int delayTimer;

        // emulation related stuff

        // chip 8 4096 bytes of memory
        private byte[] memory = new byte[4096];

        // 16 8-bit registers
        private byte[] v = new byte[16];

        // 16-bit register used to store memory addresses
        private ushort i;

        // program counter
        private ushort pc = 0x200;

        // stack
        private List<ushort> stack = new List<ushort>();
        private bool paused = false;
        private int speed = 10;

        private byte[] sprites = new byte[]
        {
            0xF0,
            0x90,
            0x90,
            0x90,
            0xF0, // 0
            0x20,
            0x60,
            0x20,
            0x20,
            0x70, // 1
            0xF0,
            0x10,
            0xF0,
            0x80,
            0xF0, // 2
            0xF0,
            0x10,
            0xF0,
            0x10,
            0xF0, // 3
            0x90,
            0x90,
            0xF0,
            0x10,
            0x10, // 4
            0xF0,
            0x80,
            0xF0,
            0x10,
            0xF0, // 5
            0xF0,
            0x80,
            0xF0,
            0x90,
            0xF0, // 6
            0xF0,
            0x10,
            0x20,
            0x40,
            0x40, // 7
            0xF0,
            0x90,
            0xF0,
            0x90,
            0xF0, // 8
            0xF0,
            0x90,
            0xF0,
            0x10,
            0xF0, // 9
            0xF0,
            0x90,
            0xF0,
            0x90,
            0x90, // A
            0xE0,
            0x90,
            0xE0,
            0x90,
            0xE0, // B
            0xF0,
            0x80,
            0x80,
            0x80,
            0xF0, // C
            0xE0,
            0x90,
            0x90,
            0x90,
            0xE0, // D
            0xF0,
            0x80,
            0xF0,
            0x80,
            0xF0, // E
            0xF0,
            0x80,
            0xF0,
            0x80,
            0x80 // F
        };

        public CPU(Emulator emulator, byte[] rom, bool shift8XYSetVXtoVY = false)
        {
            this.emulator = emulator;
            cycleDelay = (float)1000 / emulator.Cpu_hz;
            frameDelay = (float)1000 / emulator.Screen_hz;
            this.shift8XYSetVXtoVY = shift8XYSetVXtoVY;
            // load sprites
            for (int i = 0; i < sprites.Length; i++)
            {
                memory[i] = sprites[i];
            }
            // load rom
            loadRom(rom);
        }

        public void run()
        {
            while (emulator.Running)
            {
                emulator.PollEvents();
                if (emulator.SDL_GetTicks() >= nextCycleTime)
                {
                    cycle();
                    nextCycleTime += cycleDelay;
                }
                if (emulator.SDL_GetTicks() >= nextFrameTime)
                {
                    // Do screen work
                    emulator.render();
                    nextFrameTime += frameDelay;
                }
                if (emulator.SDL_GetTicks() >= nextTimerTime)
                {
                    updateTimers();
                    nextTimerTime += timerDelay;
                }
            }
        }

        private void updateTimers()
        {
            if (delayTimer > 0)
            {
                delayTimer--;
            }
            if (emulator.SoundTimer > 0)
            {
                emulator.SoundTimer--;
            }
        }

        private void loadRom(byte[] rom)
        {
            for (int i = 0; i < rom.Length; i++)
            {
                memory[0x200 + i] = rom[i];
            }
        }

        public void cycle()
        {
            if (paused)
            {
                return;
            }
            // fetch
            ushort opcode = (ushort)(memory[pc] << 8 | memory[pc + 1]);
            // increment program counter
            pc += 2;
            // decode
            executeInstruction(opcode);
        }

        private void executeInstruction(ushort opcode)
        {
            // only get the part where its a full byte
            // then shift so its the first nibble
            int x = (opcode & 0x0F00) >> 8;
            int y = (opcode & 0x00F0) >> 4;

            // get v

            // nibble of opcodes
            // X: look up one of the 16 registers (V0-VF), second nibble
            // Y: look up one of the 16 registers (V0-VF), third nibble
            // N: 4 bit constant, fourth nibble
            // NN: 8 bit constant, third and fourth nibbles
            // NNN: 12 bit constant, second, third and fourth nibbles
            // AND F000 lets us only keep the opcode, as it only
            // keeps the first 4 bits
            switch (opcode & 0xF000)
            {
                case 0x00E0:
                    // clear screen
                    emulator.clear();
                    break;
                case 0x1000:
                    // jump
                    pc = (ushort)(opcode & 0x0FFF);
                    break;
                case 0x2000:
                    // call subroutine
                    pc = (ushort)(opcode & 0x0FFF);
                    stack.Add(pc);
                    break;
                case 0x00EE:
                    // pop and return from subroutine
                    pc = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    break;
                case 0x3000:
                    // skip if vx is equal to nn (3XNN)
                    if (v[x] == (opcode & 0x00FF))
                    {
                        pc += 2;
                    }
                    break;
                case 0x4000:
                    // skip if vx is not equal to nn (4XNN)
                    if (v[x] != (opcode & 0x00FF))
                    {
                        pc += 2;
                    }
                    break;
                case 0x5000:
                    // skip if vx and vy are equal (5XY0)
                    if (v[x] == v[y])
                    {
                        pc += 2;
                    }
                    break;
                case 0x6000:
                    // set register VX to NN (6XNN)
                    v[x] = (byte)(opcode & 0x00FF);
                    break;
                case 0x7000:
                    // add value to register VX
                    // carry flag is not changed
                    v[x] += (byte)(opcode & 0x00FF);
                    break;
                case 0x8000:
                    switch (opcode & 0x000F)
                    {
                        case 0x0000:
                            // set vx to vy (8XY0)
                            v[x] = v[y];
                            break;
                        case 0x0001:
                            // set vx to vx or vy (8XY1)
                            v[x] |= v[y];
                            break;
                        case 0x0002:
                            // set vx to vx and vy (8XY2)
                            v[x] &= v[y];
                            break;
                        case 0x0003:
                            // logical xor vx and vy (8XY3)
                            v[x] ^= v[y];
                            break;
                        case 0x0004:
                            // add vy and vx
                            // carry if greater than 255
                            v[x] += v[y];
                            if (v[x] > 255)
                            {
                                v[0xF] = (byte)1;
                            }
                            else
                            {
                                v[0xF] = (byte)0;
                            }
                            break;
                        case 0x0005:
                            // subtract vy from vx
                            // borrow if vx is less than vy
                            if (v[x] > v[y])
                            {
                                v[0xF] = (byte)1;
                            }
                            else
                            {
                                v[0xF] = (byte)0;
                            }
                            v[x] -= v[y];
                            break;
                        case 0x0007:
                            // subtract vx from vy
                            // borrow if vy is less than vx
                            if (v[y] > v[x])
                            {
                                v[0xF] = (byte)1;
                            }
                            else
                            {
                                v[0xF] = (byte)0;
                            }
                            v[x] = (byte)(v[y] - v[x]);
                            break;
                        case 0x0006:
                            // shift vx right by 1
                            // ambiguous instruction
                            if (shift8XYSetVXtoVY)
                            {
                                v[x] = v[y];
                            }
                            v[x] = (byte)(v[x] >> 1);
                            v[0xF] = (byte)(v[x] & 0x1);
                            break;
                        case 0x000E:
                            // shift vx left by 1
                            // ambiguous instruction
                            if (shift8XYSetVXtoVY)
                            {
                                v[x] = v[y];
                            }
                            v[x] = (byte)(v[x] << 1);
                            v[0xF] = (byte)(v[x] & 0x80);
                            break;
                    }
                    break;
                case 0x9000:
                    // skip if vx and vy are not equal (9XY0)
                    if (v[x] != v[y])
                    {
                        pc += 2;
                    }
                    break;
                case 0xA000:
                    // set index register I
                    i = (ushort)(opcode & 0x0FFF);
                    break;
                case 0xB000:
                    // ambiguous instruction but hopefully we can get away with one
                    pc = (ushort)(opcode & 0x0FFF + v[0]);
                    break;
                case 0xC000:
                    // set vx to random number and NN
                    v[x] = (byte)(new Random().Next(0, 255) & (opcode & 0x00FF));
                    break;
                case 0xD000:
                    v[0xF] = 0;
                    for (int n = 0; n < (opcode & 0x000F); n++)
                    {
                        byte sprite = memory[i + n];
                        for (int m = 0; m < 8; m++)
                        {
                            // if current pixel in sprite row is on
                            // and the pixel in the display is on
                            if ((sprite & (0x80 >> m)) != 0)
                            {
                                if (emulator.setPixel(v[x] + m, v[y] + n))
                                {
                                    v[0xF] = 1;
                                }
                            }
                        }
                        // shift sprite left 1
                        sprite <<= 1;
                    }
                    break;
                case 0xE000:
                    switch (opcode & 0x00FF)
                    {
                        case 0x009E:
                            // skip if key with value of vx is pressed
                            if (emulator.isKeyPressed(v[x]))
                            {
                                pc += 2;
                            }
                            break;
                        case 0x00A1:
                            // skip if key with value of vx is not pressed
                            if (!emulator.isKeyPressed(v[x]))
                            {
                                pc += 2;
                            }
                            break;
                    }
                    break;
                case 0xF000:
                    switch (opcode & 0x00FF)
                    {
                        case 0x0007:
                            // set vx to delay timer
                            v[x] = (byte)delayTimer;
                            break;
                        case 0x0015:
                            // set delay timer to vx
                            delayTimer = v[x];
                            break;
                        case 0x0018:
                            // set sound timer to vx
                            delayTimer = v[x];
                            break;
                        case 0x001E:
                            // add VX to I
                            // overflow is ambiguous behavior but
                            // some games rely on it and it doesn't
                            // break much
                            // so we'll just let it overflow
                            i += v[x];
                            if (i > 0xFFF)
                            {
                                v[0xF] = 1;
                            }
                            else
                            {
                                v[0xF] = 0;
                            }

                            break;
                        case 0x000A:
                            // keep decrementing until a key is pressed
                            // then store the key in vx
                            // TODO: not exactly sure how chip8 handles key presses
                            if (!paused)
                            {
                                paused = true;
                                pc -= 2;
                            }
                            else
                            {
                                for (byte i = 0; i < 16; i++)
                                {
                                    if (emulator.isKeyPressed(i))
                                    {
                                        v[x] = i;
                                        paused = false;
                                        break;
                                    }
                                    else
                                    {
                                        pc -= 2;
                                    }
                                }
                            }
                            break;
                        case 0x0029:
                            // set I to the address of the hexadecimal sprite in VX
                            i = (ushort)(v[x] * 5);
                            break;
                        case 0x0033:
                            // store the binary-coded decimal representation of VX
                            // at the addresses I, I+1, and I+2
                            memory[i] = (byte)(v[x] / 100);
                            memory[i + 1] = (byte)((v[x] / 10) % 10);
                            memory[i + 2] = (byte)((v[x] % 100) % 10);
                            break;
                        case 0x0055:
                            // ambiguous instruction TODO:
                            // store V0 to VX in memory starting at address I, inclusive
                            for (int n = 0; n <= x; n++)
                            {
                                memory[i + n] = v[n];
                            }
                            break;
                        case 0x0065:
                            // ambiguous instruction
                            for (int n = 0; n <= x; n++)
                            {
                                v[n] = memory[i + n];
                            }
                            break;
                    }
                    break;
            }
        }
    }
}

using System;

namespace Unity.ProjectVania.Core.Input
{
    /// <summary>
    /// Every control's state for one 60 Hz step: which keys, pad buttons and mouse buttons are down, whether a pad is
    /// connected, and the wheel's direction this step. The original read one keyboard, pad and mouse state per frame
    /// (RECONSTRUCTION 3); an <see cref="IInputDevice"/> fills one of these per step.
    /// </summary>
    public sealed class InputFrame
    {
        // XNA's KeyboardState is 256 bits, one per virtual-key code.
        readonly ulong[] _keys = new ulong[4];
        uint _buttons;
        int _mouse;

        /// <summary>True while a pad is connected (the original read <c>GamePad.GetState(PlayerIndex.One)</c>).</summary>
        public bool PadConnected { get; set; }

        /// <summary>Wheel movement this step: positive is up (zoom in), negative down, 0 none. Only the sign is read.</summary>
        public int Wheel { get; set; }

        /// <summary>Whether the device reported where the mouse is this step (<see cref="MouseX"/>, <see cref="MouseY"/>).</summary>
        public bool HasMouse { get; private set; }

        /// <summary>The mouse on the 800 x 600 reference screen, pixels, y down (<c>Mouse.GetState().X</c>).</summary>
        public float MouseX { get; private set; }

        public float MouseY { get; private set; }

        /// <summary>Whether the game's window has the focus (<c>Game.IsActive</c>); the editor warps the mouse only then.</summary>
        public bool WindowActive { get; set; } = true;

        /// <summary>Reports the mouse at (<paramref name="x"/>, <paramref name="y"/>) on the reference screen.</summary>
        public InputFrame SetMouse(float x, float y)
        {
            HasMouse = true;
            MouseX = x;
            MouseY = y;
            return this;
        }

        public bool IsDown(Key key)
        {
            var code = (int)key;
            return code > 0 && code < 256 && (_keys[code >> 6] & (1UL << (code & 63))) != 0;
        }

        public bool IsDown(PadButton button) => (_buttons & (1u << (int)button)) != 0;

        public bool IsDown(MouseButton button) => (_mouse & (1 << (int)button)) != 0;

        public InputFrame Set(Key key, bool down = true)
        {
            var code = (int)key;
            if (code <= 0 || code >= 256)
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }

            var bit = 1UL << (code & 63);
            if (down)
            {
                _keys[code >> 6] |= bit;
            }
            else
            {
                _keys[code >> 6] &= ~bit;
            }

            return this;
        }

        public InputFrame Set(PadButton button, bool down = true)
        {
            var bit = 1u << (int)button;
            _buttons = down ? _buttons | bit : _buttons & ~bit;
            return this;
        }

        public InputFrame Set(MouseButton button, bool down = true)
        {
            var bit = 1 << (int)button;
            _mouse = down ? _mouse | bit : _mouse & ~bit;
            return this;
        }

        /// <summary>Every key down, in ascending code order (XNA's <c>GetPressedKeys</c> order).</summary>
        public void ForEachKeyDown(Action<Key> visit)
        {
            for (var code = 1; code < 256; code++)
            {
                if ((_keys[code >> 6] & (1UL << (code & 63))) != 0)
                {
                    visit((Key)code);
                }
            }
        }

        /// <summary>Releases everything.</summary>
        public void Clear()
        {
            Array.Clear(_keys, 0, _keys.Length);
            _buttons = 0;
            _mouse = 0;
            PadConnected = false;
            Wheel = 0;
            HasMouse = false;
            MouseX = 0f;
            MouseY = 0f;
            WindowActive = true;
        }

        public void CopyFrom(InputFrame other)
        {
            Array.Copy(other._keys, _keys, _keys.Length);
            _buttons = other._buttons;
            _mouse = other._mouse;
            PadConnected = other.PadConnected;
            Wheel = other.Wheel;
            HasMouse = other.HasMouse;
            MouseX = other.MouseX;
            MouseY = other.MouseY;
            WindowActive = other.WindowActive;
        }
    }

    /// <summary>
    /// This step's frame and the last step's, giving the original's edge tests: "pressed" is down now and up the step
    /// before (<c>IsKeyDown(k) &amp;&amp; WasKeyUp(k)</c>), per control, never per action.
    /// </summary>
    public sealed class InputState
    {
        public InputFrame Current { get; } = new InputFrame();
        public InputFrame Previous { get; } = new InputFrame();

        /// <summary>Moves the current frame to previous; the caller then fills <see cref="Current"/>.</summary>
        public void Shift()
        {
            Previous.CopyFrom(Current);
            Current.Clear();
        }

        public bool Down(Key key) => Current.IsDown(key);
        public bool Pressed(Key key) => Current.IsDown(key) && !Previous.IsDown(key);
        public bool Down(PadButton button) => Current.IsDown(button);
        public bool Pressed(PadButton button) => Current.IsDown(button) && !Previous.IsDown(button);
        public bool Down(MouseButton button) => Current.IsDown(button);
        public bool Pressed(MouseButton button) => Current.IsDown(button) && !Previous.IsDown(button);

        /// <summary>A pad connected this step (it was not connected the step before).</summary>
        public bool PadJustConnected => Current.PadConnected && !Previous.PadConnected;
    }
}

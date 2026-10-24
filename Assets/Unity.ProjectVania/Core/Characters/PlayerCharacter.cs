using System;
using Unity.ProjectVania.Core.Settings;
using Unity.ProjectVania.Core.World;

namespace Unity.ProjectVania.Core.Characters
{
    /// <summary>Where the character stands (<c>Player.TerrainState</c>, RECONSTRUCTION 4.5). Hanging is declared and never used.</summary>
    public enum TerrainState
    {
        InAir,
        OnGround,
        OnWall,
        Hanging,
    }

    /// <summary>
    /// One character's physics and per-frame state machine: the original's <c>Player\Player.cs</c> (RECONSTRUCTION
    /// 4.2-4.7, 4.9), copied frame for frame in the original's own units (pixels, y down, position at the bottom centre
    /// of the feet, velocities in pixels per second, gravity and drag per frame). <see cref="Step"/> is
    /// <c>Player.Update</c>: collisions and the terrain state machine, drag, bounds, position, then the current action.
    /// The command methods are the original's <c>Initiate_*</c> / <c>Cancel_*</c> transitions, called by the world when
    /// it applies a command, before the step's update, as the original's InputManager ran before its SceneManager.
    /// <para>
    /// Fixed defects (BIBLE, RECONSTRUCTION 15): 1, a kneeling jump starts the slide with <see cref="IsJumping"/> clear,
    /// so the ordinary ground checks apply and the character is not trapped in the slide; 2, the collision size the
    /// slide shrinks to is restored the moment the character leaves the slide; 3, owner decision 2026-10-23
    /// (<c>player.json</c>'s <c>kneel.releaseStandsUp</c>), releasing down now stands the character up instead of
    /// holding the kneel forever as the original did. Kept as the original: 6 (a wall jump climbs along the wall
    /// until contact is lost) and 7 (a terminal-speed landing while moving stays Falling on the ground for one frame).
    /// </para>
    /// </summary>
    public sealed class PlayerCharacter
    {
        readonly PlayerData _t;
        readonly int _height;
        readonly int _width;

        PixelVector _position;
        PixelVector _velocity;

        // The original's elapsed field: set at the start of each Update and read by Initiate_Jump's slide launch, so a
        // kneeling jump launched before the first update moves nothing, as in the original.
        float _elapsed;

        bool _wasJumping;

        // Owner decision 2026-10-23 (BIBLE defect 3, player.json kneel.releaseStandsUp): set whenever Kneel() runs
        // during this step's commands (S held, even on a repeat frame that early-returns) and cleared at the end of
        // the step, so the idle-stand check below can tell "held this frame" from "released", without that same
        // check undoing the very frame a kneel starts (Movement is 0 either way) or pre-empting a same-frame slide
        // jump, whose Jump() call still sees Kneeling before this step's physics run.
        bool _kneelHeldThisStep;

        public PlayerCharacter(PlayerData tuning, Pair unitSize, PixelVector spawn)
        {
            _t = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _width = unitSize.XInt;
            _height = unitSize.YInt;
            if (_width < 1 || _height < 1) throw new ArgumentOutOfRangeException(nameof(unitSize), "A character is at least one cell each way.");
            UnitWidth = _width;
            UnitHeight = _height;
            Reset(spawn);
        }

        /// <summary>The bottom centre of the feet, in pixels, y down.</summary>
        public PixelVector Position => _position;

        /// <summary>Pixels per second, y down.</summary>
        public PixelVector Velocity => _velocity;

        /// <summary>-1 left, +1 right, 0 before the first move (treated as right by the wall test).</summary>
        public int Facing { get; private set; }

        /// <summary>The held direction: -1, 0 or +1 (<c>movement</c>).</summary>
        public int Movement { get; private set; }

        public ActionKind Action { get; private set; }

        public TerrainState Terrain { get; private set; }

        public bool IsJumping { get; private set; }

        /// <summary>Set by a jump started from a wall: inverts the horizontal acceleration and the sprite's flip.</summary>
        public bool IsWallJumping { get; private set; }

        public float JumpTime { get; private set; }

        public float FallTime { get; private set; }

        public float WallSlideTime { get; private set; }

        /// <summary>The collision size in cells; the slide makes it 3 x 1 until the slide ends.</summary>
        public int UnitWidth { get; private set; }

        public int UnitHeight { get; private set; }

        /// <summary>The last step's wall count: how many cells ahead, stacked from the foot row, were Hard.</summary>
        public int WallContact { get; private set; }

        /// <summary>Whether the last step found a Hard cell under the feet.</summary>
        public bool OnFloor { get; private set; }

        // ---- Commands (Initiate_* / Cancel_*) ----

        /// <summary><c>Player.Reset</c> (R): the position, zero velocity, Falling in the air. Jump flags and movement are kept.</summary>
        public void Reset(PixelVector position)
        {
            _position = position;
            _velocity = default;
            SetAction(ActionKind.Falling);
            Terrain = TerrainState.InAir;
        }

        /// <summary><c>Initiate_Move</c>: face and move that way; Running only on the ground and not jumping.</summary>
        public void Move(int direction)
        {
            Facing = Math.Sign(direction);
            Movement = Facing;
            if (Terrain == TerrainState.OnGround && !IsJumping)
            {
                SetAction(ActionKind.Running);
            }
        }

        /// <summary><c>Cancel_Move</c> (<c>Command_Stand</c>): no direction held.</summary>
        public void StopMoving() => Movement = 0;

        /// <summary><c>Initiate_Kneel</c>: kneel when still, slide when moving; never on a terminal-speed landing while moving.</summary>
        public void Kneel()
        {
            _kneelHeldThisStep = true;
            if (Action == ActionKind.Kneeling || !_t.Ability.Kneel) return;
            if (Terrain != TerrainState.OnGround || IsJumping) return;

            if (Movement == 0)
            {
                SetAction(ActionKind.Kneeling);
            }
            else if (_velocity.Y != _t.MaxFallSpeed)
            {
                SetAction(ActionKind.Sliding);
            }

            _velocity.Y = 0;
        }

        /// <summary>
        /// <c>Command_Jump</c> and <c>Initiate_Jump</c>: only while not jumping, not already in the Jumping action, and
        /// with <see cref="JumpTime"/> back at exactly 0. From a kneel it launches a slide instead (defect 1 fixed: with
        /// <see cref="IsJumping"/> clear). There is no release: every jump runs its full curve.
        /// </summary>
        public void Jump()
        {
            if (IsJumping || Action == ActionKind.Jumping || JumpTime != 0f) return;

            if (_t.Ability.Jump) IsJumping = true;
            _wasJumping = false;
            if (!_t.Ability.WallJump && Terrain == TerrainState.OnWall) IsJumping = false;

            if (Action == ActionKind.Kneeling)
            {
                _velocity.X = Facing * _t.SlideLaunchAcceleration * _elapsed;
                IsJumping = false;
                SetAction(ActionKind.Sliding);
            }
            else
            {
                SetAction(ActionKind.Jumping);
            }
        }

        // ---- The frame (Player.Update) ----

        /// <summary>One frame of <c>Player.Update</c> on <paramref name="map"/>, <paramref name="elapsed"/> seconds long.</summary>
        public void Step(Map map, float elapsed)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            _elapsed = elapsed;
            CheckCollisions(map, elapsed);
            ApplyDrag();
            CheckBounds(map);
            _position.X += _velocity.X * elapsed;
            _position.Y += _velocity.Y * elapsed;
            CheckActionState(elapsed);
            _kneelHeldThisStep = false;
        }

        void CheckCollisions(Map map, float elapsed)
        {
            // CheckWallCollision works on a copy, so its snap is lost; only the count matters.
            var copy = _position;
            WallContact = map.CheckHorizontalCollision(ref copy, Facing, UnitHeight);
            if (WallContact > 0 && !IsJumping) _velocity.X = 0;

            OnFloor = map.CheckFloorCollision(ref _position);
            bool ceiling = map.CheckCeilingCollision(ref _position, _t.CeilingHeightCells);

            if (WallContact == 0)
            {
                _velocity.X += Movement * _t.MoveAcceleration * elapsed;
                _velocity.X = Clamp(_velocity.X, -_t.MaxMoveSpeed, _t.MaxMoveSpeed);
                if (IsWallJumping) _velocity.X = -_velocity.X;
            }
            else if (WallContact == UnitHeight)
            {
                // (The original first added 5 * -movement while wall jumping, then overwrote it.)
                _velocity.X = 0;
            }

            // A partial count (a step or ledge) neither accelerates nor stops the character; drag decays it.
            switch (Terrain)
            {
                case TerrainState.OnGround:
                    if (!IsJumping)
                    {
                        if (!OnFloor)
                        {
                            Fall();
                        }
                        else if (Movement == 0 &&
                                 (Action != ActionKind.Kneeling || (_t.KneelReleaseStandsUp && !_kneelHeldThisStep)))
                        {
                            Stand();
                        }
                    }
                    else if (ceiling)
                    {
                        // The whole rise of a ground jump stays OnGround; only the ceiling ends it early.
                        _velocity.Y = 0;
                        Fall();
                    }

                    break;

                case TerrainState.InAir:
                    if (WallContact != 0 && Movement != 0)
                    {
                        WallSlide();
                    }
                    else if (!OnFloor)
                    {
                        Fall();
                    }
                    else if (_velocity.Y == _t.MaxFallSpeed)
                    {
                        // A terminal-speed landing kneels (or, while moving, stays Falling on the ground: defect 7, kept).
                        Terrain = TerrainState.OnGround;
                        Kneel();
                    }
                    else
                    {
                        Stand();
                    }

                    break;

                case TerrainState.OnWall:
                    if (OnFloor)
                    {
                        Stand();
                    }
                    else if (WallContact == 0)
                    {
                        Facing = Facing == -1 ? 1 : -1;
                        Fall();
                    }
                    else if (Movement == 0)
                    {
                        if (WallSlideTime < _t.MaxWallSlideTime) WallSlideTime += elapsed;
                        else Fall();
                    }

                    break;
            }
        }

        void ApplyDrag()
        {
            _velocity.X *= Terrain == TerrainState.OnGround ? _t.GroundDragFactor : _t.AirDragFactor;
        }

        void CheckBounds(Map map)
        {
            if (_position.X < 0) { _position.X = 0; _velocity.X = 0; }
            if (_position.Y < 0) { _position.Y = 0; _velocity.Y = 0; }
            if (_position.X > map.Width) { _position.X = map.Width; _velocity.X = 0; }
            if (_position.Y > map.Height) { _position.Y = map.Height; _velocity.Y = 0; }
        }

        void CheckActionState(float elapsed)
        {
            switch (Action)
            {
                case ActionKind.Standing:
                    Movement = 0;
                    break;
                case ActionKind.Jumping:
                    ActionJump(elapsed);
                    break;
                case ActionKind.Falling:
                    _velocity.Y = Clamp(_velocity.Y + _t.GravityAcceleration, -_t.MaxFallSpeed, _t.MaxFallSpeed);
                    FallTime += elapsed;
                    break;
                case ActionKind.WallSliding:
                    if (_velocity.Y > _t.WallSlideSpeed) _velocity.Y *= _t.WallSlideDecay;
                    else _velocity.Y = _t.WallSlideSpeed;
                    break;
                case ActionKind.Sliding:
                    UnitWidth = _t.SlideUnitSize.XInt;
                    UnitHeight = _t.SlideUnitSize.YInt;
                    _velocity.X *= _t.SlideFriction;
                    break;

                // Running and Kneeling only choose their animation (and Running its step sound): presentation reads Action.
            }
        }

        void ActionJump(float elapsed)
        {
            if (IsJumping)
            {
                if (!_wasJumping || JumpTime > 0f) JumpTime += elapsed;

                if (JumpTime <= _t.MaxJumpTime)
                {
                    // The fixed power curve: the whole rise is decided by time, never by the key.
                    _velocity.Y = _t.JumpLaunchVelocity * (1f - (float)Math.Pow(JumpTime / _t.MaxJumpTime, _t.JumpControlPower));
                    if (Terrain == TerrainState.OnWall) IsWallJumping = true;
                }
                else
                {
                    Fall();
                }
            }

            if (!IsJumping && _wasJumping)
            {
                JumpTime = 0f;
                Fall();
            }

            _wasJumping = IsJumping;
        }

        /// <summary><c>Initiate_Stand</c>: lands, clearing the jump, the fall time and the vertical speed.</summary>
        void Stand()
        {
            if (Action == ActionKind.Standing) return;
            SetAction(ActionKind.Standing);
            Terrain = TerrainState.OnGround;
            JumpTime = 0f;
            IsJumping = false;
            IsWallJumping = false;
            _wasJumping = false;
            FallTime = 0f;
            _velocity.Y = 0;
        }

        /// <summary><c>Initiate_Fall</c>: it does not reset the jump time or the fall time.</summary>
        void Fall()
        {
            IsJumping = false;
            IsWallJumping = false;
            WallSlideTime = 0f;
            SetAction(ActionKind.Falling);
            Terrain = TerrainState.InAir;
        }

        /// <summary><c>Initiate_WallSlide</c>: only while holding a direction.</summary>
        void WallSlide()
        {
            if (Action == ActionKind.WallSliding || !_t.Ability.WallSlide || Movement == 0) return;
            IsJumping = false;
            JumpTime = 0f;
            SetAction(ActionKind.WallSliding);
            Terrain = TerrainState.OnWall;
        }

        void SetAction(ActionKind action)
        {
            // Defect 2 fixed: the slide's 3 x 1 collision size ends with the slide.
            if (Action == ActionKind.Sliding && action != ActionKind.Sliding)
            {
                UnitWidth = _width;
                UnitHeight = _height;
            }

            Action = action;
        }

        static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}

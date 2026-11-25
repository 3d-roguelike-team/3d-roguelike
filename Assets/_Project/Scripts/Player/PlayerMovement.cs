using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike.Player
{
    /// <summary>
    /// First-person CharacterController movement:
    /// walk with acceleration/friction, jump, double jump, wall jump, dash,
    /// dash-jump momentum carry, slide, ground slam and slam jump.
    /// Tuning lives in <see cref="PlayerMovementDefinition"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;

        // Braking is integrated in small steps so stopping distance doesn't change with frame rate.
        private const float MaxBrakingStep = 1f / 33f;

        private static readonly Vector3[] WallCheckDirections =
        {
            new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f),
            new Vector3(0.7071f, 0f, 0.7071f), new Vector3(-0.7071f, 0f, 0.7071f),
            new Vector3(0.7071f, 0f, -0.7071f), new Vector3(-0.7071f, 0f, -0.7071f),
        };

        [SerializeField] private PlayerMovementDefinition _definition;

        [Header("Input")]
        [SerializeField] private InputActionReference _moveAction;
        [SerializeField] private InputActionReference _jumpAction;
        [SerializeField] private InputActionReference _dashAction;
        [SerializeField] private InputActionReference _slideAction;
        [Tooltip("Unbound by default. Candidate for the Utility ability.")]
        [SerializeField] private InputActionReference _slamAction;

        [Header("Collision")]
        [Tooltip("Level geometry: used for wall jumps and stepping down ledges. Do NOT include the Player layer.")]
        [SerializeField] private LayerMask _environmentMask;

        private readonly RaycastHit[] _groundHits = new RaycastHit[8];

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _walkableNormalY;
        private float _standingHeight;
        private Vector3 _standingCenter;

        private bool _grounded;
        private bool _landedPending;
        private bool _forceAirborne;

        private Vector3 _groundNormal = Vector3.up;
        private Vector3 _contactGroundNormal;

        private bool _collectingHits;
        private bool _hasWalkableContact;
        private bool _hasBlockingHit;
        private Vector3 _blockingNormal;
        private float _blockingOpposition;

        private bool _doubleJumpAvailable = true;
        private bool _airDashAvailable = true;
        private bool _wallJumpAvailable = true;

        private bool _dashing;
        private bool _dashFromGround;
        private Vector3 _dashDirection;
        private float _dashTimeRemaining;
        private float _dashCooldownRemaining;

        private bool _sliding;
        private bool _lowered;
        private Vector3 _slideDirection;
        private float _slideElapsed;
        private float _slideCooldownRemaining;
        private float _slideKickCooldownRemaining;

        private bool _slamming;
        private float _slamTime;
        private float _slamJumpBufferRemaining;
        private float _slamPower;
        private float _slamPowerRemaining;

        public event Action Jumped;
        public event Action<bool> DoubleJumped;               // (fromDash)
        public event Action<Vector3, Vector3, bool> WallJumped; // (point, wallNormal, fromDash)
        public event Action<Vector3> Dashed;                  // (direction)
        public event Action DashEnded;
        public event Action<Vector3> SlideStarted;            // (direction)
        public event Action SlideEnded;
        public event Action SlamStarted;
        public event Action<float> SlamLanded;                // (stored jump power)
        public event Action SlamJumped;
        public event Action<float, bool> Landed;              // (impact speed, isHardLanding)

        /// <summary>Turn off to ignore player input (menus, shop UI). Gravity still applies.</summary>
        public bool InputEnabled { get; set; } = true;

        public Vector3 Velocity => _velocity;
        public Vector3 HorizontalVelocity => Flatten(_velocity);
        public bool IsGrounded => _grounded;
        public bool IsDashing => _dashing;
        public bool IsSliding => _sliding;

        /// <summary>True while the capsule is short: sliding, or still under something after a slide.</summary>
        public bool IsLowered => _lowered;
        public bool IsSlamming => _slamming;
        public float DashCooldownRemaining => _dashCooldownRemaining;
        public bool DoubleJumpAvailable => _doubleJumpAvailable;
        public bool AirDashAvailable => _airDashAvailable;
        public bool WallJumpAvailable => _wallJumpAvailable;

        /// <summary>Adds to current velocity (knockback, jump pads). Upward pushes leave the ground.</summary>
        public void AddVelocity(Vector3 delta)
        {
            // Knockback interrupts a dash or slam; otherwise their fixed velocity would overwrite it next frame.
            CancelDash();
            _slamming = false;
            _velocity += delta;
            if (delta.y > 0f)
            {
                LaunchAirborne();
            }
        }

        /// <summary>Replaces velocity entirely and cancels dash/slam (launchers, explosions).</summary>
        public void Launch(Vector3 velocity)
        {
            CancelDash();
            _slamming = false;
            _velocity = velocity;
            LaunchAirborne();
        }

        /// <summary>
        /// Gives back double jump, air dash and wall jump. Intended for rewards such as
        /// hitting an enemy in mid-air.
        /// </summary>
        public void RestoreAirAbilities()
        {
            _doubleJumpAvailable = true;
            _airDashAvailable = true;
            _wallJumpAvailable = true;
        }

        public void Teleport(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _velocity = Vector3.zero;
            EndSlide();
            CancelDash();
            _slamming = false;
        }

        private void Reset()
        {
            // Runs when the component is first added in the Editor: sets the default capsule,
            // with the pivot at the feet (Unity convention).
            var controller = GetComponent<CharacterController>();
            controller.height = 1.92f;
            controller.radius = 0.55f;
            controller.center = new Vector3(0f, 0.96f, 0f);
            controller.stepOffset = 0.45f;
            controller.slopeLimit = 44.77f;
            controller.skinWidth = 0.055f;
            controller.minMoveDistance = 0f;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _walkableNormalY = Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad);
            _standingHeight = _controller.height;
            _standingCenter = _controller.center;

            if (_definition == null)
            {
                Debug.LogError($"{nameof(PlayerMovement)} on '{name}' has no {nameof(PlayerMovementDefinition)} assigned.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            EnableAction(_moveAction);
            EnableAction(_jumpAction);
            EnableAction(_dashAction);
            EnableAction(_slideAction);
            EnableAction(_slamAction);
        }

        private void OnDisable()
        {
            _dashing = false;
            _sliding = false;
            _slamming = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            TickTimers(dt);

            // Landing has to be handled before input: a held Jump re-jumps this frame, and doing that
            // first would skip the landing (no ability refill, no Landed event).
            RefreshGroundState();

            if (Pressed(_jumpAction) || (_definition.AutoJumpWhileHeld && Held(_jumpAction) && _grounded))
            {
                TryJump();
            }

            if (Pressed(_dashAction))
            {
                TryDash();
            }

            if (Pressed(_slideAction))
            {
                TryStartSlide();
            }

            if (Pressed(_slamAction))
            {
                TryStartSlam();
            }

            Simulate(dt);
        }

        private void TickTimers(float dt)
        {
            _dashCooldownRemaining = Mathf.Max(0f, _dashCooldownRemaining - dt);
            _slideCooldownRemaining = Mathf.Max(0f, _slideCooldownRemaining - dt);
            _slideKickCooldownRemaining = Mathf.Max(0f, _slideKickCooldownRemaining - dt);
            _slamJumpBufferRemaining = Mathf.Max(0f, _slamJumpBufferRemaining - dt);
            _slamPowerRemaining = Mathf.Max(0f, _slamPowerRemaining - dt);
        }

        private void RefreshGroundState()
        {
            if (_grounded)
            {
                _doubleJumpAvailable = true;
                _wallJumpAvailable = true;
                if (!_dashing)
                {
                    _airDashAvailable = true;
                }

                if (_landedPending)
                {
                    _landedPending = false;
                    HandleLanding(-_velocity.y);

                    if (_definition.SlideOnLandWhileHeld && Held(_slideAction))
                    {
                        TryStartSlide();
                    }
                }

                if (_velocity.y < 0f)
                {
                    _velocity.y = 0f;
                }
            }
        }

        private void Simulate(float dt)
        {
            if (_dashing)
            {
                TickDash(dt);
            }
            else if (_sliding)
            {
                TickSlide(dt);
            }
            else if (_slamming)
            {
                TickSlam(dt);
            }
            else
            {
                TickWalkOrFall(dt);
            }

            if (_lowered && !_sliding && HasStandingRoom())
            {
                SetLowered(false);
            }

            _forceAirborne = false;
        }

        // ------------------------------------------------------------------ Walking / falling

        private void TickWalkOrFall(float dt)
        {
            Vector3 wish = WishDirection(out float inputAmount);
            Vector3 horizontal = Flatten(_velocity);
            bool grounded = _grounded && !_forceAirborne;

            if (grounded)
            {
                horizontal = CalcVelocity(horizontal, wish, inputAmount, dt,
                    _definition.GroundFriction, _definition.BrakingDeceleration, _definition.WalkSpeed);
                _velocity = horizontal;

                Vector3 start = transform.position;
                Move(new Vector3(horizontal.x, -_definition.GroundStickSpeed, horizontal.z) * dt);

                // Use the distance actually travelled, so running into a wall kills your speed
                // instead of storing it up.
                if (_grounded)
                {
                    Vector3 actual = (transform.position - start) / dt;
                    _velocity = new Vector3(actual.x, 0f, actual.z);
                }

                return;
            }

            Vector3 oldVelocity = _velocity;
            horizontal = CalcVelocity(horizontal, wish, inputAmount * _definition.AirControl, dt,
                _definition.AirLateralFriction, 0f, _definition.WalkSpeed);
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;
            _velocity.y = Mathf.Max(_velocity.y - _definition.Gravity * dt, -_definition.TerminalFallSpeed);

            // Average of old and new velocity: jump arcs come out the same at any frame rate.
            Move(0.5f * (oldVelocity + _velocity) * dt);
        }

        private Vector3 CalcVelocity(Vector3 horizontal, Vector3 wish, float inputAmount, float dt,
            float friction, float brakingDeceleration, float maxSpeed)
        {
            float oldSpeed = horizontal.magnitude;
            bool hasInput = inputAmount > 0.001f && wish.sqrMagnitude > 0.001f;
            bool overMax = oldSpeed > maxSpeed + Epsilon;

            if (!hasInput || overMax)
            {
                Vector3 beforeBraking = horizontal;
                horizontal = ApplyBraking(horizontal, dt, friction, brakingDeceleration);

                // Braking from above max speed never drops you below it while you hold forward.
                if (overMax && hasInput
                    && horizontal.sqrMagnitude < maxSpeed * maxSpeed
                    && Vector3.Dot(wish, beforeBraking) > 0f)
                {
                    horizontal = beforeBraking.normalized * maxSpeed;
                }
            }
            else if (oldSpeed > Epsilon && friction > 0f)
            {
                // Turns velocity toward the input direction without losing speed.
                Vector3 desired = wish * oldSpeed;
                horizontal -= (horizontal - desired) * Mathf.Min(dt * friction, 1f);
            }

            if (hasInput)
            {
                // Input can accelerate you up to max speed, or steer you while you're faster.
                float allowedSpeed = Mathf.Max(horizontal.magnitude, maxSpeed);
                horizontal += wish * (_definition.MaxAcceleration * inputAmount * dt);
                if (horizontal.magnitude > allowedSpeed && allowedSpeed > 0f)
                {
                    horizontal = horizontal.normalized * allowedSpeed;
                }
            }

            return horizontal;
        }

        private Vector3 ApplyBraking(Vector3 horizontal, float dt, float friction, float brakingDeceleration)
        {
            if (horizontal.sqrMagnitude <= Epsilon * Epsilon)
            {
                return Vector3.zero;
            }

            float effectiveFriction = Mathf.Max(0f, friction) * _definition.BrakingFrictionFactor;
            Vector3 original = horizontal;
            float remaining = dt;

            while (remaining > 0.000001f)
            {
                float step = remaining > MaxBrakingStep && effectiveFriction > 0f
                    ? Mathf.Min(MaxBrakingStep, remaining * 0.5f)
                    : remaining;
                remaining -= step;

                Vector3 reverse = brakingDeceleration > 0f
                    ? -horizontal.normalized * brakingDeceleration
                    : Vector3.zero;
                horizontal += (-effectiveFriction * horizontal + reverse) * step;

                if (Vector3.Dot(horizontal, original) <= 0f)
                {
                    return Vector3.zero;
                }
            }

            float stopSpeed = _definition.BrakeToStopSpeed;
            if (brakingDeceleration > 0f && horizontal.sqrMagnitude <= stopSpeed * stopSpeed)
            {
                return Vector3.zero;
            }

            return horizontal;
        }

        // ------------------------------------------------------------------ Jumping

        private void TryJump()
        {
            // Already launched this frame (e.g. a slam jump fired on landing): one jump per frame.
            if (_forceAirborne)
            {
                return;
            }

            if (_slamming)
            {
                // Queue it: the slam turns into a slam jump if it lands soon.
                _slamJumpBufferRemaining = _definition.SlamJumpBufferTime;
                return;
            }

            // Jumping out of a dash that started on the ground is a normal jump that keeps the dash's speed,
            // as long as the floor is still under you. Double jump stays available for later.
            if (_dashing && _dashFromGround && FindGroundBelow(_controller.stepOffset, out _, out _))
            {
                CancelDash();
                _velocity.y = _definition.JumpSpeed;
                LaunchAirborne();
                Jumped?.Invoke();
                return;
            }

            if (!_grounded)
            {
                if (TryWallJump())
                {
                    return;
                }

                if (_definition.DoubleJumpEnabled && _doubleJumpAvailable)
                {
                    bool fromDash = _dashing;

                    // Cancelling the dash keeps its full speed: this is the dash-jump.
                    CancelDash();
                    _doubleJumpAvailable = false;
                    _airDashAvailable = true;
                    _velocity.y = _definition.JumpSpeed;
                    LaunchAirborne();
                    DoubleJumped?.Invoke(fromDash);
                }

                return;
            }

            if (_slamPowerRemaining > 0f)
            {
                LaunchSlamJump();
                return;
            }

            _velocity.y = _definition.JumpSpeed;
            LaunchAirborne();
            Jumped?.Invoke();
        }

        private bool TryWallJump()
        {
            if (!_definition.WallJumpEnabled || !_wallJumpAvailable
                || !TryFindWall(out Vector3 wallNormal, out Vector3 wallPoint))
            {
                return false;
            }

            bool fromDash = _dashing;
            if (fromDash)
            {
                // Dash + wall jump: a big launch in the direction you're holding.
                Vector3 direction = WishDirection();
                if (direction.sqrMagnitude <= Epsilon)
                {
                    direction = Flatten(transform.forward).normalized;
                }

                _velocity = direction * _definition.DashWallJumpSpeed;
                _velocity.y += _definition.DashWallJumpUpSpeed;
                CancelDash();
            }
            else
            {
                // Normal wall jump: push off the wall on top of your current speed.
                Vector3 horizontal = Flatten(_velocity) + wallNormal * _definition.WallJumpPushSpeed;
                _velocity = new Vector3(horizontal.x, _definition.JumpSpeed, horizontal.z);
            }

            LaunchAirborne();
            _wallJumpAvailable = false;
            _doubleJumpAvailable = true;
            _airDashAvailable = true;
            WallJumped?.Invoke(wallPoint, wallNormal, fromDash);
            return true;
        }

        private bool TryFindWall(out Vector3 wallNormal, out Vector3 wallPoint)
        {
            Vector3 origin = ControllerCenter;
            float bestDistance = float.MaxValue;
            wallNormal = Vector3.zero;
            wallPoint = origin;

            for (int i = 0; i < WallCheckDirections.Length; i++)
            {
                if (!Physics.Raycast(origin, WallCheckDirections[i], out RaycastHit hit,
                        _definition.WallCheckDistance, _environmentMask, QueryTriggerInteraction.Ignore)
                    || IsSelf(hit.collider)
                    || hit.distance >= bestDistance)
                {
                    continue;
                }

                Vector3 flatNormal = Flatten(hit.normal);
                if (flatNormal.sqrMagnitude <= Epsilon)
                {
                    continue;
                }

                bestDistance = hit.distance;
                wallNormal = flatNormal.normalized;
                wallPoint = hit.point;
            }

            return bestDistance < float.MaxValue;
        }

        // ------------------------------------------------------------------ Dash

        private void TryDash()
        {
            if (!_definition.DashEnabled || _dashing)
            {
                return;
            }

            bool grounded = _grounded && !_forceAirborne;
            if (grounded && _dashCooldownRemaining > 0f)
            {
                return;
            }

            if (!grounded && (!_definition.AirDashEnabled || !_airDashAvailable))
            {
                return;
            }

            EndSlide();

            // Every dash, ground or air, uses up the air dash until you land or double/wall jump.
            _airDashAvailable = false;
            _dashCooldownRemaining = _definition.GroundDashCooldown;

            Vector3 input = WishDirection();
            _dashDirection = input.sqrMagnitude > 0.01f
                ? input.normalized
                : Flatten(transform.forward).normalized;

            _dashing = true;
            _dashFromGround = grounded;
            _slamming = false;
            _dashTimeRemaining = _definition.DashDuration;
            _velocity = _dashDirection * _definition.DashSpeed;
            Dashed?.Invoke(_dashDirection);
        }

        private void TickDash(float dt)
        {
            // No gravity and no steering during a dash. The last frame only covers the time that's left,
            // so the dash is the same distance at any frame rate.
            float step = Mathf.Min(dt, _dashTimeRemaining);
            _velocity = _dashDirection * _definition.DashSpeed;
            Move(_velocity * step);
            _dashTimeRemaining -= step;

            if (_dashTimeRemaining <= 0f)
            {
                // Ending normally drops you back to walk speed. Jumping out of the dash keeps full speed.
                _dashing = false;
                _velocity = _dashDirection * _definition.WalkSpeed;

                // A ground dash that ends over the floor never really left it, so it isn't a landing.
                if (_dashFromGround && TrySnapDown(_controller.stepOffset))
                {
                    _grounded = true;
                }

                DashEnded?.Invoke();
            }
        }

        /// <summary>Stops a dash early and keeps its current velocity.</summary>
        private void CancelDash()
        {
            if (!_dashing)
            {
                return;
            }

            _dashing = false;
            DashEnded?.Invoke();
        }

        // ------------------------------------------------------------------ Slide

        private void TryStartSlide()
        {
            if (!_definition.SlideEnabled || _sliding || _slamming || _slideCooldownRemaining > 0f)
            {
                return;
            }

            if (!_grounded || _forceAirborne)
            {
                // A press just above the floor still counts, so a ground dash can go straight into a slide.
                if (_forceAirborne || _velocity.y > 0f || !TrySnapDown(_definition.SlideEntryGroundSnap))
                {
                    return;
                }

                _grounded = true;
                _velocity.y = 0f;
            }

            Vector3 horizontal = Flatten(_velocity);
            float speed = horizontal.magnitude;

            // Already faster than walking: keep your momentum's direction (WASD then steers it gradually).
            // Slower: slide the way you're holding, else the way you're moving, else where you're facing.
            Vector3 direction;
            if (speed > _definition.WalkSpeed)
            {
                direction = horizontal;
            }
            else
            {
                direction = WishDirection();
                if (direction.sqrMagnitude <= 0.01f)
                {
                    direction = speed > 1f ? horizontal : Flatten(transform.forward);
                }
            }

            if (direction.sqrMagnitude <= Epsilon)
            {
                direction = Vector3.forward;
            }

            if (_dashing)
            {
                speed = Mathf.Max(speed, _definition.DashSpeed) + _definition.SlideDashBoostSpeed;
            }
            else if (speed < _definition.SlideStartSpeed && _slideKickCooldownRemaining <= 0f)
            {
                speed = _definition.SlideStartSpeed;
                _slideKickCooldownRemaining = _definition.SlideKickCooldown;
            }

            // Too slow to be worth a slide (kick still on cooldown).
            if (speed < _definition.SlideEndSpeed)
            {
                return;
            }

            CancelDash();
            _sliding = true;
            _slideElapsed = 0f;
            _slideDirection = direction.normalized;
            _velocity = _slideDirection * speed;
            SetLowered(true);
            SlideStarted?.Invoke(_slideDirection);
        }

        private void TickSlide(float dt)
        {
            if (!_grounded)
            {
                EndSlide();
                TickWalkOrFall(dt);
                return;
            }

            _slideElapsed += dt;
            float maxDuration = _definition.SlideMaxDuration;
            if (!Held(_slideAction) || (maxDuration > 0f && _slideElapsed >= maxDuration))
            {
                EndSlide();
                TickWalkOrFall(dt);
                return;
            }

            Vector3 horizontal = Flatten(_velocity);
            float speed = horizontal.magnitude;
            Vector3 direction = speed > Epsilon ? horizontal / speed : _slideDirection;

            // WASD bends the slide's path a little. It never adds speed.
            Vector3 wish = WishDirection();
            if (wish.sqrMagnitude > 0.01f)
            {
                float maxRadians = _definition.SlideSteerDegreesPerSecond * Mathf.Deg2Rad * dt;
                direction = Vector3.RotateTowards(direction, wish.normalized, maxRadians, 0f).normalized;
            }

            // Friction plus drag: fast slides bleed speed quicker, slow ones fade out.
            speed = Mathf.Max(0f, speed - (_definition.SlideFriction + _definition.SlideDrag * speed) * dt);
            horizontal = direction * speed;

            // Slopes: downhill speeds you up, uphill slows you down.
            Vector3 slopePull = Flatten(Vector3.ProjectOnPlane(
                Vector3.down * (_definition.Gravity * _definition.SlideGravityScale), _groundNormal));
            horizontal += slopePull * dt;

            // Slopes can't push you past the top slide speed (speed you already had is kept).
            float cap = Mathf.Max(_definition.SlideMaxSpeed, speed);
            if (horizontal.sqrMagnitude > cap * cap)
            {
                horizontal = horizontal.normalized * cap;
            }

            _velocity = horizontal;
            if (horizontal.sqrMagnitude > Epsilon)
            {
                _slideDirection = horizontal.normalized;
            }

            Vector3 start = transform.position;
            Move(new Vector3(horizontal.x, -_definition.GroundStickSpeed, horizontal.z) * dt);

            if (!_grounded)
            {
                // Slid off a ledge: keep the momentum and fall.
                EndSlide();
                return;
            }

            // Use the distance actually travelled, so sliding into a wall stops you.
            Vector3 actual = (transform.position - start) / dt;
            _velocity = new Vector3(actual.x, 0f, actual.z);

            if (Flatten(_velocity).magnitude < _definition.SlideEndSpeed)
            {
                EndSlide();
            }
        }

        private void EndSlide()
        {
            if (!_sliding)
            {
                return;
            }

            // The capsule stays short until there's room to stand (see Simulate).
            _sliding = false;
            _slideCooldownRemaining = _definition.SlideCooldown;
            SlideEnded?.Invoke();
        }

        private void SetLowered(bool lowered)
        {
            _lowered = lowered;

            // CharacterController can't be shorter than its own width.
            float height = lowered
                ? Mathf.Max(_definition.SlideHeight, _controller.radius * 2f)
                : _standingHeight;

            // Shrink from the top so your feet stay on the floor.
            _controller.height = height;
            _controller.center = _standingCenter + Vector3.down * ((_standingHeight - height) * 0.5f);
        }

        private bool HasStandingRoom()
        {
            float skin = _controller.skinWidth;
            float radius = Mathf.Max(0.01f, _controller.radius - skin);
            float sphereOffset = Mathf.Max(0f, _standingHeight * 0.5f - _controller.radius);
            Vector3 center = transform.TransformPoint(_standingCenter);
            Vector3 bottom = center + Vector3.down * sphereOffset + Vector3.up * skin;
            Vector3 top = center + Vector3.up * sphereOffset;

            return !Physics.CheckCapsule(bottom, top, radius, _environmentMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ Ground slam

        private void TryStartSlam()
        {
            if (!_definition.SlamEnabled || _grounded || _slamming)
            {
                return;
            }

            CancelDash();
            _slamming = true;
            _slamTime = 0f;
            _velocity = Vector3.down * _definition.SlamSpeed;
            SlamStarted?.Invoke();
        }

        private void TickSlam(float dt)
        {
            _velocity = Vector3.down * _definition.SlamSpeed;
            Move(_velocity * dt);
            _slamTime += dt;
        }

        private void EndSlam()
        {
            _slamming = false;

            // The longer the slam fell, the higher the slam jump.
            _slamPower = Mathf.Sqrt(2f * _slamTime * _definition.SlamSpeed * _definition.Gravity)
                + _definition.SlamJumpBonusSpeed;
            _slamPowerRemaining = _definition.SlamPowerWindow;
            SlamLanded?.Invoke(_slamPower);

            if (_slamJumpBufferRemaining > 0f)
            {
                LaunchSlamJump();
            }
        }

        private void LaunchSlamJump()
        {
            _slamJumpBufferRemaining = 0f;
            _slamTime = 0f;
            _velocity.y = _slamPower < _definition.SlamJumpMinimumPower
                ? _definition.JumpSpeed
                : Mathf.Max(_definition.JumpSpeed, _slamPower);
            LaunchAirborne();
            SlamJumped?.Invoke();
        }

        // ------------------------------------------------------------------ Collision

        private void HandleLanding(float impactSpeed)
        {
            if (_slamming)
            {
                EndSlam();
            }

            Landed?.Invoke(impactSpeed, impactSpeed > _definition.HardLandingSpeed);
        }

        private void Move(Vector3 delta)
        {
            bool wasGrounded = _grounded;

            _hasWalkableContact = false;
            _contactGroundNormal = Vector3.up;
            _hasBlockingHit = false;
            _blockingOpposition = 0f;
            _collectingHits = true;
            CollisionFlags flags = _controller.Move(delta);
            _collectingHits = false;

            bool grounded = (flags & CollisionFlags.Below) != 0;

            // Only land on walkable floor under your feet, not on a ledge corner or steep slope.
            if (!wasGrounded && grounded && !_hasWalkableContact)
            {
                grounded = false;
            }

            // Walking off a small step: snap down to it instead of falling.
            if (!grounded && wasGrounded && !_forceAirborne && delta.y <= 0f && TrySnapDown(_controller.stepOffset))
            {
                grounded = true;
            }

            // The first frame of a jump still touches the floor; ignore that.
            if (_forceAirborne && _velocity.y > 0f)
            {
                grounded = false;
            }

            // Dashes always count as airborne, so you can dash off ledges and dash-jump.
            if (_dashing)
            {
                grounded = false;
            }

            // In the air, hitting a wall slides your velocity along it instead of keeping it.
            if (!grounded && _hasBlockingHit && _blockingOpposition > Epsilon)
            {
                _velocity = DeflectAlongSurface(_velocity, _blockingNormal);
            }

            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f)
            {
                _velocity.y = 0f;
            }

            if (!wasGrounded && grounded)
            {
                _landedPending = true;
            }

            if (_hasWalkableContact)
            {
                _groundNormal = _contactGroundNormal;
            }
            else if (!grounded)
            {
                _groundNormal = Vector3.up;
            }

            _grounded = grounded;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!_collectingHits)
            {
                return;
            }

            Vector3 normal = hit.normal;
            float opposition = -Vector3.Dot(_velocity, normal);
            if (opposition > _blockingOpposition)
            {
                _blockingOpposition = opposition;
                _blockingNormal = normal;
                _hasBlockingHit = true;
            }

            if (normal.y >= _walkableNormalY && hit.point.y <= BottomSphereCenter.y)
            {
                // With several floor contacts (e.g. where a ramp meets flat ground), use the flattest.
                if (!_hasWalkableContact || normal.y > _contactGroundNormal.y)
                {
                    _contactGroundNormal = normal;
                }

                _hasWalkableContact = true;
            }
        }

        private bool TrySnapDown(float maxDistance)
        {
            if (!FindGroundBelow(maxDistance, out float distance, out Vector3 normal))
            {
                return false;
            }

            _controller.Move(Vector3.down * distance);
            _groundNormal = normal;
            return true;
        }

        /// <summary>Looks for walkable floor up to <paramref name="maxDistance"/> below the capsule.</summary>
        private bool FindGroundBelow(float maxDistance, out float distance, out Vector3 normal)
        {
            float radius = _controller.radius;
            float castRadius = Mathf.Max(0.01f, radius - _controller.skinWidth);
            Vector3 origin = BottomSphereCenter;
            float castDistance = maxDistance + _controller.skinWidth;

            int count = Physics.SphereCastNonAlloc(origin, castRadius, Vector3.down, _groundHits, castDistance,
                _environmentMask, QueryTriggerInteraction.Ignore);

            distance = float.MaxValue;
            normal = Vector3.up;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _groundHits[i];
                if (hit.distance <= 0f || IsSelf(hit.collider) || hit.normal.y < _walkableNormalY
                    || hit.distance >= distance)
                {
                    continue;
                }

                distance = hit.distance;
                normal = hit.normal;
            }

            return distance < float.MaxValue;
        }

        private static Vector3 DeflectAlongSurface(Vector3 velocity, Vector3 normal)
        {
            Vector3 slide = Vector3.ProjectOnPlane(velocity, normal);

            // Don't let slopes or wall corners fling you upward faster than you were going.
            if (slide.y > 0f && slide.y > velocity.y)
            {
                slide.y = Mathf.Max(0f, velocity.y);
            }

            return slide;
        }

        // ------------------------------------------------------------------ Helpers

        private Vector3 ControllerCenter => transform.TransformPoint(_controller.center);

        private Vector3 BottomSphereCenter =>
            ControllerCenter + Vector3.down * Mathf.Max(0f, _controller.height * 0.5f - _controller.radius);

        private void LaunchAirborne()
        {
            // Any launch (jump, knockback, jump pad) ends a slide. Its speed carries into the air.
            EndSlide();
            _forceAirborne = true;
            _grounded = false;
        }

        private Vector3 WishDirection(out float amount)
        {
            Vector2 raw = ReadMove();
            amount = Mathf.Clamp01(raw.magnitude);
            if (raw.sqrMagnitude > 1f)
            {
                raw.Normalize();
            }

            Vector3 world = transform.right * raw.x + transform.forward * raw.y;
            return world.sqrMagnitude > Epsilon ? world.normalized : Vector3.zero;
        }

        private Vector3 WishDirection()
        {
            return WishDirection(out float amount) * amount;
        }

        private static Vector3 Flatten(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }

        private bool IsSelf(Collider candidate)
        {
            return candidate != null && candidate.transform.IsChildOf(transform);
        }

        private Vector2 ReadMove()
        {
            if (!InputEnabled || _moveAction == null || _moveAction.action == null)
            {
                return Vector2.zero;
            }

            return _moveAction.action.ReadValue<Vector2>();
        }

        private bool Pressed(InputActionReference reference)
        {
            return InputEnabled && reference != null && reference.action != null
                && reference.action.WasPressedThisFrame();
        }

        private bool Held(InputActionReference reference)
        {
            return InputEnabled && reference != null && reference.action != null
                && reference.action.IsPressed();
        }

        private static void EnableAction(InputActionReference reference)
        {
            if (reference != null && reference.action != null)
            {
                reference.action.Enable();
            }
        }
    }
}

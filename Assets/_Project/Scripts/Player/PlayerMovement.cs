using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike.Player
{
    /// <summary>
    /// First-person CharacterController movement:
    /// walk with acceleration/friction, jump, double jump, wall jump, dash,
    /// dash-jump momentum carry, ground slam and slam jump.
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
        [SerializeField] private InputActionReference _slamAction;

        [Header("Collision")]
        [Tooltip("Level geometry: used for wall jumps and stepping down ledges. Do NOT include the Player layer.")]
        [SerializeField] private LayerMask _environmentMask;

        private readonly RaycastHit[] _groundHits = new RaycastHit[8];

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _walkableNormalY;

        private bool _grounded;
        private bool _landedPending;
        private bool _forceAirborne;

        private bool _collectingHits;
        private bool _hasWalkableContact;
        private bool _hasBlockingHit;
        private Vector3 _blockingNormal;
        private float _blockingOpposition;

        private bool _doubleJumpAvailable = true;
        private bool _airDashAvailable = true;
        private bool _wallJumpAvailable = true;

        private bool _dashing;
        private Vector3 _dashDirection;
        private float _dashTimeRemaining;
        private float _dashCooldownRemaining;

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
        public bool IsSlamming => _slamming;
        public float DashCooldownRemaining => _dashCooldownRemaining;
        public bool DoubleJumpAvailable => _doubleJumpAvailable;
        public bool AirDashAvailable => _airDashAvailable;
        public bool WallJumpAvailable => _wallJumpAvailable;

        /// <summary>Adds to current velocity (knockback, jump pads). Upward pushes leave the ground.</summary>
        public void AddVelocity(Vector3 delta)
        {
            _velocity += delta;
            if (delta.y > 0f)
            {
                LaunchAirborne();
            }
        }

        /// <summary>Replaces velocity entirely and cancels dash/slam (launchers, explosions).</summary>
        public void Launch(Vector3 velocity)
        {
            _dashing = false;
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
            _dashing = false;
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
            EnableAction(_slamAction);
        }

        private void OnDisable()
        {
            _dashing = false;
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

            if (Pressed(_jumpAction) || (_definition.AutoJumpWhileHeld && Held(_jumpAction) && _grounded))
            {
                TryJump();
            }

            if (Pressed(_dashAction))
            {
                TryDash();
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
            _slamJumpBufferRemaining = Mathf.Max(0f, _slamJumpBufferRemaining - dt);
            _slamPowerRemaining = Mathf.Max(0f, _slamPowerRemaining - dt);
        }

        private void Simulate(float dt)
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
                }

                if (_velocity.y < 0f)
                {
                    _velocity.y = 0f;
                }
            }

            if (_dashing)
            {
                TickDash(dt);
            }
            else if (_slamming)
            {
                TickSlam(dt);
            }
            else
            {
                TickWalkOrFall(dt);
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
                    _definition.GroundFriction, _definition.BrakingDeceleration);
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
                _definition.AirLateralFriction, 0f);
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;
            _velocity.y = Mathf.Max(_velocity.y - _definition.Gravity * dt, -_definition.TerminalFallSpeed);

            // Average of old and new velocity: jump arcs come out the same at any frame rate.
            Move(0.5f * (oldVelocity + _velocity) * dt);
        }

        private Vector3 CalcVelocity(Vector3 horizontal, Vector3 wish, float inputAmount, float dt,
            float friction, float brakingDeceleration)
        {
            float maxSpeed = _definition.WalkSpeed;
            float oldSpeed = horizontal.magnitude;
            bool hasInput = inputAmount > 0.001f && wish.sqrMagnitude > 0.001f;
            bool overMax = oldSpeed > maxSpeed + Epsilon;

            if (!hasInput || overMax)
            {
                Vector3 beforeBraking = horizontal;
                horizontal = ApplyBraking(horizontal, dt, friction, brakingDeceleration);

                // Braking from above walk speed never drops you below it while you hold forward.
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
                // Input can accelerate you up to walk speed, or steer you while you're faster.
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
            if (_slamming)
            {
                // Queue it: the slam turns into a slam jump if it lands soon.
                _slamJumpBufferRemaining = _definition.SlamJumpBufferTime;
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
                    _dashing = false;
                    _doubleJumpAvailable = false;
                    _airDashAvailable = true;
                    _velocity.y = _definition.JumpSpeed;
                    LaunchAirborne();
                    DoubleJumped?.Invoke(fromDash);
                }

                return;
            }

            if (_dashing)
            {
                _dashing = false;
                _velocity.y = _definition.JumpSpeed;
                LaunchAirborne();
                Jumped?.Invoke();
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
                _dashing = false;
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

            // Every dash, ground or air, uses up the air dash until you land or double/wall jump.
            _airDashAvailable = false;
            _dashCooldownRemaining = _definition.GroundDashCooldown;

            Vector3 input = WishDirection();
            _dashDirection = input.sqrMagnitude > 0.01f
                ? input.normalized
                : Flatten(transform.forward).normalized;

            _dashing = true;
            _slamming = false;
            _dashTimeRemaining = _definition.DashDuration;
            _velocity = _dashDirection * _definition.DashSpeed;
            Dashed?.Invoke(_dashDirection);
        }

        private void TickDash(float dt)
        {
            if (_dashTimeRemaining <= 0f)
            {
                // Ending normally drops you back to walk speed. Jumping out of the dash keeps full speed.
                _dashing = false;
                _velocity = _dashDirection * _definition.WalkSpeed;
                DashEnded?.Invoke();
                return;
            }

            // No gravity and no steering during a dash.
            _velocity = _dashDirection * _definition.DashSpeed;
            Move(_velocity * dt);
            _dashTimeRemaining -= dt;
        }

        // ------------------------------------------------------------------ Ground slam

        private void TryStartSlam()
        {
            if (!_definition.SlamEnabled || _grounded || _slamming)
            {
                return;
            }

            _dashing = false;
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
            if (!grounded && wasGrounded && !_forceAirborne && delta.y <= 0f && TrySnapDown())
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
                _hasWalkableContact = true;
            }
        }

        private bool TrySnapDown()
        {
            float radius = _controller.radius;
            float castRadius = Mathf.Max(0.01f, radius - _controller.skinWidth);
            Vector3 origin = BottomSphereCenter;
            float distance = _controller.stepOffset + _controller.skinWidth;

            int count = Physics.SphereCastNonAlloc(origin, castRadius, Vector3.down, _groundHits, distance,
                _environmentMask, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _groundHits[i];
                if (hit.distance <= 0f || IsSelf(hit.collider) || hit.normal.y < _walkableNormalY)
                {
                    continue;
                }

                best = Mathf.Min(best, hit.distance);
            }

            if (best == float.MaxValue)
            {
                return false;
            }

            _controller.Move(Vector3.down * best);
            return true;
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

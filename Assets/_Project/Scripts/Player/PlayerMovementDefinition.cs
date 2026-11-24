using UnityEngine;

namespace Roguelike.Player
{
    /// <summary>
    /// Tuning for <see cref="PlayerMovement"/>.
    /// All units are meters and seconds.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Player/Movement Definition", fileName = "PlayerMovementDefinition")]
    public sealed class PlayerMovementDefinition : ScriptableObject
    {
        [Header("Walking")]
        [Tooltip("Top speed from input alone. Dashes and jumps can carry you above this.")]
        [SerializeField, Min(0f)] private float _walkSpeed = 7.5f;
        [SerializeField, Min(0f)] private float _maxAcceleration = 80f;
        [Tooltip("How quickly velocity turns toward your input on the ground. Also drives braking.")]
        [SerializeField, Min(0f)] private float _groundFriction = 8f;
        [Tooltip("Constant slow-down on the ground with no input, or when faster than walk speed.")]
        [SerializeField, Min(0f)] private float _brakingDeceleration = 20.48f;
        [Tooltip("Ground friction is multiplied by this while braking.")]
        [SerializeField, Min(0f)] private float _brakingFrictionFactor = 2f;
        [Tooltip("Below this speed while braking, you stop dead.")]
        [SerializeField, Min(0f)] private float _brakeToStopSpeed = 0.1f;
        [Tooltip("Small downward push while walking so the controller stays on the floor.")]
        [SerializeField, Min(0f)] private float _groundStickSpeed = 0.5f;

        [Header("Air")]
        [Tooltip("1 = full control in the air, 0 = none.")]
        [SerializeField, Range(0f, 1f)] private float _airControl = 1f;
        [Tooltip("0 keeps all air momentum (needed for dash-jump speed carry).")]
        [SerializeField, Min(0f)] private float _airLateralFriction = 0f;
        [SerializeField, Min(0f)] private float _gravity = 15.68f;
        [SerializeField, Min(0f)] private float _terminalFallSpeed = 40f;
        [Tooltip("Landing faster than this counts as a hard landing (for effects).")]
        [SerializeField, Min(0f)] private float _hardLandingSpeed = 20f;

        [Header("Jump")]
        [SerializeField, Min(0f)] private float _jumpSpeed = 8f;
        [Tooltip("Holding Jump re-jumps as soon as you touch the ground.")]
        [SerializeField] private bool _autoJumpWhileHeld = true;
        [SerializeField] private bool _doubleJumpEnabled = true;

        [Header("Wall Jump")]
        [SerializeField] private bool _wallJumpEnabled = true;
        [Tooltip("How far from your center a wall can be and still count.")]
        [SerializeField, Min(0f)] private float _wallCheckDistance = 0.96f;
        [Tooltip("Push away from the wall, added to your current horizontal speed.")]
        [SerializeField, Min(0f)] private float _wallJumpPushSpeed = 9f;
        [Tooltip("Wall jump during a dash: launch speed in your input direction.")]
        [SerializeField, Min(0f)] private float _dashWallJumpSpeed = 22.5f;
        [SerializeField, Min(0f)] private float _dashWallJumpUpSpeed = 5.333f;

        [Header("Dash")]
        [SerializeField] private bool _dashEnabled = true;
        [SerializeField, Min(0f)] private float _dashDistance = 5f;
        [SerializeField, Min(0.01f)] private float _dashDuration = 0.2f;
        [Tooltip("Only applies to dashes started on the ground.")]
        [SerializeField, Min(0f)] private float _groundDashCooldown = 0.35f;
        [SerializeField] private bool _airDashEnabled = true;

        [Header("Ground Slam")]
        [SerializeField] private bool _slamEnabled = true;
        [SerializeField, Min(0f)] private float _slamSpeed = 35f;
        [Tooltip("Pressing Jump this long before a slam lands launches a slam jump on impact.")]
        [SerializeField, Min(0f)] private float _slamJumpBufferTime = 0.25f;
        [Tooltip("After a slam lands, a jump within this window becomes a slam jump.")]
        [SerializeField, Min(0f)] private float _slamPowerWindow = 0.15f;
        [Tooltip("Added to the slam jump's launch speed.")]
        [SerializeField, Min(0f)] private float _slamJumpBonusSpeed = 0.5f;
        [Tooltip("Slam power below this gives a normal jump instead.")]
        [SerializeField, Min(0f)] private float _slamJumpMinimumPower = 1f;

        public float WalkSpeed => _walkSpeed;
        public float MaxAcceleration => _maxAcceleration;
        public float GroundFriction => _groundFriction;
        public float BrakingDeceleration => _brakingDeceleration;
        public float BrakingFrictionFactor => _brakingFrictionFactor;
        public float BrakeToStopSpeed => _brakeToStopSpeed;
        public float GroundStickSpeed => _groundStickSpeed;

        public float AirControl => _airControl;
        public float AirLateralFriction => _airLateralFriction;
        public float Gravity => _gravity;
        public float TerminalFallSpeed => _terminalFallSpeed;
        public float HardLandingSpeed => _hardLandingSpeed;

        public float JumpSpeed => _jumpSpeed;
        public bool AutoJumpWhileHeld => _autoJumpWhileHeld;
        public bool DoubleJumpEnabled => _doubleJumpEnabled;

        public bool WallJumpEnabled => _wallJumpEnabled;
        public float WallCheckDistance => _wallCheckDistance;
        public float WallJumpPushSpeed => _wallJumpPushSpeed;
        public float DashWallJumpSpeed => _dashWallJumpSpeed;
        public float DashWallJumpUpSpeed => _dashWallJumpUpSpeed;

        public bool DashEnabled => _dashEnabled;
        public float DashDuration => _dashDuration;
        public float DashSpeed => _dashDistance / _dashDuration;
        public float GroundDashCooldown => _groundDashCooldown;
        public bool AirDashEnabled => _airDashEnabled;

        public bool SlamEnabled => _slamEnabled;
        public float SlamSpeed => _slamSpeed;
        public float SlamJumpBufferTime => _slamJumpBufferTime;
        public float SlamPowerWindow => _slamPowerWindow;
        public float SlamJumpBonusSpeed => _slamJumpBonusSpeed;
        public float SlamJumpMinimumPower => _slamJumpMinimumPower;
    }
}

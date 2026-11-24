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

        [Header("Slide")]
        [SerializeField] private bool _slideEnabled = true;
        [Tooltip("Starting a slide slower than this kicks you up to it (once per Kick Cooldown).")]
        [SerializeField, Min(0f)] private float _slideStartSpeed = 12f;
        [Tooltip("Seconds before another slide can give the start kick. Stops tap-sliding from beating walk speed.")]
        [SerializeField, Min(0f)] private float _slideKickCooldown = 1f;
        [Tooltip("Added on top of dash speed when you slide straight out of a dash.")]
        [SerializeField, Min(0f)] private float _slideDashBoostSpeed = 3f;
        [Tooltip("Flat speed lost per second while sliding.")]
        [SerializeField, Min(0f)] private float _slideFriction = 3f;
        [Tooltip("Extra speed lost per second for each m/s you're going. Fast slides bleed speed faster.")]
        [SerializeField, Min(0f)] private float _slideDrag = 0.35f;
        [Tooltip("How strongly slopes speed you up (downhill) or slow you down (uphill). 1 = normal gravity.")]
        [SerializeField, Min(0f)] private float _slideGravityScale = 2f;
        [Tooltip("How fast you can steer the slide with WASD, in degrees per second.")]
        [SerializeField, Min(0f)] private float _slideSteerDegreesPerSecond = 90f;
        [Tooltip("The slide ends when it slows below this.")]
        [SerializeField, Min(0f)] private float _slideEndSpeed = 6f;
        [Tooltip("Top slide speed, mostly reached going down steep ramps.")]
        [SerializeField, Min(0f)] private float _slideMaxSpeed = 30f;
        [Tooltip("Pressing Slide this close above the floor still starts a slide. Lets you slide straight out of a ground dash.")]
        [SerializeField, Min(0f)] private float _slideEntryGroundSnap = 0.08f;
        [Tooltip("Capsule height while sliding. Can't go below twice the CharacterController radius.")]
        [SerializeField, Min(0.1f)] private float _slideHeight = 1.05f;
        [Tooltip("0 = no time limit (the slide still ends when it slows down).")]
        [SerializeField, Min(0f)] private float _slideMaxDuration = 0f;
        [SerializeField, Min(0f)] private float _slideCooldown = 0f;
        [Tooltip("Holding Slide while you land starts a slide on touchdown.")]
        [SerializeField] private bool _slideOnLandWhileHeld = true;

        [Header("Ground Slam (unbound: Utility candidate)")]
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

        public bool SlideEnabled => _slideEnabled;
        public float SlideStartSpeed => _slideStartSpeed;
        public float SlideKickCooldown => _slideKickCooldown;
        public float SlideDashBoostSpeed => _slideDashBoostSpeed;
        public float SlideFriction => _slideFriction;
        public float SlideDrag => _slideDrag;
        public float SlideGravityScale => _slideGravityScale;
        public float SlideSteerDegreesPerSecond => _slideSteerDegreesPerSecond;
        public float SlideEndSpeed => _slideEndSpeed;
        public float SlideMaxSpeed => _slideMaxSpeed;
        public float SlideEntryGroundSnap => _slideEntryGroundSnap;
        public float SlideHeight => _slideHeight;
        public float SlideMaxDuration => _slideMaxDuration;
        public float SlideCooldown => _slideCooldown;
        public bool SlideOnLandWhileHeld => _slideOnLandWhileHeld;

        public bool SlamEnabled => _slamEnabled;
        public float SlamSpeed => _slamSpeed;
        public float SlamJumpBufferTime => _slamJumpBufferTime;
        public float SlamPowerWindow => _slamPowerWindow;
        public float SlamJumpBonusSpeed => _slamJumpBonusSpeed;
        public float SlamJumpMinimumPower => _slamJumpMinimumPower;
    }
}

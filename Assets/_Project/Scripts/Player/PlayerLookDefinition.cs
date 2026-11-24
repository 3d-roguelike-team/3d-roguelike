using UnityEngine;

namespace Roguelike.Player
{
    /// <summary>Tuning for <see cref="PlayerLook"/>.</summary>
    [CreateAssetMenu(menuName = "Roguelike/Player/Look Definition", fileName = "PlayerLookDefinition")]
    public sealed class PlayerLookDefinition : ScriptableObject
    {
        [Header("Mouse")]
        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [SerializeField, Min(0.001f)] private float _sensitivity = 0.219f;
        [SerializeField] private bool _invertY;
        [SerializeField, Range(-90f, 0f)] private float _minPitch = -89f;
        [SerializeField, Range(0f, 90f)] private float _maxPitch = 89f;

        [Header("Camera")]
        [Tooltip("Horizontal field of view. Converted to Unity's vertical FOV for the current screen shape.")]
        [SerializeField, Range(30f, 170f)] private float _horizontalFov = 119f;

        [Header("Slide")]
        [Tooltip("How far the camera drops while sliding (or crouched under something after a slide).")]
        [SerializeField, Min(0f)] private float _slideEyeDrop = 0.27f;
        [Tooltip("How fast the camera moves to and from the slide height, in meters per second.")]
        [SerializeField, Min(0f)] private float _eyeHeightSpeed = 2.5f;

        [Header("Strafe Lean")]
        [Tooltip("Camera roll in degrees while strafing.")]
        [SerializeField] private float _leanRoll = 1f;
        [Tooltip("Camera roll in degrees while strafing during a dash.")]
        [SerializeField] private float _dashLeanRoll = 3.75f;
        [Tooltip("How fast the roll catches up (higher = snappier).")]
        [SerializeField, Min(0f)] private float _leanResponse = 9f;
        [SerializeField, Min(0f)] private float _dashLeanResponse = 16f;

        public float Sensitivity => _sensitivity;
        public bool InvertY => _invertY;
        public float MinPitch => _minPitch;
        public float MaxPitch => _maxPitch;
        public float HorizontalFov => _horizontalFov;
        public float SlideEyeDrop => _slideEyeDrop;
        public float EyeHeightSpeed => _eyeHeightSpeed;
        public float LeanRoll => _leanRoll;
        public float DashLeanRoll => _dashLeanRoll;
        public float LeanResponse => _leanResponse;
        public float DashLeanResponse => _dashLeanResponse;
    }
}

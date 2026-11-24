using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike.Player
{
    /// <summary>
    /// Mouse look (yaw on the body, pitch on the camera), strafe camera lean and
    /// horizontal FOV. Runs before <see cref="PlayerMovement"/> so movement uses this frame's facing.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private PlayerLookDefinition _definition;

        [Header("Input")]
        [SerializeField] private InputActionReference _lookAction;
        [Tooltip("Same Move action as PlayerMovement. Used for the strafe lean.")]
        [SerializeField] private InputActionReference _moveAction;

        [Header("References")]
        [Tooltip("The first-person camera, a child of the player.")]
        [SerializeField] private Camera _camera;
        [Tooltip("Optional. Lowers the camera while sliding and strengthens the lean while dashing.")]
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private bool _lockCursor = true;

        private float _pitch;
        private float _roll;
        private float _standingEyeHeight;
        private float _lastAspect = -1f;

        /// <summary>Turn off to freeze the view (menus, shop UI).</summary>
        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            if (_definition == null || _camera == null)
            {
                Debug.LogError($"{nameof(PlayerLook)} on '{name}' needs a {nameof(PlayerLookDefinition)} and a Camera assigned.", this);
                enabled = false;
                return;
            }

            float x = _camera.transform.localEulerAngles.x;
            _pitch = x > 180f ? x - 360f : x;
            _standingEyeHeight = _camera.transform.localPosition.y;
        }

        private void OnEnable()
        {
            EnableAction(_lookAction);
            EnableAction(_moveAction);

            if (_lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (_lockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            UpdateFov();

            if (InputEnabled && _lookAction != null && _lookAction.action != null)
            {
                // Mouse delta is already per-frame, so it isn't multiplied by deltaTime.
                Vector2 delta = _lookAction.action.ReadValue<Vector2>() * _definition.Sensitivity;
                if (_definition.InvertY)
                {
                    delta.y = -delta.y;
                }

                transform.Rotate(0f, delta.x, 0f, Space.Self);
                _pitch = Mathf.Clamp(_pitch - delta.y, _definition.MinPitch, _definition.MaxPitch);
            }

            UpdateLean(Time.deltaTime);
            UpdateEyeHeight(Time.deltaTime);
            _camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, _roll);
        }

        private void UpdateEyeHeight(float dt)
        {
            bool lowered = _movement != null && _movement.IsLowered;
            float target = lowered ? _standingEyeHeight - _definition.SlideEyeDrop : _standingEyeHeight;

            Vector3 position = _camera.transform.localPosition;
            position.y = Mathf.MoveTowards(position.y, target, _definition.EyeHeightSpeed * dt);
            _camera.transform.localPosition = position;
        }

        private void UpdateLean(float dt)
        {
            float strafe = InputEnabled && _moveAction != null && _moveAction.action != null
                ? _moveAction.action.ReadValue<Vector2>().x
                : 0f;

            // Only lean at full strafe, so diagonal stick drift doesn't wobble the camera.
            float leanDirection = strafe >= 0.99f ? 1f : strafe <= -0.99f ? -1f : 0f;

            bool dashing = _movement != null && _movement.IsDashing;
            float targetRoll = -leanDirection * (dashing ? _definition.DashLeanRoll : _definition.LeanRoll);
            float response = dashing ? _definition.DashLeanResponse : _definition.LeanResponse;

            // Exponential smoothing so the lean feels the same at any frame rate.
            _roll = response <= 0f || dt <= 0f
                ? targetRoll
                : Mathf.Lerp(_roll, targetRoll, 1f - Mathf.Exp(-response * dt));
        }

        private void UpdateFov()
        {
            float aspect = _camera.aspect;
            if (Mathf.Approximately(aspect, _lastAspect))
            {
                return;
            }

            _lastAspect = aspect;
            float halfHorizontal = _definition.HorizontalFov * Mathf.Deg2Rad * 0.5f;
            _camera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(halfHorizontal) / Mathf.Max(0.01f, aspect)) * Mathf.Rad2Deg;
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

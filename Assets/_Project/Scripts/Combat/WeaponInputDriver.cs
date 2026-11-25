using Roguelike.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike.Combat
{
    /// <summary>
    /// Connects Input System actions to a weapon without coupling the weapon to player input.
    /// </summary>
    public class WeaponInputDriver : MonoBehaviour
    {
        [Header("Target Weapon")]
        // Optional explicit weapon reference. The component on this GameObject is used as a fallback.
        [SerializeField] private MonoBehaviour _weaponComponent;

        [Header("Input Actions")]
        // Actions used to fire and reload the configured weapon.
        [SerializeField] private InputActionReference _fireAction;
        [SerializeField] private InputActionReference _reloadAction;

        [Header("Firing Mode")]
        // Automatic weapons fire while the fire action is held; semi-automatic weapons fire on press.
        [SerializeField] private bool _automatic = true;

        private IWeapon _weapon;

        private void Awake()
        {
            // Resolve the weapon through the assigned component before checking this GameObject.
            _weapon = _weaponComponent as IWeapon;
            if (_weapon == null)
            {
                _weapon = GetComponent<IWeapon>();
            }
        }

        private void OnEnable()
        {
            // Enable the actions and subscribe only to events needed by the selected firing mode.
            if (_fireAction != null && _fireAction.action != null)
            {
                _fireAction.action.Enable();
                if (!_automatic)
                {
                    _fireAction.action.performed += OnFirePerformed;
                }
            }

            if (_reloadAction != null && _reloadAction.action != null)
            {
                _reloadAction.action.Enable();
                _reloadAction.action.performed += OnReloadPerformed;
            }
        }

        private void OnDisable()
        {
            // Remove event handlers so disabled drivers do not continue receiving input callbacks.
            if (_fireAction != null && _fireAction.action != null)
            {
                if (!_automatic)
                {
                    _fireAction.action.performed -= OnFirePerformed;
                }
            }

            if (_reloadAction != null && _reloadAction.action != null)
            {
                _reloadAction.action.performed -= OnReloadPerformed;
            }
        }

        private void Update()
        {
            // Automatic fire is polled each frame while the input action remains held.
            if (_weapon == null)
            {
                return;
            }

            if (_automatic && _fireAction != null && _fireAction.action != null && _fireAction.action.IsPressed())
            {
                _weapon.TryFire();
            }
        }

        private void OnFirePerformed(InputAction.CallbackContext context)
        {
            // Semi-automatic fire is triggered once for each performed input event.
            _weapon?.TryFire();
        }

        private void OnReloadPerformed(InputAction.CallbackContext context)
        {
            // Reload requests are delegated to the weapon so it can enforce its own rules.
            _weapon?.Reload();
        }
    }
}

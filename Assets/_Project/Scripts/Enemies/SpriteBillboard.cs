using UnityEngine;

namespace Roguelike.Enemies
{
    public class SpriteBillboard : MonoBehaviour
    {
        [Tooltip("If true, locks pitch/roll so the sprite only rotates around the Y axis.")]
        [SerializeField] private bool _lockVerticalAxis = true;

        private Camera _cachedCamera;

        private void LateUpdate()
        {
            if (_cachedCamera == null)
            {
                _cachedCamera = Camera.main;
                if (_cachedCamera == null)
                {
                    return;
                }
            }

            Vector3 directionToCamera = _cachedCamera.transform.position - transform.position;
            if (_lockVerticalAxis)
            {
                directionToCamera.y = 0f;
            }

            if (directionToCamera.sqrMagnitude > 0.0001f)
            {
                // Quad/SpriteRenderer faces +Z away or -Z toward camera depending on setup;
                // LookRotation(-directionToCamera) points the sprite's front (-Z) straight at the camera.
                transform.rotation = Quaternion.LookRotation(-directionToCamera.normalized, Vector3.up);
            }
        }
    }
}

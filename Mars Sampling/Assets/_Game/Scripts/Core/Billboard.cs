using UnityEngine;

namespace MarsSampling
{
    /// <summary>Keeps a world label (site flag numbers) facing the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        Transform _cam;

        void LateUpdate()
        {
            if (_cam == null)
            {
                if (Camera.main == null) return;
                _cam = Camera.main.transform;
            }
            // Face the camera, yaw only, so text doesn't tilt.
            Vector3 dir = transform.position - _cam.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}

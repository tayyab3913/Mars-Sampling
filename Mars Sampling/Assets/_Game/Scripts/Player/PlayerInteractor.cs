using UnityEngine;

namespace MarsSampling
{
    /// <summary>
    /// Turns interact taps into world raycasts. If the tap hits an IInteractable
    /// within reach, MissionManager decides what it means for the current phase.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Wired by the scene builder")]
        public Camera cam;

        [Tooltip("Filled from MissionConfig at startup.")]
        public float interactRange = 3.5f;

        public void TapAt(Vector2 screenPos)
        {
            var mission = MissionManager.Instance;
            if (mission == null || cam == null) return;
            if (mission.ModalOpen) return; // a panel is up; taps belong to UI

            Ray ray = cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                return;

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null) return;

            if (Vector3.Distance(transform.position, hit.point) > interactRange)
            {
                mission.Hud.ShowHint("Move closer.");
                return;
            }

            mission.OnInteract(interactable as MonoBehaviour);
        }
    }
}

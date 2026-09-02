using UnityEngine;

namespace MarsSampling
{
    /// <summary>
    /// A tappable prop at base camp. What tapping it does depends on the
    /// current mission phase (see MissionManager.OnInteract).
    /// </summary>
    public class StationProp : MonoBehaviour, IInteractable
    {
        public enum Kind
        {
            BagBox,    // confirm the numbered sample bags   (checklist step 1)
            LayoutMat, // lay all 11 samples out on the tarp (checklist step 4)
            Vehicle    // pack the crate / load the rover    (checklist step 5)
        }

        public Kind kind;
    }
}

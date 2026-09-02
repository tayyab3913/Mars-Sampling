using UnityEngine;

namespace MarsSampling
{
    /// <summary>
    /// A collectible rock in the field. Placed by the scene builder in a cluster
    /// around each sample site. Tapping it (in range, at the active, logged site)
    /// opens the scanner UI.
    /// </summary>
    public class RockSample : MonoBehaviour, IInteractable
    {
        [Tooltip("Which rock type this specimen is (drives the scan readout + verdict).")]
        public RockTypeDef rockType;

        [Tooltip("Too big for a standard sample bag -> triggers the bag-fit judgement case.")]
        public bool oversized;

        [Tooltip("Index of the sample site this rock belongs to (1-based).")]
        public int siteIndex;
    }
}

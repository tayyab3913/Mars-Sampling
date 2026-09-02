using UnityEngine;

namespace MarsSampling
{
    /// <summary>
    /// One sample site: a flagged spot with a rock cluster and a trigger volume.
    /// Walking into the trigger while this is the next site starts the satellite
    /// locator dialogue with Good Luck ("log and verify" checklist step).
    ///
    /// TO ADD A SITE: place this on an object with a SphereCollider (isTrigger)
    /// and a kinematic Rigidbody, set the index and locator dialogue, and scatter
    /// RockSample rocks with a matching siteIndex around it.
    /// </summary>
    public class SampleSite : MonoBehaviour
    {
        [Tooltip("1-based site number. Sites must be visited in order.")]
        public int index;

        [Tooltip("The satellite locator exchange played when this site is reached.")]
        public DialogueSequence logDialogue;

        /// <summary>Set true once the locator dialogue has verified this site.</summary>
        public bool Logged { get; set; }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (MissionManager.Instance != null)
                MissionManager.Instance.OnEnterSite(this);
        }
    }
}

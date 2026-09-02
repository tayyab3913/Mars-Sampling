using UnityEngine;

namespace MarsSampling
{
    /// <summary>
    /// Tunable rules for the sampling mission. One asset lives in _Game/Data.
    /// Change numbers here (spacing, counts, ranges) without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "Mars Sampling/Mission Config", fileName = "MissionConfig")]
    public class MissionConfig : ScriptableObject
    {
        [Header("Sampling rules")]
        [Tooltip("Minimum allowed distance between consecutive sample sites, metres.")]
        public float minSpacingMeters = 50f;

        [Tooltip("Number of numbered sample sites (a duplicate is taken at the last one).")]
        public int siteCount = 10;

        [Header("Interaction")]
        [Tooltip("How close the player must be to tap-interact with something, metres.")]
        public float interactRange = 3.5f;
    }
}

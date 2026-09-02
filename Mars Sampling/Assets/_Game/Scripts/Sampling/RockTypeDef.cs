using UnityEngine;

namespace MarsSampling
{
    /// <summary>
    /// Data definition for one rock type. The "hidden properties" (composition, grain)
    /// are only revealed to the player through the XRF scanner UI.
    ///
    /// TO ADD A NEW ROCK TYPE: create one of these via
    /// Assets > Create > Mars Sampling > Rock Type, fill in the fields, assign a
    /// material, and place rocks that reference it (see RockSample). No code changes needed.
    /// </summary>
    [CreateAssetMenu(menuName = "Mars Sampling/Rock Type", fileName = "RockType")]
    public class RockTypeDef : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Rock";

        [Tooltip("What the player sees when holding the rock, BEFORE scanning.")]
        [TextArea] public string fieldDescription;

        [Header("Hidden properties (revealed by the XRF scan)")]
        public float silicaPct = 50f;     // SiO2 weight %
        public float ironPct = 15f;       // FeO weight %
        public float magnesiumPct = 8f;   // MgO weight %
        [Tooltip("Grain / texture line shown in the scan readout.")]
        public string grainNote = "fine-grained";

        [Header("Scanner judgement")]
        [Tooltip("True = this is a scientifically 'correct' representative pick. " +
                 "False = outlier (e.g. the shiny novelty-bias bait).")]
        public bool isRepresentative = true;

        [Tooltip("Scanner explanation shown when this type IS the right choice.")]
        [TextArea] public string correctText;

        [Tooltip("Scanner explanation shown when this type is NOT the right choice.")]
        [TextArea] public string incorrectText;

        [Header("Visuals")]
        public Material material;

        [Tooltip("Marks the novelty-bias bait (used for end-of-level reporting).")]
        public bool shiny;
    }
}

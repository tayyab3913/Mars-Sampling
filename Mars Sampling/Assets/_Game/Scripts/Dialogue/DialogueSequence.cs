using System;
using UnityEngine;

namespace MarsSampling
{
    /// <summary>One spoken line.</summary>
    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea(2, 5)] public string text;
    }

    /// <summary>
    /// A scripted dialogue: main lines, then (optionally) a two-way player choice,
    /// then the branch lines for whichever option was picked.
    ///
    /// TO ADD DIALOGUE: create via Assets > Create > Mars Sampling > Dialogue Sequence,
    /// fill in lines (speaker + text). For a choice (like the 5,000 km locator catch),
    /// tick hasChoice, write both option labels and both branches. The code that starts
    /// the sequence receives which option was picked (true = option A).
    /// </summary>
    [CreateAssetMenu(menuName = "Mars Sampling/Dialogue Sequence", fileName = "Dialogue")]
    public class DialogueSequence : ScriptableObject
    {
        [Tooltip("Lines played in order before any choice.")]
        public DialogueLine[] lines;

        [Header("Optional player choice (after the main lines)")]
        public bool hasChoice;
        public string choiceAText;
        public string choiceBText;
        [Tooltip("Lines played if the player picks option A.")]
        public DialogueLine[] branchA;
        [Tooltip("Lines played if the player picks option B.")]
        public DialogueLine[] branchB;
    }
}

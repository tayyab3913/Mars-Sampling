using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsSampling
{
    /// <summary>
    /// Plays a DialogueSequence into the DialogueUI, line by line, handling the
    /// optional two-way choice. Player input is locked while a dialogue runs.
    /// The completion callback receives true if the player picked option A
    /// (used e.g. to detect that the 5,000 km locator error was caught).
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        [Header("Wired by the scene builder")]
        public DialogueUI ui;

        readonly Queue<DialogueLine> _queue = new Queue<DialogueLine>();
        DialogueSequence _seq;
        Action<bool> _onDone;
        bool _choseA;
        bool _choiceShown;

        public bool IsPlaying { get; private set; }

        public void Play(DialogueSequence seq, Action<bool> onDone = null)
        {
            if (seq == null) { onDone?.Invoke(false); return; }
            if (IsPlaying)
            {
                // Mission logic never overlaps dialogues; guard just in case.
                Debug.LogWarning("DialogueRunner: Play called while already playing; ignoring.");
                return;
            }

            IsPlaying = true;
            _seq = seq;
            _onDone = onDone;
            _choseA = false;
            _choiceShown = false;
            _queue.Clear();
            foreach (var line in seq.lines) _queue.Enqueue(line);

            MissionManager.Instance.PushModal();
            ShowNext();
        }

        /// <summary>Hooked to the dialogue panel's Next button.</summary>
        public void OnNextClicked() => ShowNext();

        /// <summary>Hooked to the two choice buttons (a = option A).</summary>
        public void OnChoiceClicked(bool a)
        {
            _choseA = a;
            var branch = a ? _seq.branchA : _seq.branchB;
            if (branch != null) foreach (var line in branch) _queue.Enqueue(line);
            ShowNext();
        }

        void ShowNext()
        {
            if (_queue.Count == 0)
            {
                // Out of lines: offer the choice once (if any), otherwise finish.
                if (_seq.hasChoice && !_choiceShown)
                {
                    _choiceShown = true;
                    ui.ShowChoices(_seq.choiceAText, _seq.choiceBText);
                    return;
                }
                Finish();
                return;
            }

            var line = _queue.Dequeue();
            ui.ShowLine(line.speaker, line.text);
        }

        void Finish()
        {
            IsPlaying = false;
            ui.Hide();
            MissionManager.Instance.PopModal();
            var cb = _onDone;
            _onDone = null;
            cb?.Invoke(_choseA);
        }
    }
}

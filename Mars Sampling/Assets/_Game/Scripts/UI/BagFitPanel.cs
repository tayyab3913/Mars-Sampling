using System;
using UnityEngine;
using UnityEngine.UI;

namespace MarsSampling
{
    /// <summary>
    /// The bag-fit judgement case: the chosen rock does not fit a standard
    /// sample bag. The player decides - use the single oversize spare bag, or
    /// break the rock into hand-sized pieces. Both choices proceed (no fail
    /// state); the decision is recorded and reflected on the end screen.
    /// </summary>
    public class BagFitPanel : MonoBehaviour
    {
        [Header("Wired by the scene builder")]
        public GameObject root;
        public Text bodyText;
        public Button largerBagButton;
        public Button breakButton;

        Action<bool> _onChoice; // true = larger bag, false = break it

        public void Open(Action<bool> onChoice)
        {
            _onChoice = onChoice;
            bodyText.text =
                "The rock will not fit a standard sample bag.\n\n" +
                "You carry ONE oversize spare bag. Or there is the hammer -\n" +
                "breaking it loses some context, but a fragment still assays fine.";
            root.SetActive(true);
            MissionManager.Instance.PushModal();
        }

        public void OnLargerBagClicked() => Resolve(true);
        public void OnBreakClicked() => Resolve(false);

        void Resolve(bool largerBag)
        {
            root.SetActive(false);
            MissionManager.Instance.PopModal();
            var cb = _onChoice;
            _onChoice = null;
            cb?.Invoke(largerBag);
        }
    }
}

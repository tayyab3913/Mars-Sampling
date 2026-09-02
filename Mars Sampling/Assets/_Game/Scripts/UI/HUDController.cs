using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MarsSampling
{
    /// <summary>
    /// Always-on HUD: the current objective line (top), transient hint messages
    /// (bottom), and the end-of-mission screen.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Wired by the scene builder")]
        public Text objectiveText;
        public Text hintText;
        public GameObject endRoot;
        public Text endTitle;
        public Text endBody;

        Coroutine _hintRoutine;

        public void SetObjective(string text)
        {
            if (objectiveText != null) objectiveText.text = text;
        }

        /// <summary>Show a short-lived message ("Move closer.", "Sample bagged.").</summary>
        public void ShowHint(string text, float seconds = 3f)
        {
            if (hintText == null) return;
            if (_hintRoutine != null) StopCoroutine(_hintRoutine);
            hintText.text = text;
            hintText.gameObject.SetActive(true);
            _hintRoutine = StartCoroutine(HideHintAfter(seconds));
        }

        IEnumerator HideHintAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            hintText.gameObject.SetActive(false);
            _hintRoutine = null;
        }

        public void ShowEndScreen(string title, string body)
        {
            endTitle.text = title;
            endBody.text = body;
            endRoot.SetActive(true);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace MarsSampling
{
    /// <summary>
    /// The dialogue panel at the bottom of the screen: speaker name, line text,
    /// a Next button, and two choice buttons (shown only for choices).
    /// Driven entirely by DialogueRunner.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [Header("Wired by the scene builder")]
        public GameObject root;
        public Text speakerText;
        public Text bodyText;
        public Button nextButton;
        public Button choiceAButton;
        public Button choiceBButton;
        public Text choiceALabel;
        public Text choiceBLabel;

        public void ShowLine(string speaker, string text)
        {
            root.SetActive(true);
            speakerText.text = speaker;
            bodyText.text = text;
            nextButton.gameObject.SetActive(true);
            choiceAButton.gameObject.SetActive(false);
            choiceBButton.gameObject.SetActive(false);
        }

        public void ShowChoices(string a, string b)
        {
            root.SetActive(true);
            nextButton.gameObject.SetActive(false);
            choiceAButton.gameObject.SetActive(true);
            choiceBButton.gameObject.SetActive(true);
            choiceALabel.text = a;
            choiceBLabel.text = b;
        }

        public void Hide() => root.SetActive(false);
    }
}

using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MarsSampling
{
    /// <summary>
    /// The lab scanner (portable XRF) panel. Flow:
    ///   pick up rock -> field description -> RUN XRF SCAN -> readout + verdict
    ///   -> BAG &amp; LABEL (or PUT BACK).
    /// The verdict is informational only: there is no fail state. Bagging an
    /// oversized rock is intercepted by MissionManager and routed to the
    /// bag-fit choice instead.
    ///
    /// TO ADD A SCANNER CASE: the readout and verdict text all come from
    /// RockTypeDef fields, so new cases are configured on rock type assets.
    /// </summary>
    public class ScannerUI : MonoBehaviour
    {
        [Header("Wired by the scene builder")]
        public GameObject root;
        public Text titleText;
        public Text bodyText;
        public Button scanButton;
        public Button bagButton;
        public Button putBackButton;
        public Text bagButtonLabel;

        RockSample _rock;
        bool _scanned;
        bool _duplicateMode;

        public bool IsOpen => root.activeSelf;
        public RockSample CurrentRock => _rock;
        public bool ScannedVerdictCorrect => _rock != null && _rock.rockType.isRepresentative;

        /// <summary>Open for a normal numbered sample (scan required before bagging).</summary>
        public void Open(RockSample rock, string nextLabel)
        {
            _rock = rock;
            _scanned = false;
            _duplicateMode = false;

            titleText.text = "FIELD SPECIMEN";
            bodyText.text = rock.rockType.fieldDescription
                + (rock.oversized ? "\n\nIt is... a big one. Noticeably bigger than the sample bags." : "");
            bagButtonLabel.text = $"BAG + LABEL {nextLabel}";

            scanButton.gameObject.SetActive(true);
            bagButton.gameObject.SetActive(false); // must scan first
            root.SetActive(true);
            MissionManager.Instance.PushModal();
        }

        /// <summary>
        /// Open for the duplicate at site 10: per the agreed scope this is a
        /// bagging and labelling step only, no separate scan/judgement.
        /// </summary>
        public void OpenDuplicate(RockSample rock)
        {
            _rock = rock;
            _scanned = true; // no scan step for the duplicate
            _duplicateMode = true;

            titleText.text = "DUPLICATE SAMPLE - SITE 10";
            bodyText.text = "Protocol: one duplicate at the tenth site. Same spot, second bag.\n" +
                            "No scan needed - bag and label it MS-10-B.";
            bagButtonLabel.text = "BAG + LABEL MS-10-B";

            scanButton.gameObject.SetActive(false);
            bagButton.gameObject.SetActive(true);
            root.SetActive(true);
            MissionManager.Instance.PushModal();
        }

        /// <summary>Hooked to the RUN XRF SCAN button.</summary>
        public void OnScanClicked()
        {
            if (_rock == null || _scanned) return;
            _scanned = true;

            var t = _rock.rockType;
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{t.displayName}</b>");
            sb.AppendLine("XRF SURFACE ASSAY (reads ~1 mm deep - dust rubbed off first)");
            sb.AppendLine($"SiO2 {t.silicaPct:0.0}%   FeO {t.ironPct:0.0}%   MgO {t.magnesiumPct:0.0}%");
            sb.AppendLine($"Texture: {t.grainNote}");
            sb.AppendLine("--------------------------------");
            if (t.isRepresentative)
            {
                sb.AppendLine("<color=#8BE28B><b>VERDICT: REPRESENTATIVE PICK [OK]</b></color>");
                sb.AppendLine(t.correctText);
            }
            else
            {
                sb.AppendLine("<color=#E2A08B><b>VERDICT: OUTLIER - NOT REPRESENTATIVE</b></color>");
                sb.AppendLine(t.incorrectText);
            }
            bodyText.text = sb.ToString();

            scanButton.gameObject.SetActive(false);
            bagButton.gameObject.SetActive(true);

            MissionManager.Instance.NotifyScanned(_rock);
        }

        /// <summary>Hooked to the BAG + LABEL button.</summary>
        public void OnBagClicked()
        {
            if (_rock == null || !_scanned) return;
            var rock = _rock;
            bool dup = _duplicateMode;
            Close();
            MissionManager.Instance.RequestBagging(rock, dup);
        }

        /// <summary>Hooked to the PUT BACK button.</summary>
        public void OnPutBackClicked() => Close();

        void Close()
        {
            _rock = null;
            root.SetActive(false);
            MissionManager.Instance.PopModal();
        }
    }
}

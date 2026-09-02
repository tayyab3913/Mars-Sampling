using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MarsSampling
{
    /// <summary>
    /// The in-fiction field tablet: renders the live checklist and the site log
    /// from MissionManager state, and hosts the final "send shipment confirmation"
    /// button. Opened/closed with the TABLET button; opening pauses movement.
    /// </summary>
    public class TabletUI : MonoBehaviour
    {
        [Header("Wired by the scene builder")]
        public GameObject root;
        public Text bodyText;
        public Button sendButton;

        bool _open;

        public void Toggle()
        {
            if (_open) Close(); else Open();
        }

        public void Open()
        {
            if (_open) return;
            _open = true;
            Refresh();
            root.SetActive(true);
            MissionManager.Instance.PushModal();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            root.SetActive(false);
            MissionManager.Instance.PopModal();
        }

        /// <summary>Rebuild the checklist text from mission state.</summary>
        public void Refresh()
        {
            var m = MissionManager.Instance;
            if (m == null || bodyText == null) return;

            var sb = new StringBuilder();
            int bagged = m.NumberedSampleCount;
            bool dupDone = m.DuplicateBagged;

            sb.AppendLine(Line(m.Phase > MissionPhase.ConfirmBags,
                "1. Confirm numbered sample bags (MS-01..10, 10-B, oversize spare)"));

            string sampling = $"2. Collect samples: {bagged}/{m.Config.siteCount}";
            if (m.Phase == MissionPhase.Sampling && bagged < m.Config.siteCount)
                sampling += $"   >> next: Site {m.NextSiteIndex}";
            sb.AppendLine(Line(bagged >= m.Config.siteCount, sampling));
            sb.AppendLine($"     min spacing {m.Config.minSpacingMeters:0} m, verified by satellite log");

            sb.AppendLine(Line(dupDone, "3. Duplicate sample at Site 10 (bag MS-10-B)"));
            sb.AppendLine(Line(m.Phase > MissionPhase.LayoutCheck, "4. Lay all 11 samples out, verify none missing"));
            sb.AppendLine(Line(m.Phase > MissionPhase.PackAndLoad, "5. Bag + label for shipment, load the vehicle"));
            sb.AppendLine(Line(m.Phase > MissionPhase.SendConfirmation, "6. Send shipment confirmation"));

            if (m.SiteLog.Count > 0)
            {
                sb.AppendLine("\n<b>SATELLITE SITE LOG</b>");
                foreach (var entry in m.SiteLog) sb.AppendLine(entry);
            }

            bodyText.text = sb.ToString();
            sendButton.gameObject.SetActive(m.Phase == MissionPhase.SendConfirmation);
        }

        static string Line(bool done, string text) =>
            (done ? "<color=#8BE28B>[OK]</color> " : "[    ] ") + text;
    }
}

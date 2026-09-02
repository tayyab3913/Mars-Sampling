using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarsSampling
{
    /// <summary>Top-level checklist phases, in order. The tablet renders these.</summary>
    public enum MissionPhase
    {
        ConfirmBags,      // 1. confirm numbered sample bags at the rover
        Sampling,         // 2+3. log sites via locator dialogue, collect 10 + duplicate
        LayoutCheck,      // 4. lay all 11 out on the tarp, verify none missing
        PackAndLoad,      // 5. pack the crate, load the vehicle
        SendConfirmation, // 6. send shipment confirmation from the tablet
        Complete
    }

    /// <summary>
    /// The mission "brain": owns the checklist state machine, the sample records,
    /// the spacing/site-order rules and the two scripted judgement cases. All
    /// player taps and site triggers are routed here, and it drives every panel.
    ///
    /// TO ADD A CHECKLIST STEP: add a phase to MissionPhase, handle it in
    /// OnInteract/advance logic below, and add a display line in TabletUI.Refresh.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [Header("Config + world (wired by the scene builder)")]
        public MissionConfig config;
        public PlayerController player;
        public Transform camp;              // distance reference for Site 1
        public SampleSite[] sites;          // index 0 = Site 1
        public Transform layoutRoot;        // the tarp; layout bags spawn here
        public Material bagMaterial;        // for the spawned layout bag props

        [Header("UI (wired by the scene builder)")]
        public HUDController hud;
        public TabletUI tablet;
        public ScannerUI scanner;
        public BagFitPanel bagFitPanel;
        public DialogueRunner dialogue;

        [Header("Dialogue assets (wired by the scene builder)")]
        public DialogueSequence introDialogue;
        public DialogueSequence bagCheckDialogue;
        public DialogueSequence bagFitLargerDialogue;
        public DialogueSequence bagFitBreakDialogue;
        public DialogueSequence layoutDialogue;
        public DialogueSequence loadDialogue;
        public DialogueSequence outroDialogue;

        // ---- state -------------------------------------------------------------

        public MissionPhase Phase { get; private set; } = MissionPhase.ConfirmBags;
        public MissionConfig Config => config;
        public HUDController Hud => hud;

        /// <summary>1-based index of the next site to log/sample.</summary>
        public int NextSiteIndex { get; private set; } = 1;

        public List<SampleRecord> Records { get; } = new List<SampleRecord>();
        public List<string> SiteLog { get; } = new List<string>();

        public int NumberedSampleCount
        {
            get { int n = 0; foreach (var r in Records) if (!r.isDuplicate) n++; return n; }
        }

        public bool DuplicateBagged
        {
            get { foreach (var r in Records) if (r.isDuplicate) return true; return false; }
        }

        bool _awaitingDuplicate;               // sample 10 bagged, MS-10-B pending
        readonly List<GameObject> _layoutBags = new List<GameObject>();

        // Judgement-case reporting for the end screen.
        string _bagFitResolution = "";
        bool _sawShinyInScanner;
        bool _baggedShiny;
        bool _caughtLocatorError;

        int _modalCount;
        public bool ModalOpen => _modalCount > 0;

        // ---- lifecycle ---------------------------------------------------------

        void Awake() => Instance = this;

        void Start()
        {
            hud.SetObjective("Meet Good Luck at the rover.");
            tablet.Refresh();
            StartCoroutine(PlayIntro());
        }

        IEnumerator PlayIntro()
        {
            yield return new WaitForSeconds(0.8f);
            dialogue.Play(introDialogue, _ =>
                hud.SetObjective("Tap the BAG BOX by the rover to confirm the numbered bags."));
        }

        /// <summary>Panels/dialogues call these so movement pauses while UI is up.</summary>
        public void PushModal() { _modalCount++; player.InputLocked = _modalCount > 0; }
        public void PopModal() { _modalCount = Mathf.Max(0, _modalCount - 1); player.InputLocked = _modalCount > 0; }

        // ---- interaction routing ----------------------------------------------

        /// <summary>Called by PlayerInteractor for every valid in-range tap.</summary>
        public void OnInteract(MonoBehaviour target)
        {
            if (target is RockSample rock) { HandleRockTap(rock); return; }
            if (target is StationProp prop) { HandleStationTap(prop); return; }
        }

        void HandleStationTap(StationProp prop)
        {
            switch (prop.kind)
            {
                case StationProp.Kind.BagBox:
                    if (Phase != MissionPhase.ConfirmBags)
                    {
                        hud.ShowHint("Bags are already confirmed and counted.");
                        return;
                    }
                    dialogue.Play(bagCheckDialogue, _ =>
                    {
                        Phase = MissionPhase.Sampling;
                        hud.SetObjective("Head to the Site 1 flag and log it with Good Luck.");
                        tablet.Refresh();
                    });
                    break;

                case StationProp.Kind.LayoutMat:
                    if (Phase != MissionPhase.LayoutCheck)
                    {
                        hud.ShowHint(Phase < MissionPhase.LayoutCheck
                            ? "Nothing to lay out yet - samples first."
                            : "Layout check already done.");
                        return;
                    }
                    DoLayoutCheck();
                    break;

                case StationProp.Kind.Vehicle:
                    if (Phase != MissionPhase.PackAndLoad)
                    {
                        hud.ShowHint(Phase < MissionPhase.PackAndLoad
                            ? "Nothing to load yet."
                            : "The crate is already loaded.");
                        return;
                    }
                    DoPackAndLoad();
                    break;
            }
        }

        // ---- site logging (satellite locator dialogue) -------------------------

        /// <summary>Called by SampleSite triggers when the player walks in.</summary>
        public void OnEnterSite(SampleSite site)
        {
            if (Phase != MissionPhase.Sampling || dialogue.IsPlaying) return;
            if (site.Logged) return;

            if (site.index != NextSiteIndex)
            {
                hud.ShowHint($"This is Site {site.index}'s area - the tablet wants Site {NextSiteIndex} next.");
                return;
            }

            dialogue.Play(site.logDialogue, choseA =>
            {
                site.Logged = true;
                float dist = DistanceFromPrevious(site.index);
                string note = "";
                if (site.index == 4)
                {
                    // The scripted locator error: option A = the player caught it.
                    _caughtLocatorError = choseA;
                    note = choseA ? "  (downlink error caught by you)" : "  (downlink error, self-corrected)";
                }
                SiteLog.Add($"S{site.index:00}  {dist:0} m from {(site.index == 1 ? "camp" : $"S{site.index - 1:00}")}" +
                            $"  [OK] >= {config.minSpacingMeters:0} m{note}");
                hud.SetObjective($"Site {site.index} logged. Tap a rock near the flag to sample it.");
                tablet.Refresh();
            });
        }

        float DistanceFromPrevious(int siteIndex)
        {
            Vector3 prev = siteIndex <= 1 ? camp.position : sites[siteIndex - 2].transform.position;
            Vector3 cur = sites[siteIndex - 1].transform.position;
            prev.y = 0; cur.y = 0;
            return Vector3.Distance(prev, cur);
        }

        // ---- sampling ----------------------------------------------------------

        void HandleRockTap(RockSample rock)
        {
            if (Phase != MissionPhase.Sampling)
            {
                hud.ShowHint(Phase == MissionPhase.ConfirmBags
                    ? "Confirm the sample bags at the rover first."
                    : "Sampling is done - check the tablet for the next step.");
                return;
            }

            // Duplicate flow: second rock at site 10, bag + label only.
            if (_awaitingDuplicate)
            {
                if (rock.siteIndex != 10)
                {
                    hud.ShowHint("The duplicate must come from Site 10 - same spot, second bag.");
                    return;
                }
                scanner.OpenDuplicate(rock);
                return;
            }

            if (rock.siteIndex != NextSiteIndex)
            {
                // Spacing rule surfaced to the player: rocks at old/other sites are refused.
                hud.ShowHint(rock.siteIndex < NextSiteIndex
                    ? $"Already sampled here - minimum spacing is {config.minSpacingMeters:0} m. Site {NextSiteIndex} is next."
                    : $"That's Site {rock.siteIndex}'s area. The tablet wants Site {NextSiteIndex} first.");
                return;
            }

            if (!sites[NextSiteIndex - 1].Logged)
            {
                hud.ShowHint("Log the site first - walk to the flag so Good Luck can pull a satellite fix.");
                return;
            }

            scanner.Open(rock, $"MS-{NumberedSampleCount + 1:00}");
        }

        /// <summary>Scanner tells us a rock was assayed (novelty-bias tracking).</summary>
        public void NotifyScanned(RockSample rock)
        {
            if (rock.rockType.shiny) _sawShinyInScanner = true;
        }

        /// <summary>
        /// Scanner requests bagging. Oversized rocks detour through the bag-fit
        /// judgement case; everything else is bagged directly.
        /// </summary>
        public void RequestBagging(RockSample rock, bool isDuplicate)
        {
            if (rock.oversized && !isDuplicate)
            {
                bagFitPanel.Open(largerBag =>
                {
                    _bagFitResolution = largerBag ? "used the oversize spare bag" : "broke it into hand-sized pieces";
                    var seq = largerBag ? bagFitLargerDialogue : bagFitBreakDialogue;
                    dialogue.Play(seq, _ => FinalizeSample(rock, false, _bagFitResolution));
                });
                return;
            }
            FinalizeSample(rock, isDuplicate, "");
        }

        void FinalizeSample(RockSample rock, bool isDuplicate, string oversizeResolution)
        {
            int number = isDuplicate ? 10 : NumberedSampleCount + 1;
            string label = isDuplicate ? "MS-10-B" : $"MS-{number:00}";

            Records.Add(new SampleRecord
            {
                number = number,
                label = label,
                rockName = rock.rockType.displayName,
                correctPick = rock.rockType.isRepresentative,
                oversized = rock.oversized,
                oversizeResolution = oversizeResolution,
                isDuplicate = isDuplicate,
                distanceFromPrevious = DistanceFromPrevious(number)
            });
            if (rock.rockType.shiny) _baggedShiny = true;
            Destroy(rock.gameObject);

            hud.ShowHint($"{label} bagged - {rock.rockType.displayName}.");

            if (!isDuplicate && number == config.siteCount)
            {
                _awaitingDuplicate = true;
                hud.SetObjective("Protocol: collect the DUPLICATE at Site 10 - tap a second rock here.");
            }
            else if (isDuplicate)
            {
                _awaitingDuplicate = false;
                Phase = MissionPhase.LayoutCheck;
                hud.SetObjective("All 11 collected. Return to the rover and tap the TARP to lay them out.");
            }
            else
            {
                NextSiteIndex = number + 1;
                hud.SetObjective($"Head to the Site {NextSiteIndex} flag and log it with Good Luck.");
            }
            tablet.Refresh();
        }

        // ---- end-of-loop steps -------------------------------------------------

        void DoLayoutCheck()
        {
            // Spawn the 11 bags in a grid on the tarp so the player can eyeball the count.
            for (int i = 0; i < Records.Count; i++)
            {
                var bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bag.name = "LayoutBag_" + Records[i].label;
                Object.Destroy(bag.GetComponent<Collider>());
                bag.transform.SetParent(layoutRoot, false);
                int col = i % 4, row = i / 4;
                bag.transform.localPosition = new Vector3(-0.9f + col * 0.6f, 0.6f, 0.55f - row * 0.55f);
                bag.transform.localScale = new Vector3(0.34f, 0.18f, 0.26f);
                if (bagMaterial != null) bag.GetComponent<MeshRenderer>().sharedMaterial = bagMaterial;
                _layoutBags.Add(bag);
            }

            dialogue.Play(layoutDialogue, _ =>
            {
                Phase = MissionPhase.PackAndLoad;
                hud.SetObjective("Count verified: 11/11. Tap the ROVER to pack and load the crate.");
                tablet.Refresh();
            });
        }

        void DoPackAndLoad()
        {
            foreach (var bag in _layoutBags) if (bag != null) Destroy(bag);
            _layoutBags.Clear();

            dialogue.Play(loadDialogue, _ =>
            {
                Phase = MissionPhase.SendConfirmation;
                hud.SetObjective("Open the TABLET and send the shipment confirmation.");
                tablet.Refresh();
            });
        }

        /// <summary>Hooked to the tablet's SEND button.</summary>
        public void OnSendConfirmation()
        {
            if (Phase != MissionPhase.SendConfirmation) return;
            tablet.Close();
            Phase = MissionPhase.Complete;
            dialogue.Play(outroDialogue, _ => ShowEndScreen());
            tablet.Refresh();
        }

        void ShowEndScreen()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Samples shipped: {Records.Count}/11  (MS-01..MS-10 + MS-10-B)");
            sb.AppendLine();
            sb.AppendLine("<b>Field judgement report</b>");
            sb.AppendLine($"- Bag-fit call: {(string.IsNullOrEmpty(_bagFitResolution) ? "not encountered" : _bagFitResolution)}");
            sb.AppendLine(_baggedShiny
                ? "- Novelty bias: you bagged the shiny outlier. The scanner flagged it - the lab wants the average rock, not the trophy."
                : _sawShinyInScanner
                    ? "- Novelty bias: you scanned the shiny one... and put it back. Textbook restraint."
                    : "- Novelty bias: you never even picked up the shiny one. Impressive or unobservant.");
            sb.AppendLine(_caughtLocatorError
                ? "- Satellite log: you caught the 5,000 km downlink error. Big Boss would not have."
                : "- Satellite log: the 5,000 km downlink error slipped past you. Good Luck caught it. Eventually.");
            sb.AppendLine();
            sb.AppendLine("Field exercise complete. Big Boss was, as ever, unavailable for comment.");

            hud.SetObjective(""); // clear the HUD behind the end screen
            hud.ShowEndScreen("SHIPMENT CONFIRMED - INBOUND", sb.ToString());
        }
    }
}

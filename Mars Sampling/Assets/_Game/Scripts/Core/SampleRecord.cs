using System;
using UnityEngine;

namespace MarsSampling
{
    /// <summary>
    /// One bagged, labelled sample. MissionManager keeps the ordered list of these;
    /// the tablet and the end screen render from it.
    /// </summary>
    [Serializable]
    public class SampleRecord
    {
        public int number;                 // 1..10 (duplicate reuses 10)
        public string label;               // "MS-01" .. "MS-10", duplicate = "MS-10-B"
        public string rockName;            // rock type display name
        public bool correctPick;           // scanner verdict at time of bagging
        public bool oversized;             // triggered the bag-fit case
        public string oversizeResolution;  // "spare bag" / "broken up" / ""
        public bool isDuplicate;           // the 11th sample at site 10
        public float distanceFromPrevious; // metres, as verified via the locator dialogue
    }
}

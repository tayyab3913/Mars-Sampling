using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MarsSampling
{
    /// <summary>Small UI helpers shared by the input code.</summary>
    public static class UiUtil
    {
        static readonly List<RaycastResult> Results = new List<RaycastResult>();

        /// <summary>
        /// True if a screen position is over any raycastable UI element.
        /// Used so world taps don't fire through buttons/panels.
        /// </summary>
        public static bool IsPointerOverUi(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            var ped = new PointerEventData(EventSystem.current) { position = screenPos };
            Results.Clear();
            EventSystem.current.RaycastAll(ped, Results);
            return Results.Count > 0;
        }
    }
}

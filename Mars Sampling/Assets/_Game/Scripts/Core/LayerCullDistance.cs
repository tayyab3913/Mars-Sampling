using UnityEngine;

namespace MarsSampling
{
    /// <summary>
    /// Applies a per-layer camera cull distance at runtime. Camera.layerCullDistances
    /// does not survive scene serialization, so the mass pebble scatter (layer
    /// "WildDebris") is culled here instead - it caps how many of the ~5,000
    /// pebbles a mid-range phone ever has to draw.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LayerCullDistance : MonoBehaviour
    {
        public string layerName = "WildDebris";
        public float distance = 170f;

        void Awake()
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) return;

            var cam = GetComponent<Camera>();
            var distances = new float[32];
            distances[layer] = distance;
            cam.layerCullDistances = distances;
            cam.layerCullSpherical = true;
        }
    }
}

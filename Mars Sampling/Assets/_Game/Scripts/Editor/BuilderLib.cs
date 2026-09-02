using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MarsSampling.EditorTools
{
    /// <summary>
    /// Factory helpers used by MarsSceneBuilder: procedural meshes (terrain,
    /// rocks, mountain backdrop), placeholder noise textures, URP materials and
    /// uGUI construction. Editor-only.
    /// </summary>
    public static class BuilderLib
    {
        // ------------------------------------------------------------- textures

        /// <summary>Generate a tileable-ish Perlin noise texture and save it as a PNG asset.</summary>
        public static Texture2D NoiseTexture(string assetPath, int size, Color a, Color b, float scale, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * scale + seed * 13.7f, y * scale + seed * 7.9f);
                    n = 0.65f * n + 0.35f * Mathf.PerlinNoise(x * scale * 3.1f + seed * 3.3f, y * scale * 3.1f + seed * 5.1f);
                    pixels[y * size + x] = Color.Lerp(a, b, n);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        // ------------------------------------------------------------ materials

        /// <summary>Create and save a URP Lit material.</summary>
        public static Material Lit(string assetPath, Color color, Texture2D baseMap,
                                   float metallic, float smoothness, bool instancing)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            m.enableInstancing = instancing;
            AssetDatabase.CreateAsset(m, assetPath);
            return m;
        }

        // --------------------------------------------------------------- meshes

        /// <summary>
        /// Heightfield grid mesh. UV0 tiles the albedo every <paramref name="uvTileMeters"/> m;
        /// UV2 is a clean 0..1 planar map for the lightmap.
        /// </summary>
        public static Mesh Grid(string name, float size, int res, Func<float, float, float> height, float uvTileMeters)
        {
            var verts = new Vector3[(res + 1) * (res + 1)];
            var uv = new Vector2[verts.Length];
            var uv2 = new Vector2[verts.Length];
            float half = size * 0.5f;

            for (int iz = 0, i = 0; iz <= res; iz++)
            {
                for (int ix = 0; ix <= res; ix++, i++)
                {
                    float x = -half + ix * (size / res);
                    float z = -half + iz * (size / res);
                    verts[i] = new Vector3(x, height(x, z), z);
                    uv[i] = new Vector2(x / uvTileMeters, z / uvTileMeters);
                    uv2[i] = new Vector2((x + half) / size, (z + half) / size);
                }
            }

            var tris = new int[res * res * 6];
            for (int iz = 0, t = 0; iz < res; iz++)
            {
                for (int ix = 0; ix < res; ix++)
                {
                    int i0 = iz * (res + 1) + ix;
                    int i1 = i0 + 1;
                    int i2 = i0 + res + 1;
                    int i3 = i2 + 1;
                    tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
                    tris[t++] = i1; tris[t++] = i2; tris[t++] = i3;
                }
            }

            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.uv2 = uv2;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Low-poly rock: icosphere (1 subdivision) with radial noise, a flattened
        /// underside so it sits on the ground, and flat shading. Local radius ~0.5.
        /// </summary>
        public static Mesh Rock(string name, int seed)
        {
            List<Vector3> verts;
            List<int> tris;
            Icosphere(1, out verts, out tris);

            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 dir = verts[i].normalized;
                float n = Mathf.PerlinNoise(dir.x * 1.9f + seed * 0.517f, dir.y * 1.7f + dir.z * 1.3f + seed * 0.331f);
                float r = 0.5f * (0.72f + 0.55f * n);
                Vector3 v = dir * r;
                v.y = Mathf.Max(v.y, -0.32f); // flat-ish bottom
                verts[i] = v;
            }

            var mesh = FlatShade(verts, tris);
            mesh.name = name;
            return mesh;
        }

        static void Icosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3>
            {
                new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
                new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
                new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1)
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;

            tris = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };

            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var newTris = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Midpoint(verts, cache, a, b);
                    int bc = Midpoint(verts, cache, b, c);
                    int ca = Midpoint(verts, cache, c, a);
                    newTris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = newTris;
            }
        }

        static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            if (cache.TryGetValue(key, out int idx)) return idx;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            idx = verts.Count - 1;
            cache[key] = idx;
            return idx;
        }

        /// <summary>Rebuild with one vertex per triangle corner so normals are faceted.</summary>
        public static Mesh FlatShade(List<Vector3> verts, List<int> tris)
        {
            var outVerts = new Vector3[tris.Count];
            var outTris = new int[tris.Count];
            for (int i = 0; i < tris.Count; i++)
            {
                outVerts[i] = verts[tris[i]];
                outTris[i] = i;
            }
            var mesh = new Mesh { vertices = outVerts, triangles = outTris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Jagged silhouette ring of distant mountains, faces pointing inward.
        /// Sits outside the playable terrain; fog does most of the work.
        /// </summary>
        public static Mesh MountainRing(string name, int segments, float radius, float baseY,
                                        float minHeight, float maxHeight, int seed)
        {
            var rnd = new System.Random(seed);
            var verts = new Vector3[segments * 2];
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float aPeak = (i + 0.5f) / segments * Mathf.PI * 2f;
                float rPeak = radius + (float)(rnd.NextDouble() * 90.0 - 45.0);
                float h = Mathf.Lerp(minHeight, maxHeight, (float)rnd.NextDouble());
                verts[i * 2] = new Vector3(Mathf.Cos(a) * radius, baseY, Mathf.Sin(a) * radius);
                verts[i * 2 + 1] = new Vector3(Mathf.Cos(aPeak) * rPeak, baseY + h, Mathf.Sin(aPeak) * rPeak);
            }

            var tris = new List<int>(segments * 6);
            for (int i = 0; i < segments; i++)
            {
                int b0 = i * 2, p0 = i * 2 + 1;
                int b1 = ((i + 1) % segments) * 2, p1 = ((i + 1) % segments) * 2 + 1;
                tris.AddRange(new[] { b0, b1, p0 });  // inward-facing (verified winding)
                tris.AddRange(new[] { p0, b1, p1 });
            }

            var mesh = FlatShade(new List<Vector3>(verts), tris);
            mesh.name = name;
            return mesh;
        }

        // ------------------------------------------------------------------- UI

        public static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static Sprite BuiltinSprite(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
        public static Sprite UiSprite => BuiltinSprite("UI/Skin/UISprite.psd");
        public static Sprite CircleSprite => BuiltinSprite("UI/Skin/Knob.psd");
        public static Sprite BackgroundSprite => BuiltinSprite("UI/Skin/Background.psd");

        /// <summary>Set up a RectTransform in one call.</summary>
        public static RectTransform Rect(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color,
                                  Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                  Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Rect(go, anchorMin, anchorMax, pivot, pos, size);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text MakeText(Transform parent, string name, string content, int fontSize,
                                    Color color, TextAnchor align,
                                    Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                    Vector2 pos, Vector2 size, bool bold = false, bool shadow = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Rect(go, anchorMin, anchorMax, pivot, pos, size);
            var text = go.GetComponent<Text>();
            text.font = UiFont;
            text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.alignment = align;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
                sh.effectDistance = new Vector2(2f, -2f);
            }
            return text;
        }

        public static Button MakeButton(Transform parent, string name, string label, int fontSize,
                                        Color bgColor, Color textColor,
                                        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                        Vector2 pos, Vector2 size, out Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Rect(go, anchorMin, anchorMax, pivot, pos, size);

            var img = go.GetComponent<Image>();
            img.sprite = UiSprite;
            img.type = Image.Type.Sliced;
            img.color = bgColor;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            labelText = MakeText(go.transform, "Label", label, fontSize, textColor, TextAnchor.MiddleCenter,
                                 Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, bold: true);
            return btn;
        }
    }
}

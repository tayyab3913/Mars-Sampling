using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MarsSampling.EditorTools
{
    /// <summary>
    /// One-click level construction: Mars Sampling > Build Level rebuilds the whole
    /// playable scene from scratch - terrain, backdrop, lighting, base camp, the 10
    /// sample sites with rock clusters, all data assets (rock types, config,
    /// dialogue) and the full mobile UI, fully wired.
    ///
    /// Everything is deterministic (fixed seeds), so a rebuild produces the same
    /// level. Content lives in the generated assets under _Game/Data - tweak those
    /// in the inspector, or change the authored text/layout here and rebuild.
    /// </summary>
    public static class MarsSceneBuilder
    {
        const string Root = "Assets/_Game";
        const string ScenePath = Root + "/Scenes/Mars.unity";
        const string MatDir = Root + "/Materials";
        const string TexDir = Root + "/Textures";
        const string MeshDir = Root + "/Meshes";
        const string DataDir = Root + "/Data";
        const string RockDir = Root + "/Data/RockTypes";
        const string DlgDir = Root + "/Data/Dialogue";

        const string GL = "Good Luck (radio)";
        const string YOU = "You";

        // Base camp and the 10 site positions (metres, XZ). Consecutive spacing
        // is 63-95 m - all above the 50 m minimum the tablet enforces.
        static readonly Vector2 Camp = new Vector2(0f, -180f);
        static readonly Vector2[] SitePos =
        {
            new Vector2(-20f, -120f), new Vector2(45f, -75f), new Vector2(-15f, -15f),
            new Vector2(60f, 40f),    new Vector2(0f, 105f),  new Vector2(-80f, 60f),
            new Vector2(-140f, -10f), new Vector2(-90f, -90f),new Vector2(-160f, -150f),
            new Vector2(-190f, -60f)
        };

        // Materials built once per run, shared by all sections.
        static Material _terrainMat, _basaltMat, _sandMat, _hematiteMat, _mountainMat,
                        _poleMat, _roverMat, _darkMat, _tarpMat, _bagMat, _crateMat, _suitMat;

        [MenuItem("Mars Sampling/Build Level (full rebuild)")]
        public static void Build()
        {
            EnsureFolders();
            BuildTexturesAndMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildTerrain();
            BuildMountains();
            BuildLighting();

            var rockTypes = BuildRockTypeAssets();
            var config = BuildConfigAsset();
            var rockMeshes = BuildRockMeshes();

            var player = BuildPlayer(out Camera cam);
            BuildCamp(out StationProp bagBox, out StationProp tarp, out StationProp rover, out Transform campCenter);
            var sites = BuildSites(rockTypes, rockMeshes);
            BuildDecorativeRocks(rockTypes, rockMeshes);

            var ui = BuildUi();
            WireMission(config, player, cam, campCenter, tarp.transform, sites, ui);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[MarsSceneBuilder] Level built and saved to " + ScenePath);
        }

        // ---------------------------------------------------------------- helpers

        static void EnsureFolders()
        {
            foreach (var dir in new[] { Root, Root + "/Scenes", MatDir, TexDir, MeshDir, DataDir, RockDir, DlgDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        /// <summary>Terrain height field: dunes + shallow craters, flattened at camp.</summary>
        static float H(float x, float z)
        {
            float h = Mathf.PerlinNoise(x * 0.011f + 37.3f, z * 0.011f + 91.2f) * 7f;
            h += Mathf.PerlinNoise(x * 0.045f + 17.1f, z * 0.045f + 43.7f) * 1.5f;
            h -= Crater(x, z, 120f, 150f, 55f, 4f);
            h -= Crater(x, z, -150f, 120f, 45f, 3.5f);
            h -= Crater(x, z, 150f, -140f, 60f, 4f);

            float d = Vector2.Distance(new Vector2(x, z), Camp);
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 40f, d));
            return Mathf.Lerp(3.2f, h, t);
        }

        static float Crater(float x, float z, float cx, float cz, float r, float depth)
        {
            float d = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz));
            if (d > r) return 0f;
            return (Mathf.Cos(d / r * Mathf.PI) * 0.5f + 0.5f) * depth;
        }

        static Vector3 G(float x, float z) => new Vector3(x, H(x, z), z);

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos,
                               Vector3 scale, Material mat, bool keepCollider = false, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // --------------------------------------------------- textures + materials

        static void BuildTexturesAndMaterials()
        {
            var terrainTex = BuilderLib.NoiseTexture(TexDir + "/terrain_noise.png", 256,
                new Color(0.52f, 0.31f, 0.21f), new Color(0.66f, 0.42f, 0.28f), 0.045f, 1);
            var basaltTex = BuilderLib.NoiseTexture(TexDir + "/basalt_noise.png", 128,
                new Color(0.30f, 0.24f, 0.22f), new Color(0.40f, 0.31f, 0.27f), 0.09f, 2);
            var sandTex = BuilderLib.NoiseTexture(TexDir + "/sandstone_noise.png", 128,
                new Color(0.70f, 0.53f, 0.36f), new Color(0.80f, 0.64f, 0.46f), 0.02f, 3);
            var hemaTex = BuilderLib.NoiseTexture(TexDir + "/hematite_noise.png", 128,
                new Color(0.24f, 0.21f, 0.26f), new Color(0.42f, 0.37f, 0.45f), 0.12f, 4);

            _terrainMat = BuilderLib.Lit(MatDir + "/Terrain.mat", Color.white, terrainTex, 0f, 0.03f, false);
            _basaltMat = BuilderLib.Lit(MatDir + "/RockBasalt.mat", Color.white, basaltTex, 0f, 0.08f, true);
            _sandMat = BuilderLib.Lit(MatDir + "/RockSandstone.mat", Color.white, sandTex, 0f, 0.12f, true);
            _hematiteMat = BuilderLib.Lit(MatDir + "/RockHematite.mat", Color.white, hemaTex, 0.85f, 0.82f, true,
                emission: new Color(0.16f, 0.13f, 0.22f)); // faint glint so the bait reads "shiny" at a glance
            _mountainMat = BuilderLib.Lit(MatDir + "/Mountains.mat", new Color(0.44f, 0.27f, 0.20f), null, 0f, 0f, false);
            _poleMat = BuilderLib.Lit(MatDir + "/FlagOrange.mat", new Color(0.95f, 0.42f, 0.08f), null, 0f, 0.25f, false);
            _roverMat = BuilderLib.Lit(MatDir + "/RoverBody.mat", new Color(0.38f, 0.38f, 0.30f), null, 0.35f, 0.35f, false);
            _darkMat = BuilderLib.Lit(MatDir + "/DarkMetal.mat", new Color(0.16f, 0.16f, 0.16f), null, 0.4f, 0.3f, false);
            _tarpMat = BuilderLib.Lit(MatDir + "/Tarp.mat", new Color(0.42f, 0.19f, 0.14f), null, 0f, 0.1f, false);
            _bagMat = BuilderLib.Lit(MatDir + "/SampleBag.mat", new Color(0.86f, 0.84f, 0.78f), null, 0f, 0.2f, false);
            _crateMat = BuilderLib.Lit(MatDir + "/Crate.mat", new Color(0.5f, 0.42f, 0.28f), null, 0f, 0.15f, false);
            _suitMat = BuilderLib.Lit(MatDir + "/Suit.mat", new Color(0.85f, 0.5f, 0.2f), null, 0f, 0.3f, false);

            // Butterscotch Mars sky: a hand-rolled gradient on the Panoramic skybox
            // (the procedural skybox insists on rendering Earth-blue).
            var skyTex = BuilderLib.GradientSky(TexDir + "/sky_gradient.png",
                ground: new Color(0.46f, 0.29f, 0.21f),
                horizon: new Color(0.85f, 0.58f, 0.40f),
                zenith: new Color(0.44f, 0.31f, 0.28f));
            var sky = new Material(Shader.Find("Skybox/Panoramic"));
            sky.SetTexture("_MainTex", skyTex);
            sky.SetFloat("_Mapping", 1f);   // latitude-longitude
            sky.SetFloat("_ImageType", 0f); // 360 degrees
            sky.SetFloat("_Exposure", 1.0f);
            AssetDatabase.CreateAsset(sky, MatDir + "/MarsSky.mat");
        }

        // ------------------------------------------------------------------ world

        static void BuildTerrain()
        {
            var mesh = BuilderLib.Grid("MarsTerrain", 480f, 120, H, 12f);
            AssetDatabase.CreateAsset(mesh, MeshDir + "/Terrain.asset");

            var go = new GameObject("Terrain", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = _terrainMat;
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
        }

        static void BuildMountains()
        {
            // Ring sits just outside the walkable terrain, INSIDE the fog range so
            // the peaks read as soft silhouettes on the horizon.
            var mesh = BuilderLib.MountainRing("Mountains", 90, 420f, -25f, 45f, 110f, 77);
            AssetDatabase.CreateAsset(mesh, MeshDir + "/Mountains.asset");

            var go = new GameObject("MountainBackdrop", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = _mountainMat;
            // Static for batching, but NOT lightmapped (distant silhouette only).
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        static void BuildLighting()
        {
            var sunGo = new GameObject("Sun", typeof(Light));
            var sun = sunGo.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.91f, 0.78f);
            sun.intensity = 1.15f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sun.shadows = LightShadows.Soft; // used by the bake; realtime cost governed by URP asset
            sunGo.transform.rotation = Quaternion.Euler(38f, -55f, 0f);

            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/MarsSky.mat");
            RenderSettings.sun = sun;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 560f;
            RenderSettings.fogColor = new Color(0.80f, 0.55f, 0.39f); // matches the sky horizon band

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.70f, 0.52f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.34f, 0.26f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.18f, 0.14f);
            RenderSettings.subtractiveShadowColor = new Color(0.35f, 0.23f, 0.18f);

            // Low-cost baked GI settings for a mobile blockout (single 1024 map).
            var ls = new LightingSettings
            {
                bakedGI = true,
                realtimeGI = false,
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                mixedBakeMode = MixedLightingMode.Subtractive,
                lightmapResolution = 1f,
                lightmapMaxSize = 1024,
                directSampleCount = 16,
                indirectSampleCount = 32,
                environmentSampleCount = 32,
                ao = true,
                aoMaxDistance = 1.5f
            };
            AssetDatabase.CreateAsset(ls, DataDir + "/MarsLightingSettings.lighting");
            Lightmapping.lightingSettings = ls;
        }

        // ----------------------------------------------------------------- player

        static PlayerController BuildPlayer(out Camera cam)
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = G(2f, -174f) + Vector3.up * 0.1f;
            player.transform.rotation = Quaternion.Euler(0f, 215f, 0f); // spawn facing the rover camp

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.slopeLimit = 50f;

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot.transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.12f;
            cam.farClipPlane = 900f;
            camGo.AddComponent<AudioListener>();

            var pc = player.AddComponent<PlayerController>();
            pc.cameraPivot = pivot.transform;
            var interactor = player.AddComponent<PlayerInteractor>();
            interactor.cam = cam;
            pc.interactor = interactor;
            return pc;
        }

        // ------------------------------------------------------------------- camp

        static void BuildCamp(out StationProp bagBox, out StationProp tarp, out StationProp rover, out Transform campCenter)
        {
            campCenter = new GameObject("CampCenter").transform;
            campCenter.position = G(Camp.x, Camp.y);

            // Rover: worn, boxy hauler out of primitives (Atreides-utilitarian blockout).
            var roverGo = new GameObject("Rover");
            roverGo.transform.position = G(-6f, -186f);
            roverGo.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            var r = roverGo.transform;
            Part(PrimitiveType.Cube, "Chassis", r, new Vector3(0f, 0.9f, 0f), new Vector3(3.6f, 0.5f, 2.0f), _roverMat);
            Part(PrimitiveType.Cube, "Cab", r, new Vector3(1.25f, 1.55f, 0f), new Vector3(1.1f, 0.9f, 1.9f), _roverMat);
            Part(PrimitiveType.Cube, "BedFloor", r, new Vector3(-0.7f, 1.2f, 0f), new Vector3(2.1f, 0.12f, 1.9f), _darkMat);
            Part(PrimitiveType.Cube, "BedWallL", r, new Vector3(-0.7f, 1.5f, 0.9f), new Vector3(2.1f, 0.5f, 0.1f), _roverMat);
            Part(PrimitiveType.Cube, "BedWallR", r, new Vector3(-0.7f, 1.5f, -0.9f), new Vector3(2.1f, 0.5f, 0.1f), _roverMat);
            Part(PrimitiveType.Cube, "BedWallBack", r, new Vector3(-1.7f, 1.5f, 0f), new Vector3(0.1f, 0.5f, 1.9f), _roverMat);
            Part(PrimitiveType.Cube, "SampleCrate", r, new Vector3(-0.7f, 1.7f, 0f), new Vector3(1.2f, 0.8f, 1.2f), _crateMat);
            Part(PrimitiveType.Cylinder, "Mast", r, new Vector3(1.5f, 2.6f, 0.7f), new Vector3(0.05f, 0.6f, 0.05f), _darkMat);
            Part(PrimitiveType.Sphere, "Dish", r, new Vector3(1.5f, 3.3f, 0.7f), new Vector3(0.35f, 0.35f, 0.35f), _darkMat);
            for (int i = 0; i < 6; i++)
            {
                float x = new[] { 1.2f, 0f, -1.2f }[i / 2];
                float z = (i % 2 == 0) ? 1.05f : -1.05f;
                Part(PrimitiveType.Cylinder, "Wheel" + i, r, new Vector3(x, 0.55f, z),
                     new Vector3(1.1f, 0.18f, 1.1f), _darkMat, euler: new Vector3(90f, 0f, 0f));
            }
            var roverCol = roverGo.AddComponent<BoxCollider>();
            roverCol.center = new Vector3(0f, 1.3f, 0f);
            roverCol.size = new Vector3(4.8f, 2.7f, 2.7f);
            rover = roverGo.AddComponent<StationProp>();
            rover.kind = StationProp.Kind.Vehicle;
            roverGo.isStatic = true;

            // Layout tarp for the verify-count step.
            var tarpGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tarpGo.name = "LayoutTarp";
            tarpGo.transform.position = G(2.5f, -182f) + Vector3.up * 0.05f;
            tarpGo.transform.localScale = new Vector3(3.2f, 0.08f, 2.2f);
            tarpGo.GetComponent<MeshRenderer>().sharedMaterial = _tarpMat;
            tarp = tarpGo.AddComponent<StationProp>();
            tarp.kind = StationProp.Kind.LayoutMat;
            tarpGo.isStatic = true;

            // Bag box: step 1 of the checklist.
            var boxGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boxGo.name = "BagBox";
            boxGo.transform.position = G(-2.5f, -181f) + Vector3.up * 0.3f;
            boxGo.transform.localScale = new Vector3(0.9f, 0.55f, 0.65f);
            boxGo.GetComponent<MeshRenderer>().sharedMaterial = _bagMat;
            bagBox = boxGo.AddComponent<StationProp>();
            bagBox.kind = StationProp.Kind.BagBox;
            boxGo.isStatic = true;

            // Good Luck: a static presence at camp; all his lines arrive by radio.
            var gl = new GameObject("GoodLuck");
            gl.transform.position = G(-3.5f, -179f);
            Part(PrimitiveType.Capsule, "Body", gl.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 0.9f, 0.6f), _suitMat, keepCollider: true);
            Part(PrimitiveType.Sphere, "Helmet", gl.transform, new Vector3(0f, 1.95f, 0f), new Vector3(0.45f, 0.45f, 0.45f), _bagMat);
        }

        // ------------------------------------------------------------------ sites

        static SampleSite[] BuildSites(RockTypeDef[] types, Mesh[] rockMeshes)
        {
            // types: [0]=basalt, [1]=sandstone, [2]=hematite
            var sitesRoot = new GameObject("SampleSites").transform;
            var sites = new SampleSite[SitePos.Length];
            var rnd = new System.Random(4242);

            for (int i = 0; i < SitePos.Length; i++)
            {
                int n = i + 1;
                Vector3 pos = G(SitePos[i].x, SitePos[i].y);

                var siteGo = new GameObject("Site_" + n);
                siteGo.transform.SetParent(sitesRoot, false);
                siteGo.transform.position = pos;

                var trigger = siteGo.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 9f;
                var rb = siteGo.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                var site = siteGo.AddComponent<SampleSite>();
                site.index = n;
                site.logDialogue = BuildSiteDialogue(n);
                sites[i] = site;

                // Flag: pole + cloth + floating billboard number (world prop, not UI).
                Part(PrimitiveType.Cylinder, "Pole", siteGo.transform, new Vector3(0f, 3f, 0f), new Vector3(0.1f, 3f, 0.1f), _darkMat);
                Part(PrimitiveType.Cube, "Flag", siteGo.transform, new Vector3(0.65f, 5.5f, 0f), new Vector3(1.3f, 0.75f, 0.05f), _poleMat);
                var numGo = new GameObject("Number", typeof(TextMesh), typeof(Billboard));
                numGo.transform.SetParent(siteGo.transform, false);
                numGo.transform.localPosition = new Vector3(0f, 6.8f, 0f);
                var tm = numGo.GetComponent<TextMesh>();
                tm.font = BuilderLib.UiFont;
                numGo.GetComponent<MeshRenderer>().sharedMaterial = BuilderLib.UiFont.material;
                tm.fontSize = 64;
                tm.characterSize = 0.26f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = Color.white;
                tm.text = n.ToString();

                BuildSiteRocks(siteGo.transform, n, types, rockMeshes, rnd);
            }
            return sites;
        }

        static void BuildSiteRocks(Transform parent, int n, RockTypeDef[] types, Mesh[] meshes, System.Random rnd)
        {
            // Cluster composition drives the two scripted judgement cases:
            //  - Site 2: every rock is oversized  -> bag-fit case always triggers.
            //  - Site 6: one glittering hematite among dull basalt -> novelty-bias trap.
            var picks = new List<(RockTypeDef type, bool oversized, float scale, float yLift)>();
            if (n == 2)
            {
                for (int j = 0; j < 4; j++)
                    picks.Add((types[j % 2], true, 1.9f + (float)rnd.NextDouble() * 0.4f, 0f));
            }
            else if (n == 6)
            {
                picks.Add((types[2], false, 0.95f, 0.25f)); // the shiny bait, propped up a little
                for (int j = 0; j < 5; j++) picks.Add((types[0], false, 0.55f + (float)rnd.NextDouble() * 0.3f, 0f));
                picks.Add((types[1], false, 0.7f, 0f));
            }
            else
            {
                for (int j = 0; j < 5; j++) picks.Add((types[0], false, 0.55f + (float)rnd.NextDouble() * 0.3f, 0f));
                for (int j = 0; j < 2; j++) picks.Add((types[1], false, 0.6f + (float)rnd.NextDouble() * 0.25f, 0f));
            }

            for (int j = 0; j < picks.Count; j++)
            {
                var (type, oversized, scale, yLift) = picks[j];
                float angle = (float)(j / (double)picks.Count * Mathf.PI * 2.0 + rnd.NextDouble() * 0.5);
                float dist = 2.5f + (float)rnd.NextDouble() * 3f;
                float wx = parent.position.x + Mathf.Cos(angle) * dist;
                float wz = parent.position.z + Mathf.Sin(angle) * dist;

                var go = new GameObject($"Rock_S{n}_{j}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, true);
                go.transform.position = new Vector3(wx, H(wx, wz) + 0.28f * scale + yLift, wz);
                go.transform.localScale = Vector3.one * scale;
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.GetComponent<MeshFilter>().sharedMesh = meshes[rnd.Next(meshes.Length)];
                go.GetComponent<MeshRenderer>().sharedMaterial = type.material;

                var col = go.AddComponent<SphereCollider>();
                col.radius = 0.62f; // generous for fat-finger taps

                var rock = go.AddComponent<RockSample>();
                rock.rockType = type;
                rock.oversized = oversized;
                rock.siteIndex = n;
            }
        }

        static void BuildDecorativeRocks(RockTypeDef[] types, Mesh[] meshes)
        {
            var root = new GameObject("DecorativeRocks").transform;
            var rnd = new System.Random(9001);
            int placed = 0;
            while (placed < 340)
            {
                float x = (float)(rnd.NextDouble() * 470.0 - 235.0);
                float z = (float)(rnd.NextDouble() * 470.0 - 235.0);

                bool tooClose = Vector2.Distance(new Vector2(x, z), Camp) < 18f;
                foreach (var sp in SitePos)
                    if (Vector2.Distance(new Vector2(x, z), sp) < 12f) { tooClose = true; break; }
                if (tooClose) continue;

                float scale = 0.3f + (float)rnd.NextDouble() * 0.45f;
                var go = new GameObject("DecoRock", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(x, H(x, z) + 0.24f * scale, z);
                go.transform.localScale = Vector3.one * scale;
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.GetComponent<MeshFilter>().sharedMesh = meshes[rnd.Next(meshes.Length)];
                go.GetComponent<MeshRenderer>().sharedMaterial = rnd.NextDouble() < 0.6 ? types[0].material : types[1].material;
                go.isStatic = true; // no collider: decorative only, static-batched
                placed++;
            }
        }

        // ------------------------------------------------------------ data assets

        static Mesh[] BuildRockMeshes()
        {
            var meshes = new Mesh[4];
            for (int i = 0; i < meshes.Length; i++)
            {
                meshes[i] = BuilderLib.Rock("Rock" + i, i * 31 + 7);
                Unwrapping.GenerateSecondaryUVSet(meshes[i]); // lightmap UVs
                AssetDatabase.CreateAsset(meshes[i], $"{MeshDir}/Rock{i}.asset");
            }
            return meshes;
        }

        static MissionConfig BuildConfigAsset()
        {
            var cfg = ScriptableObject.CreateInstance<MissionConfig>();
            cfg.minSpacingMeters = 50f;
            cfg.siteCount = 10;
            cfg.interactRange = 3.5f;
            AssetDatabase.CreateAsset(cfg, DataDir + "/MissionConfig.asset");
            return cfg;
        }

        static RockTypeDef[] BuildRockTypeAssets()
        {
            var basalt = ScriptableObject.CreateInstance<RockTypeDef>();
            basalt.displayName = "Vesicular Basalt";
            basalt.fieldDescription = "Dull, reddish-grey, fine-grained, pocked with tiny gas bubbles. Looks like every other rock out here. That might be the point.";
            basalt.silicaPct = 48.6f; basalt.ironPct = 17.2f; basalt.magnesiumPct = 9.1f;
            basalt.grainNote = "fine-grained, vesicular (gas pockets)";
            basalt.isRepresentative = true;
            basalt.correctText = "Composition sits inside the local basalt cluster. This is what the site actually looks like, statistically. The lab wants exactly this.";
            basalt.incorrectText = "";
            basalt.material = _basaltMat;

            var sand = ScriptableObject.CreateInstance<RockTypeDef>();
            sand.displayName = "Layered Sandstone";
            sand.fieldDescription = "Banded tan stone - sediment layers you can count with a thumbnail. Solid, unglamorous field geology.";
            sand.silicaPct = 78.2f; sand.ironPct = 5.8f; sand.magnesiumPct = 2.4f;
            sand.grainNote = "banded, medium-grained, sedimentary";
            sand.isRepresentative = true;
            sand.correctText = "Consistent with the surrounding sedimentary units - a representative pick for this site.";
            sand.incorrectText = "";
            sand.material = _sandMat;

            var hema = ScriptableObject.CreateInstance<RockTypeDef>();
            hema.displayName = "Specular Hematite Nodule";
            hema.fieldDescription = "A metallic, glittering nodule. Very shiny. Suspiciously shiny. It practically asked to be picked up.";
            hema.silicaPct = 11.8f; hema.ironPct = 68.4f; hema.magnesiumPct = 1.2f;
            hema.grainNote = "coarse specular crystal faces";
            hema.isRepresentative = false;
            hema.correctText = "";
            hema.incorrectText = "High-grade iron outlier - striking, and it represents almost nothing around it. Survey protocol wants the average rock, not the trophy. No penalty: noted, and the lab will still enjoy it.";
            hema.material = _hematiteMat;
            hema.shiny = true;

            AssetDatabase.CreateAsset(basalt, RockDir + "/Basalt.asset");
            AssetDatabase.CreateAsset(sand, RockDir + "/Sandstone.asset");
            AssetDatabase.CreateAsset(hema, RockDir + "/Hematite.asset");
            return new[] { basalt, sand, hema };
        }

        // -------------------------------------------------------------- dialogue

        static DialogueLine[] Lines(params (string sp, string tx)[] arr)
        {
            var result = new DialogueLine[arr.Length];
            for (int i = 0; i < arr.Length; i++)
                result[i] = new DialogueLine { speaker = arr[i].sp, text = arr[i].tx };
            return result;
        }

        static DialogueSequence Dlg(string fileName, params (string sp, string tx)[] arr)
        {
            var seq = ScriptableObject.CreateInstance<DialogueSequence>();
            seq.lines = Lines(arr);
            AssetDatabase.CreateAsset(seq, $"{DlgDir}/{fileName}.asset");
            return seq;
        }

        static float SiteDist(int n) // real spacing, quoted in the locator lines
        {
            Vector2 prev = n <= 1 ? Camp : SitePos[n - 2];
            return Vector2.Distance(prev, SitePos[n - 1]);
        }

        static string Coord(int n)
        {
            Vector2 p = SitePos[n - 1];
            return $"MS-grid {128.00f + p.x * 0.01f:F2}E / {22.00f + p.y * 0.01f:F2}N";
        }

        static DialogueSequence BuildSiteDialogue(int n)
        {
            float d = SiteDist(n);
            string prev = n == 1 ? "camp" : $"Site {n - 1}";

            if (n == 4)
            {
                // The scripted satellite-locator error: the player can catch it
                // against the 50 m spacing rule, or let it slide (Good Luck
                // self-corrects either way - informational, no fail state).
                var seq = ScriptableObject.CreateInstance<DialogueSequence>();
                seq.lines = Lines(
                    (GL, $"Site 4 fix coming through... {Coord(4)}. Locked."),
                    (GL, "Distance from Site 3 reads... five thousand kilometres. Huh. Logging it.")
                );
                seq.hasChoice = true;
                seq.choiceAText = "That can't be right. We walked about ninety metres.";
                seq.choiceBText = "Copy. Log it: 5,000 km.";
                seq.branchA = Lines(
                    (GL, "...Checking. Yes. Decimal drift on the downlink - it happens when Big Boss doesn't renew the calibration contract."),
                    (GL, $"Actual spacing: {d:0} metres. Above the 50 minimum. Nice catch - that number was headed straight for the university records.")
                );
                seq.branchB = Lines(
                    (GL, "Logging five thousand kilo- hold on. That would put Site 4 on the far side of Olympus Mons."),
                    (GL, $"Decimal drift on the downlink. Actual spacing: {d:0} metres, above the 50 minimum. Corrected. Good thing one of us is paying attention.")
                );
                AssetDatabase.CreateAsset(seq, $"{DlgDir}/Site04_Locator.asset");
                return seq;
            }

            var list = new List<(string, string)>
            {
                (GL, $"Locator fix is in. Site {n}: {Coord(n)}.")
            };
            switch (n)
            {
                case 1:
                    list.Add((GL, $"That's {d:0} metres out from the rover. Spacing rule says fifty minimum between sites, so we're clear."));
                    list.Add((GL, "Grab something representative. Which means: the most boring rock you can find. I know. I'm sorry."));
                    break;
                case 6:
                    list.Add((GL, $"{d:0} metres from {prev}. Above minimum. Logged."));
                    list.Add((GL, "Ooh - see the glittery one up on the little mound? Look at it SPARKLE. ...We are absolutely not supposed to take that one. The scanner will say so. But look at it."));
                    break;
                case 10:
                    list.Add((GL, $"{d:0} metres from {prev}. Logged. Last site!"));
                    list.Add((GL, "Protocol wants a duplicate at site ten - two rocks, two bags, same spot. MS-10 and MS-10-B. Don't ask me why ten. Big Boss wrote the protocol. Allegedly."));
                    break;
                default:
                    list.Add((GL, $"{d:0} metres from {prev} - clear of the 50 metre minimum. Verified and logged. Tablet's updated."));
                    break;
            }
            return Dlg($"Site{n:00}_Locator", list.ToArray());
        }

        static void BuildStoryDialogues(out DialogueSequence intro, out DialogueSequence bagCheck,
            out DialogueSequence bagFitLarger, out DialogueSequence bagFitBreak,
            out DialogueSequence layout, out DialogueSequence load, out DialogueSequence outro)
        {
            intro = Dlg("Intro",
                (GL, "Morning! Or whatever this is. Sol-time. You made it down in one piece - already ahead of the last student."),
                (GL, "I'm your field colleague. Big Boss calls me 'Good Luck', which I choose to read as a name and not a warning."),
                (GL, "He was supposed to brief you. He is... not here. He is never here. So: ten rock samples, one duplicate, fifty metres minimum between sites. It's all on your tablet."),
                (GL, "Bags first - protocol. The white box by the rover. Count them, confirm the numbering."));

            bagCheck = Dlg("BagCheck",
                (GL, "Right: MS-01 through MS-10, one duplicate bag MS-10-B, and one oversize spare. Twelve. All numbered, all sealed."),
                (YOU, "Why do we number the bags and not the rocks?"),
                (GL, "Because rocks don't hold ink and Big Boss doesn't hold briefings. Tablet's updated - Site 1 flag is out ahead. Orange. Hard to miss."));

            bagFitLarger = Dlg("BagFit_LargerBag",
                (GL, "Told you it wouldn't fit. Luckily, someone - me - packed the oversize spare."),
                (GL, "We carry exactly one, so that's the budget spent. Whole rock, full context, one very smug colleague. Bag it."));

            bagFitBreak = Dlg("BagFit_Break",
                (GL, "Hammer time. A geologist's hammer is a precision instrument, whatever it looks like."),
                (GL, "There - hand-sized pieces. We lose a little context, the assay's still fine. Bag the freshest face, not the weathered crust."));

            layout = Dlg("LayoutCheck",
                (GL, "Lay them out... MS-01, 02, 03... 09, 10, and 10-B. Eleven bags."),
                (GL, "Count twice, ship once. Nothing missing. I'm honestly a little surprised. Good surprised."));

            load = Dlg("LoadVehicle",
                (GL, "Crate's packed and strapped to the bed. Labels face up, the way the lab likes and never acknowledges."),
                (GL, "One thing left: the tablet wants a shipment confirmation. Official. Very official. Big Boss might even read it."));

            outro = Dlg("Outro",
                (GL, "'Shipment inbound - eleven units, sites logged and spaced.' Sent."),
                (GL, "First field job, zero samples lost, one downlink error survived. Big Boss will take full credit, of course."),
                (GL, "Welcome to field geology. The pay is bad, but at least the commute is forty million kilometres."));
        }

        // --------------------------------------------------------------------- UI

        /// <summary>All the UI components the mission needs, built and wired.</summary>
        struct UiRefs
        {
            public HUDController hud;
            public TabletUI tablet;
            public ScannerUI scanner;
            public BagFitPanel bagFit;
            public DialogueUI dialogueUi;
            public RectTransform joyBase, joyKnob;
            public Button tabletButton;
        }

        static UiRefs BuildUi()
        {
            var refs = new UiRefs();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var ct = canvasGo.transform;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            Color panelBg = new Color(0.05f, 0.07f, 0.09f, 0.96f);
            Color accent = new Color(0.95f, 0.55f, 0.25f);
            Color btnCol = new Color(0.75f, 0.38f, 0.15f, 0.95f);
            Color btnDark = new Color(0.25f, 0.28f, 0.32f, 0.95f);

            // ---- HUD ----
            var objective = BuilderLib.MakeText(ct, "Objective", "", 30, Color.white, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(1150f, 80f), shadow: true);
            var hint = BuilderLib.MakeText(ct, "Hint", "", 28, new Color(1f, 0.9f, 0.65f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(1000f, 70f), shadow: true);
            hint.gameObject.SetActive(false);

            var cross = BuilderLib.Panel(ct, "Crosshair", new Color(1f, 1f, 1f, 0.35f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            cross.sprite = BuilderLib.CircleSprite;
            cross.raycastTarget = false;

            var joyBase = BuilderLib.Panel(ct, "JoystickBase", new Color(1f, 1f, 1f, 0.22f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(300f, 300f), new Vector2(220f, 220f));
            joyBase.sprite = BuilderLib.BackgroundSprite;
            joyBase.raycastTarget = false;
            var joyKnob = BuilderLib.Panel(joyBase.transform, "Knob", new Color(1f, 1f, 1f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(95f, 95f));
            joyKnob.sprite = BuilderLib.CircleSprite;
            joyKnob.raycastTarget = false;
            joyBase.gameObject.SetActive(false);
            refs.joyBase = joyBase.rectTransform;
            refs.joyKnob = joyKnob.rectTransform;

            refs.tabletButton = BuilderLib.MakeButton(ct, "TabletButton", "TABLET", 28, btnDark, Color.white,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-25f, -25f), new Vector2(210f, 92f), out _);

            // ---- Tablet panel ----
            var tabletPanel = BuilderLib.Panel(ct, "TabletPanel", panelBg,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(680f, 950f));
            BuilderLib.MakeText(tabletPanel.transform, "Title", "FIELD TABLET - SAMPLING CHECKLIST", 30, accent, TextAnchor.UpperCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(0f, 50f), bold: true);
            var tabletBody = BuilderLib.MakeText(tabletPanel.transform, "Body", "", 24, new Color(0.85f, 0.9f, 0.92f), TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -35f), new Vector2(-60f, -220f));
            var tabletClose = BuilderLib.MakeButton(tabletPanel.transform, "CloseButton", "X", 30, btnDark, Color.white,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(70f, 70f), out _);
            var sendBtn = BuilderLib.MakeButton(tabletPanel.transform, "SendButton", "SEND SHIPMENT CONFIRMATION", 26,
                new Color(0.2f, 0.55f, 0.25f, 0.95f), Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(560f, 90f), out _);
            sendBtn.gameObject.SetActive(false);
            tabletPanel.gameObject.SetActive(false);

            refs.tablet = canvasGo.AddComponent<TabletUI>();
            refs.tablet.root = tabletPanel.gameObject;
            refs.tablet.bodyText = tabletBody;
            refs.tablet.sendButton = sendBtn;

            // ---- Scanner panel ----
            var scanPanel = BuilderLib.Panel(ct, "ScannerPanel", panelBg,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1000f, 640f));
            var scanTitle = BuilderLib.MakeText(scanPanel.transform, "Title", "FIELD SPECIMEN", 34, accent, TextAnchor.UpperCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(0f, 55f), bold: true);
            var scanBody = BuilderLib.MakeText(scanPanel.transform, "Body", "", 26, new Color(0.85f, 0.9f, 0.92f), TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(-70f, -240f));
            var scanBtn = BuilderLib.MakeButton(scanPanel.transform, "ScanButton", "RUN XRF SCAN", 26, btnCol, Color.white,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 25f), new Vector2(300f, 85f), out _);
            var bagBtn = BuilderLib.MakeButton(scanPanel.transform, "BagButton", "BAG + LABEL", 26,
                new Color(0.2f, 0.55f, 0.25f, 0.95f), Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(30f, 25f), new Vector2(380f, 85f), out var bagLabel);
            var backBtn = BuilderLib.MakeButton(scanPanel.transform, "PutBackButton", "PUT BACK", 26, btnDark, Color.white,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 25f), new Vector2(240f, 85f), out _);
            scanPanel.gameObject.SetActive(false);

            refs.scanner = canvasGo.AddComponent<ScannerUI>();
            refs.scanner.root = scanPanel.gameObject;
            refs.scanner.titleText = scanTitle;
            refs.scanner.bodyText = scanBody;
            refs.scanner.scanButton = scanBtn;
            refs.scanner.bagButton = bagBtn;
            refs.scanner.putBackButton = backBtn;
            refs.scanner.bagButtonLabel = bagLabel;

            // ---- Bag-fit panel ----
            var bagFitPanel = BuilderLib.Panel(ct, "BagFitPanel", panelBg,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(940f, 470f));
            BuilderLib.MakeText(bagFitPanel.transform, "Title", "BAG-FIT PROBLEM", 34, accent, TextAnchor.UpperCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(0f, 55f), bold: true);
            var bagFitBody = BuilderLib.MakeText(bagFitPanel.transform, "Body", "", 27, new Color(0.85f, 0.9f, 0.92f), TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -25f), new Vector2(-70f, -250f));
            var largerBtn = BuilderLib.MakeButton(bagFitPanel.transform, "LargerBagButton", "USE OVERSIZE SPARE BAG", 24, btnCol, Color.white,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 25f), new Vector2(420f, 90f), out _);
            var breakBtn = BuilderLib.MakeButton(bagFitPanel.transform, "BreakButton", "BREAK IT WITH THE HAMMER", 24, btnDark, Color.white,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 25f), new Vector2(420f, 90f), out _);
            bagFitPanel.gameObject.SetActive(false);

            refs.bagFit = canvasGo.AddComponent<BagFitPanel>();
            refs.bagFit.root = bagFitPanel.gameObject;
            refs.bagFit.bodyText = bagFitBody;
            refs.bagFit.largerBagButton = largerBtn;
            refs.bagFit.breakButton = breakBtn;

            // ---- Dialogue panel ----
            var dlgPanel = BuilderLib.Panel(ct, "DialoguePanel", new Color(0f, 0f, 0f, 0.82f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1450f, 310f));
            var speaker = BuilderLib.MakeText(dlgPanel.transform, "Speaker", "", 28, accent, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(35f, -18f), new Vector2(-70f, 40f), bold: true);
            var dlgBody = BuilderLib.MakeText(dlgPanel.transform, "Body", "", 30, Color.white, TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(-70f, -130f));
            var nextBtn = BuilderLib.MakeButton(dlgPanel.transform, "NextButton", "NEXT >", 26, btnCol, Color.white,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-25f, 18f), new Vector2(190f, 75f), out _);
            var choiceA = BuilderLib.MakeButton(dlgPanel.transform, "ChoiceA", "", 24, btnCol, Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 18f), new Vector2(660f, 80f), out var choiceALabel);
            var choiceB = BuilderLib.MakeButton(dlgPanel.transform, "ChoiceB", "", 24, btnDark, Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(12f, 18f), new Vector2(660f, 80f), out var choiceBLabel);
            dlgPanel.gameObject.SetActive(false);

            refs.dialogueUi = canvasGo.AddComponent<DialogueUI>();
            refs.dialogueUi.root = dlgPanel.gameObject;
            refs.dialogueUi.speakerText = speaker;
            refs.dialogueUi.bodyText = dlgBody;
            refs.dialogueUi.nextButton = nextBtn;
            refs.dialogueUi.choiceAButton = choiceA;
            refs.dialogueUi.choiceBButton = choiceB;
            refs.dialogueUi.choiceALabel = choiceALabel;
            refs.dialogueUi.choiceBLabel = choiceBLabel;

            // ---- End screen ----
            var endPanel = BuilderLib.Panel(ct, "EndPanel", new Color(0.03f, 0.03f, 0.05f, 0.97f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var endTitle = BuilderLib.MakeText(endPanel.transform, "Title", "", 56, accent, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1400f, 90f), bold: true);
            var endBody = BuilderLib.MakeText(endPanel.transform, "Body", "", 30, Color.white, TextAnchor.UpperCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1300f, 700f));
            endPanel.gameObject.SetActive(false);

            refs.hud = canvasGo.AddComponent<HUDController>();
            refs.hud.objectiveText = objective;
            refs.hud.hintText = hint;
            refs.hud.endRoot = endPanel.gameObject;
            refs.hud.endTitle = endTitle;
            refs.hud.endBody = endBody;

            // Panel button wiring that doesn't need the mission yet.
            UnityEventTools.AddPersistentListener(tabletClose.onClick, new UnityAction(refs.tablet.Close));
            UnityEventTools.AddPersistentListener(refs.tabletButton.onClick, new UnityAction(refs.tablet.Toggle));
            UnityEventTools.AddPersistentListener(scanBtn.onClick, new UnityAction(refs.scanner.OnScanClicked));
            UnityEventTools.AddPersistentListener(bagBtn.onClick, new UnityAction(refs.scanner.OnBagClicked));
            UnityEventTools.AddPersistentListener(backBtn.onClick, new UnityAction(refs.scanner.OnPutBackClicked));
            UnityEventTools.AddPersistentListener(largerBtn.onClick, new UnityAction(refs.bagFit.OnLargerBagClicked));
            UnityEventTools.AddPersistentListener(breakBtn.onClick, new UnityAction(refs.bagFit.OnBreakClicked));

            return refs;
        }

        // ----------------------------------------------------------------- wiring

        static void WireMission(MissionConfig config, PlayerController player, Camera cam,
                                Transform campCenter, Transform tarp, SampleSite[] sites, UiRefs ui)
        {
            BuildStoryDialogues(out var intro, out var bagCheck, out var bagFitLarger,
                                out var bagFitBreak, out var layout, out var load, out var outro);

            var missionGo = new GameObject("Mission");
            var runner = missionGo.AddComponent<DialogueRunner>();
            runner.ui = ui.dialogueUi;

            var m = missionGo.AddComponent<MissionManager>();
            m.config = config;
            m.player = player;
            m.camp = campCenter;
            m.sites = sites;
            m.layoutRoot = tarp;
            m.bagMaterial = _bagMat;
            m.hud = ui.hud;
            m.tablet = ui.tablet;
            m.scanner = ui.scanner;
            m.bagFitPanel = ui.bagFit;
            m.dialogue = runner;
            m.introDialogue = intro;
            m.bagCheckDialogue = bagCheck;
            m.bagFitLargerDialogue = bagFitLarger;
            m.bagFitBreakDialogue = bagFitBreak;
            m.layoutDialogue = layout;
            m.loadDialogue = load;
            m.outroDialogue = outro;

            player.joystickBase = ui.joyBase;
            player.joystickKnob = ui.joyKnob;
            player.interactor.interactRange = config.interactRange;

            // Dialogue + send-button wiring now that the runner/mission exist.
            UnityEventTools.AddPersistentListener(ui.dialogueUi.nextButton.onClick, new UnityAction(runner.OnNextClicked));
            UnityEventTools.AddBoolPersistentListener(ui.dialogueUi.choiceAButton.onClick, runner.OnChoiceClicked, true);
            UnityEventTools.AddBoolPersistentListener(ui.dialogueUi.choiceBButton.onClick, runner.OnChoiceClicked, false);
            UnityEventTools.AddPersistentListener(ui.tablet.sendButton.onClick, new UnityAction(m.OnSendConfirmation));
        }
    }
}

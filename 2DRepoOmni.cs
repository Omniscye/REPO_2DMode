// SPDX-License-Identifier: MIT
// REPO: "Flat2D — toggleable 2D mode (Enemies, Valuables, Player Avatars)"
// Author: Omniscye (2D-only; avatars enforced each frame; strict rig matching)

using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BepInEx;

namespace REPO.Flat2D
{
    public static class Entry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Bootstrap() => Flat2DMod.Init();
    }

    [HarmonyPatch]
    public static class Flat2DMod
    {
        private static Harmony _harmony;
        private static bool _patched;

        public static void Init()
        {
            if (_patched) return;
            _patched = true;
            _harmony = new Harmony("repo.flat2d.toggle");
            _harmony.PatchAll(typeof(Flat2DMod).Assembly);
            Debug.Log("[Flat2D] Harmony patches applied.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RenderTextureMain), "Awake")]
        private static void RenderTextureMain_Awake_Postfix(RenderTextureMain __instance)
        {
            if (!__instance) return;
            if (!__instance.gameObject.TryGetComponent(out Flat2DController _))
                __instance.gameObject.AddComponent<Flat2DController>();
        }
    }

    public sealed class Flat2DController : MonoBehaviour
    {
        public bool ModEnabled = false; // start OFF - wacking off bruh
        [Range(0.0001f, 0.2f)] public float Depth = 0.01f; // Z for enemies/valuables/items, X for avatars and b is for bitches
        [Range(0.0001f, 2f)] public float FlashlightY = 0.1f; // enforced Y for flashlight mesh because we needs it precious

        private Coroutine _avatarLoop;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                ModEnabled = !ModEnabled;
                if (ModEnabled)
                {
                    StopAllCoroutines();
                    StartCoroutine(ApplyFlatToExistingDelayed(0.15f)); // ONE-TIME sweep
                    _avatarLoop = StartCoroutine(AvatarKeepFlatLoop()); // cheap per-avatar upkeep or we get stalls
                    Debug.Log("[Flat2D] ENABLED");
                }
                else
                {
                    if (_avatarLoop != null) { StopCoroutine(_avatarLoop); _avatarLoop = null; }

                    AvatarFlatUtil.DetachAllFlattener();
                    FlashlightSceneUtil.DetachAllEnforcers(); // restore scene flashlight meshes
                    RestoreAllFlatified(); // enemies/valuables/items/moms
                    Debug.Log("[Flat2D] DISABLED");
                }
            }
        }

        // Only per-avatar (cheap), no scene-wide loops or we get fucked
        private IEnumerator AvatarKeepFlatLoop()
        {
            var wait = new WaitForSecondsRealtime(0.75f);
            while (ModEnabled)
            {
                var avatars = UnityEngine.Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);
                for (int i = 0; i < avatars.Length; i++)
                {
                    var p = avatars[i];
                    if (!p || !p.gameObject.activeInHierarchy) continue;
                    AvatarFlatUtil.AttachFlattener(p, Depth);
                    AvatarFlatUtil.AttachFlashlightScaler(p, FlashlightY);
                }
                yield return wait;
            }
        }

        private static bool NameIsWhitelistedItem(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            // Must start with "Item " (space), and must NOT contain "Cart"
            bool startsItem = n.StartsWith("Item ", System.StringComparison.OrdinalIgnoreCase);
            bool hasCart = n.IndexOf("Cart", System.StringComparison.OrdinalIgnoreCase) >= 0;
            return startsItem && !hasCart;
        }

        private void RestoreAllFlatified()
        {
            var markers = UnityEngine.Object.FindObjectsByType<_FlatMarker>(FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
                FlatUtil.Restore(markers[i].gameObject);
        }

        private IEnumerator ApplyFlatToExistingDelayed(float extraDelaySeconds)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForFixedUpdate();
            if (extraDelaySeconds > 0f) yield return new WaitForSecondsRealtime(extraDelaySeconds);
            if (!ModEnabled) yield break;

            // Enemies (Z squash)
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyParent>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] && enemies[i].gameObject.activeInHierarchy)
                    FlatUtil.TryFlatify(enemies[i].gameObject, Depth);

            // Valuables (Z squash)
            var vals = UnityEngine.Object.FindObjectsByType<ValuableObject>(FindObjectsSortMode.None);
            for (int i = 0; i < vals.Length; i++)
                if (vals[i] && vals[i].gameObject.activeInHierarchy)
                    FlatUtil.TryFlatify(vals[i].gameObject, Depth);

            // Items whitelist (Z squash) — one-time pass on toggle
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!t || !t.gameObject.activeInHierarchy) continue;

                var n = t.name;
                if (!NameIsWhitelistedItem(n)) continue;

                if (t.GetComponent<_FlatMarker>() != null) continue; // already processed well should be
                FlatUtil.TryFlatify(t.gameObject, Depth);
            }

            // Avatars (attach enforcers + flashlight scaler)
            var avatars = UnityEngine.Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);
            for (int i = 0; i < avatars.Length; i++)
            {
                var p = avatars[i];
                if (!p || !p.gameObject.activeInHierarchy) continue;
                AvatarFlatUtil.AttachFlattener(p, Depth);
                AvatarFlatUtil.AttachFlashlightScaler(p, FlashlightY);
            }

            // ONE initial scene flashlight sweep (covers remotes w/ different hierarchy)
            FlashlightSceneUtil.SweepAndEnforce(FlashlightY);
        }

        private void OnGUI()
        {
            string txt = ModEnabled ? "2D Mode: ON (F8 to toggle)" : "2D Mode: OFF (F8 to toggle)";
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.black } };
            var r = new Rect(10, 10, 400, 30);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), txt, style);
            style.normal.textColor = Color.green;
            GUI.Label(r, txt, style);
        }
    }

    // ====== Flatten helpers (enemies/valuables/items) ======
    sealed class _FlatMarker : MonoBehaviour
    {
        public Dictionary<Transform, Vector3> original = new();
    }

    static class FlatUtil
    {
        public static void TryFlatify(GameObject root, float depth)
        {
            if (!root) return;
            var marker = root.GetComponent<_FlatMarker>();
            if (!marker) marker = root.AddComponent<_FlatMarker>();

            var rends = root.GetComponentsInChildren<Renderer>(true);
            var newZ = Mathf.Max(depth, 0.0001f);

            for (int i = 0; i < rends.Length; i++)
            {
                var t = rends[i].transform;
                if (!marker.original.ContainsKey(t))
                    marker.original[t] = t.localScale;
                var s = t.localScale;
                if (s.z != newZ)
                    t.localScale = new Vector3(s.x, s.y, newZ); // squash Z
            }
        }

        public static void Restore(GameObject root)
        {
            var marker = root ? root.GetComponent<_FlatMarker>() : null;
            if (!marker) return;
            foreach (var kv in marker.original)
                if (kv.Key) kv.Key.localScale = kv.Value;
            UnityEngine.Object.Destroy(marker);
        }
    }

    // ====== Avatar & Flashlight flattener components ======

    /// Enforce X-scale on a single target (the rig/root). Captures original BEFORE modification.
    public sealed class AvatarRigFlattener : MonoBehaviour
    {
        public Transform Target;      // [RIG] or visuals root
        public float XDepth = 0.01f;

        private Vector3 _origScale;
        private bool _hasOrig;

        // Configure captures the original BEFORE caller changes scale OR THEY FUCK RIGHT OFF FFS
        public void Configure(Transform target, float xDepth)
        {
            Target = target;
            XDepth = xDepth;
            if (Target && !_hasOrig)
            {
                _origScale = Target.localScale;
                _hasOrig = true;
            }
            enabled = true;
        }

        private void LateUpdate()
        {
            if (!Target) { enabled = false; return; }
            var s = Target.localScale;
            float want = Mathf.Max(XDepth, 0.0001f);
            if (Mathf.Abs(s.x - want) > 0.00001f)
                Target.localScale = new Vector3(want, s.y, s.z);
        }

        private void OnDisable() { if (Target && _hasOrig) Target.localScale = _origScale; }
        private void OnDestroy() { if (Target && _hasOrig) Target.localScale = _origScale; }
    }

    /// Enforce Y-scale on MANY flashlight meshes under an avatar (restores when removed/disabled).
    public sealed class FlashlightMultiScaler : MonoBehaviour
    {
        public List<Transform> Targets = new List<Transform>();
        public float YScale = 0.1f;

        private readonly List<(Transform t, Vector3 orig)> _orig = new List<(Transform, Vector3)>();
        private bool _captured;

        public void Configure(IList<Transform> targets, float yScale)
        {
            Targets.Clear();
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    if (t) Targets.Add(t);
                }
            }
            YScale = yScale;

            if (!_captured)
            {
                _orig.Clear();
                for (int i = 0; i < Targets.Count; i++)
                {
                    var t = Targets[i];
                    _orig.Add((t, t.localScale));
                }
                _captured = true;
            }
            enabled = true;
        }

        private void LateUpdate()
        {
            float want = Mathf.Max(YScale, 0.0001f);
            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (!t) continue;
                var s = t.localScale;
                if (Mathf.Abs(s.y - want) > 0.00001f)
                    t.localScale = new Vector3(s.x, want, s.z);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _orig.Count; i++)
            {
                var p = _orig[i];
                if (p.t) p.t.localScale = p.orig;
            }
        }

        private void OnDestroy() => OnDisable();
    }

    /// Scene-wide Y enforcer for a single flashlight Mesh (attach directly to Mesh GO).
    public sealed class FlashlightYEnforcer : MonoBehaviour
    {
        public float Y = 0.1f;

        private Vector3 _orig;
        private bool _has;

        private void OnEnable()
        {
            _orig = transform.localScale;
            _has = true;
        }

        private void LateUpdate()
        {
            float want = Mathf.Max(Y, 0.0001f);
            var s = transform.localScale;
            if (Mathf.Abs(s.y - want) > 0.00001f)
                transform.localScale = new Vector3(s.x, want, s.z);
        }

        private void OnDisable() { if (_has) transform.localScale = _orig; }
        private void OnDestroy() { if (_has) transform.localScale = _orig; }
    }

    /// Fallback: enforce X-scale on EVERY Renderer transform under the avatar.
    public sealed class AvatarPerRendererFlattener : MonoBehaviour
    {
        public float XDepth = 0.01f;
        private readonly List<(Transform t, Vector3 orig)> _targets = new();

        private void OnEnable()
        {
            _targets.Clear();
            var rends = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var t = rends[i].transform;
                _targets.Add((t, t.localScale));
            }
        }

        private void LateUpdate()
        {
            float want = Mathf.Max(XDepth, 0.0001f);
            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i].t;
                if (!t) continue;
                var s = t.localScale;
                if (Mathf.Abs(s.x - want) > 0.00001f)
                    t.localScale = new Vector3(want, s.y, s.z);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                var p = _targets[i];
                if (p.t) p.t.localScale = p.orig;
            }
            _targets.Clear();
        }

        private void OnDestroy() => OnDisable();
    }

    // ====== STRICT Avatar util + per-avatar flashlight - Actually need for we get FUCKED ======
    static class AvatarFlatUtil
    {
        // exact names we accept
        private static readonly string[] VISUALS = { "Player Visuals", "PlayerVisuals" };
        private static readonly string[] RIG = { "[RIG]", "RIG" };

        private static readonly string[] LOCAL_CAMERA = { "Local Camera" };
        private static readonly string[] FLASHLIGHT_TARGET = { "Flashlight Target", "FlashlightTarget" };
        private static readonly string[] FLASHLIGHT = { "Flashlight" };
        private static readonly string[] MESH = { "Mesh" };

        // caches per avatar
        private static readonly Dictionary<PlayerAvatar, Transform> _cachedTarget = new();
        private static readonly Dictionary<PlayerAvatar, List<Transform>> _cachedFlashlightMeshes = new();

        public static void AttachFlattener(PlayerAvatar avatar, float xDepth)
        {
            if (!avatar) return;

            // Fast path via cached rig target
            if (_cachedTarget.TryGetValue(avatar, out var cached) && cached)
            {
                EnsureEnforcers(avatar, cached, xDepth);
                return;
            }

            // If a rig enforcer already exists and has a target, prefer that because we needs.
            var existingRig = avatar.GetComponent<AvatarRigFlattener>();
            if (existingRig && existingRig.Target)
            {
                existingRig.Configure(existingRig.Target, xDepth);

                var existingPer = avatar.GetComponent<AvatarPerRendererFlattener>();
                if (existingPer) { existingPer.XDepth = Mathf.Max(xDepth, 0.0001f); existingPer.enabled = true; }

                // one-shot apply AFTER capturing original
                var t0 = existingRig.Target;
                var s0 = t0.localScale;
                float want0 = Mathf.Max(xDepth, 0.0001f);
                if (Mathf.Abs(s0.x - want0) > 0.00001f)
                    t0.localScale = new Vector3(want0, s0.y, s0.z);

                _cachedTarget[avatar] = t0;
                return;
            }

            // Resolve normally
            var (visuals, rig, searchRoot) = ResolveAvatarRigStrict(avatar.transform);
            var target = rig ?? visuals ?? avatar.transform;

            Debug.Log($"[Flat2D] Avatar STRICT target -> searchRoot: {searchRoot.name}, visuals: {(visuals ? visuals.name : "<null>")}, rig: {(rig ? rig.name : "<null>")}, use: {target.name}");

            _cachedTarget[avatar] = target;
            EnsureEnforcers(avatar, target, xDepth);
        }

        public static void AttachFlashlightScaler(PlayerAvatar avatar, float yScale)
        {
            if (!avatar) return;

            // cached
            if (!_cachedFlashlightMeshes.TryGetValue(avatar, out var meshes) || meshes == null || meshes.Count == 0 || meshes.Any(m => !m))
            {
                meshes = ResolveAllFlashlightMeshes(avatar.transform);
                if (meshes.Count > 0)
                    _cachedFlashlightMeshes[avatar] = meshes;
            }
            if (meshes == null || meshes.Count == 0) return;

            // attach/enforce
            var scaler = avatar.GetComponent<FlashlightMultiScaler>() ?? avatar.gameObject.AddComponent<FlashlightMultiScaler>();
            scaler.Configure(meshes, yScale);

            // one-shot apply AFTER capture
            float want = Mathf.Max(yScale, 0.0001f);
            for (int i = 0; i < meshes.Count; i++)
            {
                var m = meshes[i];
                if (!m) continue;
                var s = m.localScale;
                if (Mathf.Abs(s.y - want) > 0.00001f)
                    m.localScale = new Vector3(s.x, want, s.z);
            }
        }

        private static void EnsureEnforcers(PlayerAvatar avatar, Transform target, float xDepth)
        {
            float want = Mathf.Max(xDepth, 0.0001f);

            // rig/root enforcer (capture BEFORE we change scale)
            var rigEnforcer = avatar.GetComponent<AvatarRigFlattener>() ??
                              avatar.gameObject.AddComponent<AvatarRigFlattener>();
            rigEnforcer.Configure(target, want);

            // now apply one-shot
            var s = target.localScale;
            if (Mathf.Abs(s.x - want) > 0.00001f)
                target.localScale = new Vector3(want, s.y, s.z);

            // Also enforce per-renderer (covers any scripts that resync rig scale and shit)
            var perRenderer = avatar.GetComponent<AvatarPerRendererFlattener>() ??
                              avatar.gameObject.AddComponent<AvatarPerRendererFlattener>();
            perRenderer.XDepth = want;
            perRenderer.enabled = true;
        }

        public static void DetachAllFlattener()
        {
            foreach (var f in UnityEngine.Object.FindObjectsByType<AvatarRigFlattener>(FindObjectsSortMode.None))
            { if (!f) continue; f.enabled = false; UnityEngine.Object.Destroy(f); }

            foreach (var f in UnityEngine.Object.FindObjectsByType<AvatarPerRendererFlattener>(FindObjectsSortMode.None))
            { if (!f) continue; f.enabled = false; UnityEngine.Object.Destroy(f); }

            foreach (var f in UnityEngine.Object.FindObjectsByType<FlashlightMultiScaler>(FindObjectsSortMode.None))
            { if (!f) continue; f.enabled = false; UnityEngine.Object.Destroy(f); }

            _cachedTarget.Clear();
            _cachedFlashlightMeshes.Clear();
        }

        // --------- STRICT path: Player Visuals -> [RIG] only -> Fucking CHRIST ----------
        private static (Transform visuals, Transform rig, Transform searchRoot) ResolveAvatarRigStrict(Transform avatarNode)
        {
            // climb to the NEAREST ancestor whose subtree actually contains "Player Visuals"
            Transform searchRoot = FindNearestAncestorWhoseSubtreeHas(avatarNode, VISUALS) ?? avatarNode;

            // 1) find "Player Visuals" under searchRoot
            Transform visuals = FindExactBFS(searchRoot, VISUALS);
            if (!visuals)
            {
                // Silent fallback (no spam now): use SkinnedMesh common ancestor
                return (null, FallbackSkinsCommonAncestor(avatarNode, avatarNode), searchRoot);
            }

            // 2) under visuals, find "[RIG]"
            Transform rig = FindExactBFS(visuals, RIG);
            if (!rig)
            {
                Debug.LogWarning("[Flat2D] [RIG] not found under Player Visuals; using SkinnedMesh common ancestor as rig.");
                rig = FallbackSkinsCommonAncestor(avatarNode, visuals);
            }

            return (visuals, rig, searchRoot);
        }

        // Resolve ALL flashlight meshes under an avatar.
        private static List<Transform> ResolveAllFlashlightMeshes(Transform avatarNode)
        {
            var result = new List<Transform>();

            // Try the Local Camera canonical path first
            Transform searchRoot = FindNearestAncestorWhoseSubtreeHas(avatarNode, LOCAL_CAMERA) ?? avatarNode;
            Transform localCam = FindExactBFS(searchRoot, LOCAL_CAMERA);
            if (localCam)
            {
                foreach (Transform flt in FindAllExactBFS(localCam, FLASHLIGHT_TARGET))
                {
                    foreach (Transform fl in FindAllExactBFS(flt, FLASHLIGHT))
                    {
                        foreach (Transform mesh in FindAllExactBFS(fl, MESH))
                            if (mesh && !result.Contains(mesh)) result.Add(mesh);
                    }
                }
            }

            // If none found, do a broad scan under the avatar for any "Flashlight" -> "Mesh"
            if (result.Count == 0)
            {
                foreach (Transform fl in FindAllExactBFS(avatarNode, FLASHLIGHT))
                {
                    foreach (Transform mesh in FindAllExactBFS(fl, MESH))
                        if (mesh && !result.Contains(mesh)) result.Add(mesh);
                }
            }

            return result;
        }

        // Breadth-first exact-name search (first match)
        private static Transform FindExactBFS(Transform root, params string[] names)
        {
            if (!root) return null;
            var set = new HashSet<string>(names.Select(n => n.Trim()), System.StringComparer.OrdinalIgnoreCase);

            foreach (Transform c in root)
                if (set.Contains(c.name)) return c;

            var q = new Queue<Transform>();
            q.Enqueue(root);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (Transform c in cur)
                {
                    if (set.Contains(c.name)) return c;
                    q.Enqueue(c);
                }
            }
            return null;
        }

        // Breadth-first exact-name search (all matches)
        private static IEnumerable<Transform> FindAllExactBFS(Transform root, params string[] names)
        {
            if (!root) yield break;
            var set = new HashSet<string>(names.Select(n => n.Trim()), System.StringComparer.OrdinalIgnoreCase);

            var q = new Queue<Transform>();
            q.Enqueue(root);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (Transform c in cur)
                {
                    if (set.Contains(c.name)) yield return c;
                    q.Enqueue(c);
                }
            }
        }

        // climbs parents; at each step, checks if that ancestor’s subtree has any of the names
        private static Transform FindNearestAncestorWhoseSubtreeHas(Transform start, params string[] names)
        {
            for (Transform t = start; t != null; t = t.parent)
                if (FindExactBFS(t, names)) return t;
            return null;
        }

        // Common ancestor of all SkinnedMeshRenderers (fallback if [RIG] missing)
        private static Transform FallbackSkinsCommonAncestor(Transform avatarRoot, Transform fallback)
        {
            var skins = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0) return fallback;

            // Build ancestor chains
            List<List<Transform>> chains = new();
            foreach (var s in skins)
            {
                var list = new List<Transform>();
                for (var t = s.transform; t != null; t = t.parent) list.Add(t);
                chains.Add(list);
            }

            // Pick the first transform that’s present in all chains
            foreach (var candidate in chains[0])
            {
                bool inAll = true;
                for (int i = 1; i < chains.Count; i++)
                {
                    if (!chains[i].Contains(candidate)) { inAll = false; break; }
                }
                if (inAll) return candidate;
            }
            return fallback;
        }
    }

    // ====== Scene-wide flashlight helpers (single sweep on enable only) ======
    static class FlashlightSceneUtil
    {
        private static readonly string[] FLASHLIGHT = { "Flashlight" };
        private static readonly string[] FLASHLIGHT_TARGET = { "Flashlight Target", "FlashlightTarget" };
        private static readonly string[] MESH = { "Mesh" };

        private static readonly HashSet<Transform> _tracked = new HashSet<Transform>();

        public static void SweepAndEnforce(float yScale)
        {
            // 1) Path: * -> Flashlight Target -> Flashlight -> Mesh
            foreach (var flt in FindAllExactBFS_Scene(FLASHLIGHT_TARGET))
            {
                foreach (Transform fl in FindAllExactBFS(flt, FLASHLIGHT))
                {
                    foreach (Transform mesh in FindAllExactBFS(fl, MESH))
                        Attach(mesh, yScale);
                }
            }

            // 2) Fallback: * -> Flashlight -> Mesh (covers remotes or custom rigs)
            foreach (var fl in FindAllExactBFS_Scene(FLASHLIGHT))
            {
                foreach (Transform mesh in FindAllExactBFS(fl, MESH))
                    Attach(mesh, yScale);
            }
        }

        private static void Attach(Transform mesh, float yScale)
        {
            if (!mesh) return;
            if (!_tracked.Contains(mesh))
                _tracked.Add(mesh);

            var en = mesh.GetComponent<FlashlightYEnforcer>();
            if (!en) en = mesh.gameObject.AddComponent<FlashlightYEnforcer>();
            en.Y = yScale;
            en.enabled = true;
        }

        public static void DetachAllEnforcers()
        {
            foreach (var t in _tracked)
            {
                if (!t) continue;
                var en = t.GetComponent<FlashlightYEnforcer>();
                if (en) { en.enabled = false; UnityEngine.Object.Destroy(en); }
            }
            _tracked.Clear();
        }

        // scene BFS helpers (used once on enable) SHOULD BE ONCE FFS
        private static IEnumerable<Transform> FindAllExactBFS_Scene(params string[] names)
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var tr = all[i];
                if (!tr || !tr.gameObject.activeInHierarchy) continue;
                foreach (var m in FindAllExactBFS(tr, names))
                    yield return m;
            }
        }

        private static IEnumerable<Transform> FindAllExactBFS(Transform root, params string[] names)
        {
            if (!root) yield break;
            var set = new HashSet<string>(names.Select(n => n.Trim()), System.StringComparer.OrdinalIgnoreCase);

            var q = new Queue<Transform>();
            q.Enqueue(root);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (Transform c in cur)
                {
                    if (set.Contains(c.name)) yield return c;
                    q.Enqueue(c);
                }
            }
        }
    }

    // ====== Spawn hookers (helpers) ======
    [HarmonyPatch(typeof(EnemyParent), "SpawnRPC")]
    static class Patch_FlatEnemies_OnSpawn
    {
        static void Postfix(EnemyParent __instance)
        {
            var ctrl = UnityEngine.Object.FindFirstObjectByType<Flat2DController>();
            if (!__instance || ctrl == null || !ctrl.ModEnabled) return;
            __instance.StartCoroutine(WaitThenFlat(__instance.gameObject, ctrl.Depth, 0.15f));
        }

        private static IEnumerator WaitThenFlat(GameObject go, float depth, float delay)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForFixedUpdate();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (!go) yield break;
            FlatUtil.TryFlatify(go, depth);
        }
    }

    [HarmonyPatch(typeof(ValuableObject), "Start")]
    static class Patch_FlatValuables_OnStart
    {
        static void Postfix(ValuableObject __instance)
        {
            var ctrl = UnityEngine.Object.FindFirstObjectByType<Flat2DController>();
            if (!__instance || ctrl == null || !ctrl.ModEnabled) return;
            __instance.StartCoroutine(WaitThenFlat(__instance.gameObject, ctrl.Depth, 0.10f));
        }

        private static IEnumerator WaitThenFlat(GameObject go, float depth, float delay)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForFixedUpdate();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (!go) yield break;
            FlatUtil.TryFlatify(go, depth);
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "Start")]
    static class Patch_FlatPlayerAvatar_OnStart
    {
        static void Postfix(PlayerAvatar __instance)
        {
            var ctrl = UnityEngine.Object.FindFirstObjectByType<Flat2DController>();
            if (!__instance || ctrl == null || !ctrl.ModEnabled) return;
            AvatarFlatUtil.AttachFlattener(__instance, ctrl.Depth);
            AvatarFlatUtil.AttachFlashlightScaler(__instance, ctrl.FlashlightY);
        }
    }
}
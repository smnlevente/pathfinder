using Pathfinder.Game.Content;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Run;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        /// <summary>Level objects for the run structure (GDD §10).</summary>
        sealed class RunArt
        {
            public GameObject Exit, DarkMatterNode, RepairStation, ShopTerminal, Cache;
        }

        static RunArt BuildRunArt(Art art)
        {
            var dot = Load<Texture2D>(TexturesDir + "/T_SoftDot.png");
            var ring = Load<Texture2D>(TexturesDir + "/T_Ring.png");

            // Colours (GDD §12.4): Dark Matter is indigo, clearly bluer than the magenta enemy bullets. Repair is green,
            // the shop gold, and the wormhole and equipment use the player's cyan tech.
            var darkMatter = Unlit("M_DarkMatter", new Color(0.35f, 0.45f, 2.8f));
            var darkMatterGlow = Additive("M_FX_DarkMatter", new Color(0.25f, 0.25f, 2f), dot);
            var darkMatterRing = Additive("M_FX_DarkMatterRing", new Color(0.45f, 0.5f, 2.8f), ring);
            var repair = Unlit("M_RepairGlow", new Color(0.4f, 3f, 0.9f));
            var repairGlow = Additive("M_FX_Green", new Color(0.25f, 1.6f, 0.5f), dot);
            var gold = Unlit("M_ShopGlow", new Color(3f, 2.2f, 0.5f));
            var goldGlow = Additive("M_FX_Gold", new Color(1.6f, 1.1f, 0.25f), dot);
            var exitRing = Additive("M_FX_WormholeRing", new Color(0.3f, 1.4f, 2.4f), ring);
            var pad = Lit("M_StationPad", new Color(0.16f, 0.18f, 0.22f), smoothness: 0.6f, metallic: 0.5f);

            var run = new RunArt();

            // Exit wormhole: two counter-rotating rings over a soft glow.
            var exit = new GameObject("FX_ExitWormhole");
            FlatQuad("Glow", exit.transform, art.FxCyan, 0.04f, 4.5f);
            var outer = FlatQuad("RingOuter", exit.transform, exitRing, 0.06f, 3.6f);
            SetFields(outer.AddComponent<SimpleMotion>(), ("spinSpeed", 0f), ("pulseAmount", 0.06f), ("pulseFrequency", 1.2f));
            var spinA = new GameObject("SpinA").transform;
            spinA.SetParent(exit.transform, false);
            SetFields(spinA.gameObject.AddComponent<SimpleMotion>(), ("spinSpeed", 120f));
            FlatQuad("Ring", spinA, exitRing, 0.08f, 2.6f);
            var spinB = new GameObject("SpinB").transform;
            spinB.SetParent(exit.transform, false);
            SetFields(spinB.gameObject.AddComponent<SimpleMotion>(), ("spinSpeed", -200f));
            FlatQuad("Ring", spinB, exitRing, 0.1f, 1.7f);
            run.Exit = SavePrefab(exit, "Run");

            // Dark Matter node: a spinning indigo crystal. A ring grows under it as it is harvested.
            var node = new GameObject("PV_DarkMatterNode");
            FlatQuad("Glow", node.transform, darkMatterGlow, 0.03f, 2.4f);
            var crystalPivot = new GameObject("Crystal").transform;
            crystalPivot.SetParent(node.transform, false);
            crystalPivot.localPosition = new Vector3(0f, 0.7f, 0f);
            SetFields(crystalPivot.gameObject.AddComponent<SimpleMotion>(), ("spinSpeed", 90f));
            Primitive(PrimitiveType.Cube, "Shard", crystalPivot, darkMatter, Vector3.zero, new Vector3(0.45f, 0.8f, 0.45f), Quaternion.Euler(0f, 45f, 25f));
            var harvestRing = FlatQuad("HarvestRing", node.transform, darkMatterRing, 0.05f, 3.4f);
            SetFields(node.AddComponent<ObjectiveMarker>(), ("progressIndicator", harvestRing.transform));
            run.DarkMatterNode = SavePrefab(node, "Run");

            // Repair station: a pad with a glowing green cross. The cross dims once the station is used.
            var station = new GameObject("PV_RepairStation");
            FlatQuad("Glow", station.transform, repairGlow, 0.03f, 4.6f);
            Primitive(PrimitiveType.Cylinder, "Pad", station.transform, pad, new Vector3(0f, 0.03f, 0f), new Vector3(2.6f, 0.03f, 2.6f));
            var crossA = Primitive(PrimitiveType.Cube, "CrossA", station.transform, repair, new Vector3(0f, 0.1f, 0f), new Vector3(1.3f, 0.08f, 0.32f));
            var crossB = Primitive(PrimitiveType.Cube, "CrossB", station.transform, repair, new Vector3(0f, 0.1f, 0f), new Vector3(0.32f, 0.08f, 1.3f));
            SetFields(station.AddComponent<ObjectiveMarker>(), ("dimWhenSpent", new Object[] { crossA.GetComponent<Renderer>(), crossB.GetComponent<Renderer>() }));
            run.RepairStation = SavePrefab(station, "Run");

            // Shop terminal: a pad with a pillar and a gold screen.
            var shop = new GameObject("PV_ShopTerminal");
            FlatQuad("Glow", shop.transform, goldGlow, 0.03f, 4.6f);
            Primitive(PrimitiveType.Cylinder, "Pad", shop.transform, pad, new Vector3(0f, 0.03f, 0f), new Vector3(2.6f, 0.03f, 2.6f));
            Primitive(PrimitiveType.Cube, "Pillar", shop.transform, art.ShipHull, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 1.2f, 0.5f));
            Primitive(PrimitiveType.Cube, "Screen", shop.transform, gold, new Vector3(0f, 1f, -0.27f), new Vector3(0.42f, 0.3f, 0.04f));
            run.ShopTerminal = SavePrefab(shop, "Run");

            // Equipment cache: a cyan-trimmed crate (player tech), bobbing and turning.
            var cache = new GameObject("PV_EquipmentCache");
            FlatQuad("Glow", cache.transform, art.FxCyan, 0.03f, 2.6f);
            var cratePivot = new GameObject("Crate").transform;
            cratePivot.SetParent(cache.transform, false);
            cratePivot.localPosition = new Vector3(0f, 0.5f, 0f);
            SetFields(cratePivot.gameObject.AddComponent<SimpleMotion>(), ("spinSpeed", 60f));
            Primitive(PrimitiveType.Cube, "Box", cratePivot, art.ShipHull, Vector3.zero, Vector3.one * 0.7f);
            Primitive(PrimitiveType.Cube, "BandX", cratePivot, art.ShipGlow, Vector3.zero, new Vector3(0.74f, 0.12f, 0.74f));
            Primitive(PrimitiveType.Cube, "BandY", cratePivot, art.ShipGlow, Vector3.zero, new Vector3(0.12f, 0.74f, 0.74f));
            run.Cache = SavePrefab(cache, "Run");

            return run;
        }

        static RunConfig BuildRunConfig(Data data, RunArt runArt)
        {
            var config = Definition<RunConfig>("Run/RunConfig", "run.config", "Run Config", so =>
            {
                Set(so, ("CacheCarrier", data.Gunner), ("CacheCarrierElite", data.Armored));
                var boss = so.FindProperty("<Boss>k__BackingField");
                boss.FindPropertyRelative("Enemy").objectReferenceValue = data.Carrier;
                boss.FindPropertyRelative("Elite").objectReferenceValue = data.Armored;
                boss.FindPropertyRelative("HealthMultiplier").floatValue = 3f;
                boss.FindPropertyRelative("Escort").objectReferenceValue = data.Gunner;
                boss.FindPropertyRelative("EscortCount").intValue = 2;
            });

            SetIfEmpty(config, ("ExitPrefab", runArt.Exit), ("DarkMatterNodePrefab", runArt.DarkMatterNode),
                ("RepairStationPrefab", runArt.RepairStation), ("ShopTerminalPrefab", runArt.ShopTerminal), ("CachePrefab", runArt.Cache));
            return config;
        }
    }
}

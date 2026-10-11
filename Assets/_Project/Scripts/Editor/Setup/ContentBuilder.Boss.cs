using Pathfinder.Core.Equipment;
using Pathfinder.Core.Meta;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Run;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        /// <summary>The Quarry Maw (GDD §7.5): definition, head and segment prefabs, and its eruption telegraph.</summary>
        static EnemyDefinition BuildQuarryMaw(Art art)
        {
            var definition = Definition<EnemyDefinition>("Enemies/Enemy_QuarryMaw", "boss.quarry-maw", "The Quarry Maw", so => Set(so,
                ("MaxHealth", 5500f), ("MoveSpeed", 4.5f), ("ContactDamage", 15f), ("BudgetCost", 20), ("ScrapsDrop", 30), ("ScrapsDropChance", 1f),
                ("KnockbackMultiplier", 0f)));

            // Eruption telegraph (playtest 7: the old glow and rings were small textures stretched over up to 20 u and read as
            // blurry): a crisp red outline at the blast radius drawn as geometry, a fill growing out to it and a ring closing in
            // to meet it; it flashes once the spot locks. The boss sets the radius and moves it with the ship.
            var impactRing = Additive("M_FX_ImpactRing", new Color(3f, 0.35f, 0.15f), Load<Texture2D>(TexturesDir + "/T_LineCore.png"));
            var impactFill = Additive("M_FX_ImpactFill", new Color(1.6f, 0.18f, 0.08f, 0.6f), Load<Texture2D>(TexturesDir + "/T_Disc.png"));
            var telegraph = new GameObject("FX_EruptTelegraph");
            var fill = FlatQuad("Fill", telegraph.transform, impactFill, 0.03f, 1f);
            var outline = FloorRing("Outline", telegraph.transform, impactRing, 0.12f, 0.05f);
            var closing = FloorRing("ClosingRing", telegraph.transform, impactRing, 0.16f, 0.06f);
            SetFields(telegraph.AddComponent<ImpactTelegraph>(), ("fill", fill.GetComponent<Renderer>()), ("outline", outline), ("closingRing", closing));
            var telegraphPrefab = SavePrefab(telegraph, "FX");

            // Eruption burst: a sharp red ring snapping out over the blast radius.
            var burst = new GameObject("FX_EruptBurst");
            var burstRing = FlatQuad("Ring", burst.transform, Additive("M_FX_EruptBurst", new Color(3f, 0.4f, 0.15f), Load<Texture2D>(TexturesDir + "/T_RingSharp.png")), 0.07f, 2f);
            SetFields(burst.AddComponent<RingPulse>(), ("ring", burstRing.GetComponent<Renderer>()), ("duration", 0.4f), ("startScale", 0.4f), ("endScale", 1.2f));
            var burstPrefab = SavePrefab(burst, "FX");

            // Segment: a craggy crimson rock chunk. Weak points carry a pulsing yellow crystal ringed in yellow, the only
            // place shots hurt (GDD §12.4: weak points are yellow).
            var segmentRoot = new GameObject("QuarryMawSegment");
            var segmentHalo = FlatQuad("Halo", segmentRoot.transform, art.EnemyHalo, 0.03f, 0.9f * 3.2f).GetComponent<Renderer>();
            var segmentModel = Model(segmentRoot.transform);
            Primitive(PrimitiveType.Sphere, "Rock", segmentModel, art.EnemyHull, Vector3.zero, new Vector3(1.7f, 1.1f, 1.6f));
            Primitive(PrimitiveType.Cube, "SpikeA", segmentModel, art.EnemyHull, new Vector3(0.3f, 0.45f, 0f), new Vector3(0.35f, 0.5f, 0.35f), Quaternion.Euler(20f, 45f, 15f));
            Primitive(PrimitiveType.Cube, "SpikeB", segmentModel, art.EnemyHull, new Vector3(-0.35f, 0.4f, 0.2f), new Vector3(0.3f, 0.45f, 0.3f), Quaternion.Euler(-15f, 30f, -20f));
            var weakPoint = new GameObject("WeakPoint");
            weakPoint.transform.SetParent(segmentModel, false);
            var crystal = Primitive(PrimitiveType.Sphere, "Crystal", weakPoint.transform, art.WeakPoint, new Vector3(0f, 0.55f, 0f), new Vector3(0.95f, 0.7f, 0.95f));
            SetFields(crystal.AddComponent<SimpleMotion>(), ("pulseAmount", 0.12f), ("pulseFrequency", 3f));
            FlatQuad("Ring", weakPoint.transform, art.WeakPointRing, 0.62f, 2.3f);
            weakPoint.SetActive(false);
            var segment = segmentRoot.AddComponent<BossSegment>();
            SetFields(segment, ("radius", 0.85f), ("shatterEffect", art.EnemyExplosion), ("weakPointMarker", weakPoint));
            SetFields(segmentRoot.AddComponent<SinkVisual>(), ("source", segment), ("model", segmentModel));
            var segmentVisual = AddVisual(segmentRoot, segment, null);
            SetFields(segmentVisual, ("flashExclusions", new Object[] { segmentHalo }));
            var segmentPrefab = SavePrefab(segmentRoot, "Enemies").GetComponent<BossSegment>();

            // Head: a huge armoured skull with a glowing maw and teeth that flare when it roars or winds up.
            var head = new GameObject("QuarryMaw");
            var headHalo = FlatQuad("Halo", head.transform, art.EnemyHalo, 0.03f, 1.3f * 3.2f).GetComponent<Renderer>();
            var headModel = Model(head.transform);
            Primitive(PrimitiveType.Sphere, "Skull", headModel, art.EnemyHull, Vector3.zero, new Vector3(2.3f, 1.3f, 2.7f));
            Primitive(PrimitiveType.Cube, "BrowL", headModel, art.EnemyHull, new Vector3(-0.6f, 0.55f, 0.6f), new Vector3(0.7f, 0.25f, 0.9f), Quaternion.Euler(0f, 20f, 10f));
            Primitive(PrimitiveType.Cube, "BrowR", headModel, art.EnemyHull, new Vector3(0.6f, 0.55f, 0.6f), new Vector3(0.7f, 0.25f, 0.9f), Quaternion.Euler(0f, -20f, -10f));
            var maw = Glow(Primitive(PrimitiveType.Cylinder, "Maw", headModel, art.EnemyGlow, new Vector3(0f, 0f, 1.25f), new Vector3(1.2f, 0.04f, 0.9f), Quaternion.Euler(90f, 0f, 0f)));
            var teeth = new Renderer[4];
            for (int i = 0; i < teeth.Length; i++)
            {
                float angle = i * 90f + 45f;
                Vector3 offset = Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, 0.55f, 0f);
                teeth[i] = Glow(Primitive(PrimitiveType.Cube, "Tooth" + i, headModel, art.EnemyGlow, new Vector3(offset.x, offset.y, 1.3f), new Vector3(0.18f, 0.18f, 0.3f), Quaternion.Euler(0f, 0f, angle)));
            }

            // Enraged, the skull cracks open: a large yellow crystal on top, ringed like the weak points, so it reads from
            // the top-down camera (GDD §12.4: yellow means shoot here).
            var exposedMaw = new GameObject("ExposedMaw");
            exposedMaw.transform.SetParent(headModel, false);
            var mawCrystal = Primitive(PrimitiveType.Sphere, "Crystal", exposedMaw.transform, art.WeakPoint, new Vector3(0f, 0.7f, 0.35f), new Vector3(1.25f, 0.8f, 1.25f));
            SetFields(mawCrystal.AddComponent<SimpleMotion>(), ("pulseAmount", 0.15f), ("pulseFrequency", 4f));
            FlatQuad("Ring", exposedMaw.transform, art.WeakPointRing, 0.75f, 3.2f).transform.localPosition = new Vector3(0f, 0.75f, 0.35f);
            exposedMaw.SetActive(false);

            var brain = head.AddComponent<QuarryMawBrain>();
            var dust = SavePrefab(Burst("FX_BurrowDust", art.FxGrey, count: 36, speed: 4.5f, size: 0.7f, lifetime: 0.9f), "FX");
            SetFields(head.AddComponent<SinkVisual>(), ("source", brain), ("model", headModel));
            SetFields(brain, ("segmentPrefab", segmentPrefab), ("eruptTelegraphPrefab", telegraphPrefab), ("eruptBurstPrefab", burstPrefab), ("exposedMawMarker", exposedMaw), ("burrowDustPrefab", dust),
                ("eruptEffect", art.EnemyExplosion), ("bulletPrefab", art.EnemyBulletVisual), ("bulletImpactPrefab", art.BulletImpact));
            var lane = ChargeLane("SweepLane", art, head.transform, size: 1.7f, spacing: 1.9f);
            SetFields(head.AddComponent<TelegraphLine>(), ("source", brain), ("lane", lane.GetComponent<ChevronLane>()));
            var glowParts = new Renderer[teeth.Length + 1];
            glowParts[0] = maw;
            teeth.CopyTo(glowParts, 1);
            var bossPrefab = FinishEnemyWithExclusions(head, definition, 1.3f, art, new[] { headHalo }, glowParts);

            SetIfEmpty(definition, ("Prefab", bossPrefab));
            return definition;
        }

        /// <summary>M5 replaced the placeholder boss (an elite Carrier) with the Quarry Maw. Older configs are upgraded once.</summary>
        static void UseQuarryMawAsBoss(RunConfig config, EnemyDefinition quarryMaw, Data data)
        {
            var so = new SerializedObject(config);
            var boss = so.FindProperty("<Boss>k__BackingField");
            var enemy = boss.FindPropertyRelative("Enemy");
            if (enemy.objectReferenceValue != null && enemy.objectReferenceValue != data.Carrier)
                return;

            enemy.objectReferenceValue = quarryMaw;
            boss.FindPropertyRelative("Elite").objectReferenceValue = null;
            boss.FindPropertyRelative("HealthMultiplier").floatValue = 1f;
            boss.FindPropertyRelative("EscortCount").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Hub upgrades (GDD §11.2): three Lab upgrades, one Core Project and a weapon unlock.</summary>
        static MetaUpgradeDefinition[] BuildMetaUpgrades(Data data)
        {
            MetaUpgradeDefinition Upgrade(string asset, string id, string name, MetaCurrency currency, int[] costs,
                MetaUpgradeDefinition.EffectKind effect, StatId stat = StatId.MaxHull, float amount = 0f, Object unlock = null) =>
                Definition<MetaUpgradeDefinition>($"Meta/{asset}", id, name, so =>
                {
                    Set(so, ("Currency", (int)currency), ("Effect", (int)effect), ("Stat", (int)stat), ("AmountPerTier", amount));
                    var costArray = so.FindProperty("<Costs>k__BackingField");
                    costArray.arraySize = costs.Length;
                    for (int i = 0; i < costs.Length; i++)
                        costArray.GetArrayElementAtIndex(i).intValue = costs[i];
                    so.FindProperty("<UnlockTarget>k__BackingField").objectReferenceValue = unlock;
                });

            return new[]
            {
                Upgrade("Meta_HullPlating", "meta.hull-plating", "Hull Plating", MetaCurrency.DarkMatter, new[] { 30, 60, 90, 120, 150 },
                    MetaUpgradeDefinition.EffectKind.StatBonus, StatId.MaxHull, 10f),
                Upgrade("Meta_ShieldMatrix", "meta.shield-matrix", "Shield Matrix", MetaCurrency.DarkMatter, new[] { 50, 100, 150 },
                    MetaUpgradeDefinition.EffectKind.StatBonus, StatId.MaxShield, 10f),
                Upgrade("Meta_SalvageProtocols", "meta.salvage-protocols", "Salvage Protocols", MetaCurrency.DarkMatter, new[] { 40, 80, 120 },
                    MetaUpgradeDefinition.EffectKind.StartingScraps, amount: 25f),
                Upgrade("Meta_SecondDash", "meta.second-dash", "Second Dash Charge", MetaCurrency.SingularityCores, new[] { 2 },
                    MetaUpgradeDefinition.EffectKind.StatBonus, StatId.DashCharges, 1f),
                Upgrade("Meta_UnlockQuantumCharger", "meta.unlock-quantum-charger", "Quantum Charger", MetaCurrency.DarkMatter, new[] { 150 },
                    MetaUpgradeDefinition.EffectKind.Unlock, unlock: data.QuantumCharger),
            };
        }
    }
}

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
                ("MaxHealth", 2000f), ("MoveSpeed", 6f), ("ContactDamage", 15f), ("BudgetCost", 20), ("ScrapsDrop", 30), ("ScrapsDropChance", 1f)));

            var telegraph = new GameObject("FX_EruptTelegraph");
            var glow = FlatQuad("Glow", telegraph.transform, art.FxRed, 0.03f, 6.6f);
            SetFields(glow.AddComponent<SimpleMotion>(), ("pulseAmount", 0.12f), ("pulseFrequency", 4f));
            var core = Primitive(PrimitiveType.Cylinder, "Core", telegraph.transform, art.Telegraph, new Vector3(0f, 0.02f, 0f), new Vector3(0.6f, 0.01f, 0.6f));
            SetFields(core.AddComponent<SimpleMotion>(), ("pulseAmount", 0.5f), ("pulseFrequency", 4f));
            var telegraphPrefab = SavePrefab(telegraph, "FX");

            // Segment: a craggy crimson rock chunk. Weak points carry a glowing yellow band (GDD §12.4: weak points are yellow).
            var segmentRoot = new GameObject("QuarryMawSegment");
            var segmentHalo = FlatQuad("Halo", segmentRoot.transform, art.EnemyHalo, 0.03f, 0.9f * 3.2f).GetComponent<Renderer>();
            var segmentModel = Model(segmentRoot.transform);
            Primitive(PrimitiveType.Sphere, "Rock", segmentModel, art.EnemyHull, Vector3.zero, new Vector3(1.7f, 1.1f, 1.6f));
            Primitive(PrimitiveType.Cube, "SpikeA", segmentModel, art.EnemyHull, new Vector3(0.3f, 0.45f, 0f), new Vector3(0.35f, 0.5f, 0.35f), Quaternion.Euler(20f, 45f, 15f));
            Primitive(PrimitiveType.Cube, "SpikeB", segmentModel, art.EnemyHull, new Vector3(-0.35f, 0.4f, 0.2f), new Vector3(0.3f, 0.45f, 0.3f), Quaternion.Euler(-15f, 30f, -20f));
            var band = Primitive(PrimitiveType.Cylinder, "WeakPoint", segmentModel, art.WeakPoint, new Vector3(0f, 0.05f, 0f), new Vector3(1.78f, 0.12f, 1.68f));
            band.SetActive(false);
            var segment = segmentRoot.AddComponent<BossSegment>();
            SetFields(segment, ("radius", 0.85f), ("shatterEffect", art.EnemyExplosion), ("weakPointMarker", band));
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

            SetFields(head.AddComponent<QuarryMawBrain>(), ("segmentPrefab", segmentPrefab), ("eruptTelegraphPrefab", telegraphPrefab),
                ("eruptEffect", art.EnemyExplosion), ("bulletPrefab", art.EnemyBulletVisual), ("bulletImpactPrefab", art.BulletImpact));
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

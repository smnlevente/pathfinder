using System;
using System.IO;
using Pathfinder.Game.Content;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        /// <summary>Designer-tunable content definitions (GDD §5–§7).</summary>
        sealed class Data
        {
            public ShipDefinition Ship;
            public PrimaryWeaponDefinition PhotonRepeater, PlasmaCutter, QuantumCharger;
            public SeekerSwarmDefinition SeekerSwarm;
            public NovaPulseDefinition NovaPulse;
            public EnemyDefinition Drone, Gunner, Spinner, Lancer, Carrier, Scorcher, ArcDrone, Crusher;
            public EliteModifierDefinition Armored, Hasty;
            public WaveDirectorConfig Waves;

            public PrimaryWeaponDefinition[] Primaries => new[] { PhotonRepeater, PlasmaCutter, QuantumCharger };
            public SecondaryWeaponDefinition[] Secondaries => new SecondaryWeaponDefinition[] { SeekerSwarm, NovaPulse };
            public EnemyDefinition[] Enemies => new[] { Drone, Gunner, Spinner, Lancer, Carrier, Scorcher, ArcDrone, Crusher };
        }

        /// <summary>
        /// Values from the GDD. Definitions are created only when missing, so inspector tuning survives rebuilds.
        /// <see cref="Upgrade"/> fills in fields that were added after an asset was created.
        /// </summary>
        static Data BuildDefinitions()
        {
            var data = new Data
            {
                Ship = Definition<ShipDefinition>("Ships/Ship_Stormbringer", "ship.stormbringer", "Stormbringer Battleship"),

                PhotonRepeater = Definition<PrimaryWeaponDefinition>("Weapons/Primary_PhotonRepeater", "primary.photon-repeater", "Photon Repeater"),
                PlasmaCutter = Definition<PrimaryWeaponDefinition>("Weapons/Primary_PlasmaCutter", "primary.plasma-cutter", "Plasma Cutter", so => Set(so,
                    ("Damage", 6f), ("DamageAtMaxRange", 0.4f), ("ShotsPerSecond", 2f), ("MagazineSize", 6), ("ReloadTime", 1.2f),
                    ("ProjectilesPerShot", 6), ("SpreadDegrees", 30f), ("ProjectileSpeed", 26f), ("Range", 7f), ("ProjectileRadius", 0.18f))),
                QuantumCharger = Definition<PrimaryWeaponDefinition>("Weapons/Primary_QuantumCharger", "primary.quantum-charger", "Quantum Charger", so => Set(so,
                    ("FireMode", (int)PrimaryFireMode.Charge), ("Damage", 15f), ("MaxChargeDamage", 120f), ("Pierce", 99),
                    ("MinChargeTime", 0.3f), ("FullChargeTime", 1.5f), ("OverheatTime", 2f), ("LockoutTime", 1f),
                    ("ProjectileSpeed", 36f), ("Range", 24f), ("ProjectileRadius", 0.2f), ("MaxChargeRadius", 0.55f), ("MaxChargeVisualScale", 2.2f))),

                SeekerSwarm = Definition<SeekerSwarmDefinition>("Weapons/Secondary_SeekerSwarm", "secondary.seeker-swarm", "Seeker Swarm", so => Set(so,
                    ("Cooldown", 8f))),
                NovaPulse = Definition<NovaPulseDefinition>("Weapons/Secondary_NovaPulse", "secondary.nova-pulse", "Nova Pulse", so => Set(so,
                    ("Cooldown", 12f))),

                Drone = Definition<EnemyDefinition>("Enemies/Enemy_Drone", "enemy.drone", "Drone", so => Set(so,
                    ("MaxHealth", 15f), ("MoveSpeed", 5.5f), ("ContactDamage", 10f), ("BudgetCost", 1), ("ScrapsDrop", 1), ("ScrapsDropChance", 0.5f))),
                Gunner = Definition<EnemyDefinition>("Enemies/Enemy_Gunner", "enemy.gunner", "Gunner", so => Set(so,
                    ("MaxHealth", 35f), ("MoveSpeed", 4f), ("ContactDamage", 10f), ("BudgetCost", 2), ("ScrapsDrop", 2), ("ScrapsDropChance", 0.75f))),
                Spinner = Definition<EnemyDefinition>("Enemies/Enemy_Spinner", "enemy.spinner", "Spinner", so => Set(so,
                    ("MaxHealth", 60f), ("MoveSpeed", 2f), ("ContactDamage", 10f), ("BudgetCost", 3), ("ScrapsDrop", 3), ("ScrapsDropChance", 0.85f))),
                Lancer = Definition<EnemyDefinition>("Enemies/Enemy_Lancer", "enemy.lancer", "Lancer", so => Set(so,
                    ("MaxHealth", 60f), ("MoveSpeed", 4.5f), ("ContactDamage", 12f), ("BudgetCost", 3), ("ScrapsDrop", 2), ("ScrapsDropChance", 0.8f))),
                Carrier = Definition<EnemyDefinition>("Enemies/Enemy_Carrier", "enemy.carrier", "Carrier", so => Set(so,
                    ("MaxHealth", 150f), ("MoveSpeed", 1.8f), ("ContactDamage", 15f), ("BudgetCost", 6), ("ScrapsDrop", 8), ("ScrapsDropChance", 1f))),

                // Galaxy 2 (GDD §7.3).
                Scorcher = Definition<EnemyDefinition>("Enemies/Enemy_Scorcher", "enemy.scorcher", "Scorcher", so => Set(so,
                    ("MaxHealth", 45f), ("MoveSpeed", 3.2f), ("ContactDamage", 10f), ("BudgetCost", 3), ("ScrapsDrop", 2), ("ScrapsDropChance", 0.8f))),
                ArcDrone = Definition<EnemyDefinition>("Enemies/Enemy_ArcDrone", "enemy.arc-drone", "Arc Drone", so => Set(so,
                    ("MaxHealth", 20f), ("MoveSpeed", 5f), ("ContactDamage", 8f), ("BudgetCost", 2), ("ScrapsDrop", 1), ("ScrapsDropChance", 0.7f))),
                Crusher = Definition<EnemyDefinition>("Enemies/Enemy_Crusher", "enemy.crusher", "Crusher", so => Set(so,
                    ("MaxHealth", 110f), ("MoveSpeed", 2.8f), ("ContactDamage", 15f), ("BudgetCost", 4), ("ScrapsDrop", 4), ("ScrapsDropChance", 0.9f),
                    ("KnockbackMultiplier", 0.15f))),

                Armored = Definition<EliteModifierDefinition>("Enemies/Elite_Armored", "elite.armored", "Armored", so => Set(so,
                    ("HealthMultiplier", 2f), ("MoveSpeedMultiplier", 0.75f))),
                Hasty = Definition<EliteModifierDefinition>("Enemies/Elite_Hasty", "elite.hasty", "Hasty", so => Set(so,
                    ("MoveSpeedMultiplier", 1.5f), ("AttackRateMultiplier", 1.5f))),
            };

            data.Waves = Definition<WaveDirectorConfig>("Waves/Waves_ShatteredExpanse", "waves.shattered-expanse", "The Shattered Expanse", so =>
            {
                var entries = so.FindProperty("<Enemies>k__BackingField");
                var pool = new (EnemyDefinition enemy, float weight, int minWave)[]
                {
                    (data.Drone, 5f, 1), (data.Gunner, 3f, 1), (data.Spinner, 2f, 2), (data.Lancer, 2f, 2), (data.Carrier, 1f, 4),
                };
                // Galaxy 2's enemies are added by AddGalaxyTwoEnemies, as for older configs.
                entries.arraySize = pool.Length;
                for (int i = 0; i < pool.Length; i++)
                {
                    var element = entries.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Enemy").objectReferenceValue = pool[i].enemy;
                    element.FindPropertyRelative("Weight").floatValue = pool[i].weight;
                    element.FindPropertyRelative("MinWave").intValue = pool[i].minWave;
                }
                Assign(so.FindProperty("<Elites>k__BackingField"), new Object[] { data.Armored, data.Hasty }, "Elites");
            });

            Upgrade(data);
            AddGalaxyTwoEnemies(data);
            return data;
        }

        /// <summary>One-off upgrades for definitions created by earlier builder versions. They only change fields still at their old default.</summary>
        static void Upgrade(Data data)
        {
            // M2 added Scraps drops. The Gunner predates them and got the class defaults (1 Scrap at 50%).
            var gunner = new SerializedObject(data.Gunner);
            if (gunner.FindProperty("<ScrapsDrop>k__BackingField").intValue == 1 && Mathf.Approximately(gunner.FindProperty("<ScrapsDropChance>k__BackingField").floatValue, 0.5f))
            {
                Set(gunner, ("ScrapsDrop", 2), ("ScrapsDropChance", 0.75f));
                gunner.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static T Definition<T>(string relativePath, string id, string displayName, Action<SerializedObject> configure = null) where T : ContentDefinition
        {
            string path = $"{DataDir}/{relativePath}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);

            var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            configure?.Invoke(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        /// <summary>Sets auto-property backing fields on a definition being configured.</summary>
        static void Set(SerializedObject so, params (string property, object value)[] values)
        {
            foreach (var (property, value) in values)
            {
                var field = so.FindProperty($"<{property}>k__BackingField")
                    ?? throw new InvalidOperationException($"{so.targetObject.GetType().Name} has no property '{property}'.");
                Assign(field, value, property);
            }
        }

        /// <summary>Fills in art and prefab references that are still empty. A designer's own choices are never overwritten.</summary>
        static void LinkDefinitions(Art art, Actors actors, Data data)
        {
            SetIfEmpty(data.PhotonRepeater, ("ProjectilePrefab", art.PlayerBoltVisual), ("ImpactPrefab", art.BoltImpact));
            SetIfEmpty(data.PlasmaCutter, ("ProjectilePrefab", art.PelletVisual), ("ImpactPrefab", art.BoltImpact));
            SetIfEmpty(data.QuantumCharger, ("ProjectilePrefab", art.ChargeBoltVisual), ("ImpactPrefab", art.MissileImpact));
            SetIfEmpty(data.SeekerSwarm, ("ProjectilePrefab", art.MissileVisual), ("ImpactPrefab", art.MissileImpact));
            SetIfEmpty(data.NovaPulse, ("ActivationEffect", art.NovaPulse));

            SetIfEmpty(data.Drone, ("Prefab", actors.Drone));
            SetIfEmpty(data.Gunner, ("Prefab", actors.Gunner));
            SetIfEmpty(data.Spinner, ("Prefab", actors.Spinner));
            SetIfEmpty(data.Lancer, ("Prefab", actors.Lancer));
            SetIfEmpty(data.Carrier, ("Prefab", actors.Carrier));
            SetIfEmpty(data.Scorcher, ("Prefab", actors.Scorcher));
            SetIfEmpty(data.ArcDrone, ("Prefab", actors.ArcDrone));
            SetIfEmpty(data.Crusher, ("Prefab", actors.Crusher));

            SetIfEmpty(data.Armored, ("MarkerPrefab", art.EliteMarker));
            SetIfEmpty(data.Hasty, ("MarkerPrefab", art.EliteMarker));
        }

        static void SetIfEmpty(Object target, params (string property, Object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (property, value) in values)
            {
                var field = so.FindProperty($"<{property}>k__BackingField");
                if (field.objectReferenceValue == null)
                    field.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

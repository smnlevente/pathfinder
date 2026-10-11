using System.Collections.Generic;
using Pathfinder.Core.Equipment;
using Pathfinder.Game.Content;
using Pathfinder.Game.Equipment;
using Pathfinder.Game.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        /// <summary>Equipment (GDD §8), ship traits and the content database.</summary>
        sealed class EquipmentContent
        {
            public readonly List<EquipmentDefinition> Items = new List<EquipmentDefinition>();
            public LightningCoreDefinition LightningCore;
            public SwarmProtocolDefinition SwarmProtocol;
            public ShipDefinition BotCommander;
            public ShipDefinition Titan;
            public ContentDatabase Database;
        }

        static EquipmentContent BuildEquipment(Art art, Data data, EnemyDefinition[] bosses)
        {
            var content = new EquipmentContent();
            var fx = BuildEquipmentEffects(art);

            // Stat items: data only, with generated descriptions. Values from GDD §8.3–8.5.
            content.Items.Add(StatItem("Overclock", "overclock", EquipmentCategory.Weapon, Rarity.Rare, (StatId.FireRate, null, new[] { 0.15f, 0.3f, 0.45f })));
            content.Items.Add(StatItem("Extended Mag", "extended-mag", EquipmentCategory.Weapon, Rarity.Common, (StatId.MagazineSize, null, new[] { 0.3f, 0.6f, 0.9f })));
            content.Items.Add(StatItem("Piercing Rounds", "piercing-rounds", EquipmentCategory.Weapon, Rarity.Rare, (StatId.Pierce, new[] { 1f, 2f, 3f }, null)));
            content.Items.Add(StatItem("Rapid Cycler", "rapid-cycler", EquipmentCategory.Secondary, Rarity.Common, (StatId.SecondaryCooldown, null, new[] { -0.15f, -0.3f, -0.45f })));
            content.Items.Add(StatItem("Twin Charge", "twin-charge", EquipmentCategory.Secondary, Rarity.Epic,
                (StatId.SecondaryCharges, new[] { 1f, 1f, 1f }, null), (StatId.SecondaryCooldown, null, new[] { 0f, -0.1f, -0.2f })));
            content.Items.Add(StatItem("Amplifier", "amplifier", EquipmentCategory.Secondary, Rarity.Common, (StatId.SecondaryDamage, null, new[] { 0.25f, 0.5f, 0.75f })));
            content.Items.Add(StatItem("Armor Plating", "armor-plating", EquipmentCategory.Hull, Rarity.Common, (StatId.MaxHull, new[] { 20f, 40f, 60f }, null)));
            content.Items.Add(StatItem("Capacitor", "capacitor", EquipmentCategory.Hull, Rarity.Common,
                (StatId.MaxShield, new[] { 15f, 30f, 45f }, null), (StatId.ShieldRegenDelay, new[] { -0.5f, -1f, -1.5f }, null)));
            content.Items.Add(StatItem("Salvage Magnet", "salvage-magnet", EquipmentCategory.Hull, Rarity.Common,
                (StatId.MagnetRadius, new[] { 2f, 3f, 4f }, null), (StatId.ScrapsGain, null, new[] { 0.15f, 0.3f, 0.45f })));
            content.Items.Add(StatItem("Critical Optics", "critical-optics", EquipmentCategory.Hull, Rarity.Rare, (StatId.CritChance, new[] { 0.1f, 0.15f, 0.2f }, null)));
            content.Items.Add(StatItem("Adrenal Thrusters", "adrenal-thrusters", EquipmentCategory.Hull, Rarity.Common, (StatId.MoveSpeed, null, new[] { 0.1f, 0.2f, 0.3f })));

            // Behaviour items.
            content.Items.Add(Item<RicochetDefinition>("Ricochet", "ricochet", EquipmentCategory.Weapon, Rarity.Rare));
            content.Items.Add(Item<SplitShotDefinition>("Split Shot", "split-shot", EquipmentCategory.Weapon, Rarity.Common));
            content.Items.Add(Item<IncendiaryDefinition>("Incendiary", "incendiary", EquipmentCategory.Weapon, Rarity.Rare));
            content.Items.Add(Item<ActiveReloadDefinition>("Precision Loader", "precision-loader", EquipmentCategory.Weapon, Rarity.Rare));
            content.Items.Add(Item<KillRefundDefinition>("Kill Refund", "kill-refund", EquipmentCategory.Secondary, Rarity.Rare));
            content.Items.Add(Item<LastStandDefinition>("Last Stand", "last-stand", EquipmentCategory.Hull, Rarity.Epic));
            content.Items.Add(Item<PhaseDashDefinition>("Phase Dash", "phase-dash", EquipmentCategory.Dash, Rarity.Rare));
            var blink = Item<BlinkDashDefinition>("Blink Dash", "blink-dash", EquipmentCategory.Dash, Rarity.Rare);
            SetIfEmpty(blink, ("BlinkEffect", fx.Blink));
            content.Items.Add(blink);
            var pushback = Item<PushbackDashDefinition>("Pushback Dash", "pushback-dash", EquipmentCategory.Dash, Rarity.Common);
            SetIfEmpty(pushback, ("ShockwaveEffect", fx.Pushback));
            content.Items.Add(pushback);
            content.Items.Add(Item<BulletDashDefinition>("Bullet Dash", "bullet-dash", EquipmentCategory.Dash, Rarity.Epic));

            // Ship traits (GDD §5.3). Lightning Core is an effect. The Titan's Reinforced Hull is pure stats.
            content.LightningCore = Definition<LightningCoreDefinition>("Equipment/Traits/Trait_LightningCore", "trait.lightning-core", "Lightning Core");
            SetIfEmpty(content.LightningCore, ("StrikeEffect", fx.Lightning));

            content.Titan = Definition<ShipDefinition>("Ships/Ship_Titan", "ship.titan", "Titan-Class Cruiser", so => Set(so,
                ("MaxHull", 150f), ("MaxShield", 25f), ("MoveSpeed", 7f), ("DamageTakenMultiplier", 0.9f),
                ("TraitName", "Reinforced Hull"), ("TraitDescription", "+50% hull and 10% less damage taken. The beginner ship.")));
            UpgradeStormbringer(data.Ship, content.LightningCore);

            // M7: the third ship (GDD §5.3), unlocked in the Lab. Every kill launches a homing bot from the wreck.
            content.SwarmProtocol = Definition<SwarmProtocolDefinition>("Equipment/Traits/Trait_SwarmProtocol", "trait.swarm-protocol", "Swarm Protocol");
            SetIfEmpty(content.SwarmProtocol, ("BotPrefab", fx.SwarmBot), ("ImpactPrefab", art.MissileImpact));
            content.BotCommander = Definition<ShipDefinition>("Ships/Ship_BotCommander", "ship.bot-commander", "Bot-Commander Frigate", so => Set(so,
                ("MaxHull", 90f), ("MaxShield", 30f), ("MoveSpeed", 8.5f), ("HullTint", new Color(0.62f, 1.1f, 0.82f)),
                ("TraitName", "Swarm Protocol"), ("TraitDescription", "Every kill launches a guided bot that homes on the nearest enemy.")));
            SetIfEmpty(content.BotCommander, ("Trait", content.SwarmProtocol));
            // Each ship its own colour; the older assets predate the field.
            TintIfUntinted(data.Ship, new Color(0.82f, 0.86f, 1.25f));

            content.Database = BuildContentDatabase(data, content, bosses);
            return content;
        }

        sealed class EquipmentFx
        {
            public GameObject Lightning, Blink, Pushback, SwarmBot;
        }

        static EquipmentFx BuildEquipmentEffects(Art art)
        {
            var bolt = Unlit("M_LightningBolt", new Color(2.5f, 4f, 6f));
            var fx = new EquipmentFx();

            // Lightning strike: a bright vertical bolt from above, a flash on the floor, and sparks.
            var strike = new GameObject("FX_LightningStrike");
            Primitive(PrimitiveType.Cylinder, "Bolt", strike.transform, bolt, new Vector3(0f, 3f, 0f), new Vector3(0.14f, 3f, 0.14f));
            FlatQuad("Flash", strike.transform, art.FxCyan, 0.05f, 3.6f);
            var sparks = Burst("Sparks", art.FxCyan, count: 18, speed: 6f, size: 0.3f, lifetime: 0.3f);
            sparks.transform.SetParent(strike.transform, false);
            sparks.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            SetFields(strike.AddComponent<TimedDestroy>(), ("lifetime", 0.3f));
            fx.Lightning = SavePrefab(strike, "FX");

            fx.Blink = SavePrefab(Burst("FX_Blink", art.FxCyan, count: 22, speed: 5f, size: 0.35f, lifetime: 0.3f), "FX");

            // Swarm Protocol's bot: a little cyan drone with stubby wings, nose first (it turns to face where it flies).
            var bot = new GameObject("PV_SwarmBot");
            Primitive(PrimitiveType.Sphere, "Body", bot.transform, art.PlayerMissile, Vector3.zero, new Vector3(0.26f, 0.18f, 0.34f));
            Primitive(PrimitiveType.Cube, "Wings", bot.transform, art.ShipGlow, new Vector3(0f, 0f, -0.04f), new Vector3(0.5f, 0.04f, 0.12f));
            FlatQuad("Glow", bot.transform, art.FxCyan, -0.05f, 0.75f);
            fx.SwarmBot = SavePrefab(bot, "Projectiles");

            // Pushback: a quick ring sized for a 4 u radius. The effect scales it to the module's radius.
            var push = new GameObject("FX_PushbackDash");
            var ring = FlatQuad("Ring", push.transform, Load<Material>(MaterialsDir + "/M_FX_NovaRing.mat"), -0.45f, 1f);
            SetFields(push.AddComponent<RingPulse>(), ("ring", ring.GetComponent<Renderer>()), ("duration", 0.22f), ("startScale", 1f), ("endScale", 9f));
            fx.Pushback = SavePrefab(push, "FX");
            return fx;
        }

        static StatEquipmentDefinition StatItem(string name, string id, EquipmentCategory category, Rarity rarity,
            params (StatId stat, float[] flat, float[] percent)[] bonuses)
        {
            return Item<StatEquipmentDefinition>(name, id, category, rarity, so =>
            {
                var list = so.FindProperty("<Bonuses>k__BackingField");
                list.arraySize = bonuses.Length;
                for (int i = 0; i < bonuses.Length; i++)
                {
                    var element = list.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Stat").enumValueIndex = (int)bonuses[i].stat;
                    FillFloats(element.FindPropertyRelative("Flat"), bonuses[i].flat);
                    FillFloats(element.FindPropertyRelative("Percent"), bonuses[i].percent);
                }
            });
        }

        static void FillFloats(SerializedProperty array, float[] values)
        {
            values ??= new float[0];
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        static T Item<T>(string name, string id, EquipmentCategory category, Rarity rarity, System.Action<SerializedObject> configure = null) where T : EquipmentDefinition =>
            Definition<T>($"Equipment/Equipment_{name.Replace(" ", "")}", $"equipment.{id}", name, so =>
            {
                Set(so, ("Category", (int)category), ("Rarity", (int)rarity));
                configure?.Invoke(so);
            });

        static void TintIfUntinted(ShipDefinition ship, Color tint)
        {
            if (ship.HullTint == Color.white)
                SetFields(ship, ("HullTint", tint));
        }

        /// <summary>The Stormbringer predates traits. Give it Lightning Core without touching any tuning.</summary>
        static void UpgradeStormbringer(ShipDefinition stormbringer, LightningCoreDefinition trait)
        {
            var so = new SerializedObject(stormbringer);
            var name = so.FindProperty("<TraitName>k__BackingField");
            if (string.IsNullOrEmpty(name.stringValue))
            {
                name.stringValue = "Lightning Core";
                so.FindProperty("<TraitDescription>k__BackingField").stringValue = "Each primary hit has an 8% chance to call a lightning strike from above.";
            }
            var traitField = so.FindProperty("<Trait>k__BackingField");
            if (traitField.objectReferenceValue == null)
                traitField.objectReferenceValue = trait;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The ID index, regenerated on every build so it always lists all content.</summary>
        static ContentDatabase BuildContentDatabase(Data data, EquipmentContent content, EnemyDefinition[] bosses)
        {
            const string path = DataDir + "/ContentDatabase.asset";
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(path);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<ContentDatabase>();
                AssetDatabase.CreateAsset(database, path);
            }

            var so = new SerializedObject(database);
            Assign(so.FindProperty("<Ships>k__BackingField"), new Object[] { content.Titan, data.Ship, content.BotCommander }, "Ships");
            Assign(so.FindProperty("<PrimaryWeapons>k__BackingField"), data.Primaries, "PrimaryWeapons");
            Assign(so.FindProperty("<SecondaryWeapons>k__BackingField"), data.Secondaries, "SecondaryWeapons");
            Assign(so.FindProperty("<Equipment>k__BackingField"), content.Items.ToArray(), "Equipment");
            Assign(so.FindProperty("<MetaUpgrades>k__BackingField"), BuildMetaUpgrades(data, content.BotCommander), "MetaUpgrades");
            Assign(so.FindProperty("<Events>k__BackingField"), BuildMysteryEvents(), "Events");
            var enemies = new System.Collections.Generic.List<Object>(data.Enemies);
            enemies.AddRange(bosses);
            Assign(so.FindProperty("<Enemies>k__BackingField"), enemies.ToArray(), "Enemies");
            so.ApplyModifiedPropertiesWithoutUndo();
            return database;
        }
    }
}

using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Run;
using UnityEditor;
using UnityEngine;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        // M7: each galaxy its own look and its own arena hazard (GDD §2). Themes stay dim and low in saturation so the
        // gameplay colours always stand out; hazards are neutral rock and dead-station amber rather than enemy red.

        sealed class GalaxyContent
        {
            public EnemyDefinition Asteroid, LaserPylon;
        }

        static GalaxyContent BuildGalaxyIdentity(Art art, RunConfig runConfig, GameObject sporeCloud)
        {
            var expanse = Theme("Theme_ShatteredExpanse", "theme.shattered-expanse", "The Shattered Expanse",
                new Color(0.16f, 0.36f, 0.46f), new Color(0.3f, 1.3f, 2.2f), new Color(0.02f, 0.02f, 0.05f));    // cold blue: asteroid fields
            var drift = Theme("Theme_GraveyardDrift", "theme.graveyard-drift", "The Graveyard Drift",
                new Color(0.44f, 0.29f, 0.14f), new Color(2.2f, 1.15f, 0.35f), new Color(0.045f, 0.028f, 0.016f)); // rust and amber: a dead station
            var nebula = Theme("Theme_VeilNebula", "theme.veil-nebula", "The Veil Nebula",
                new Color(0.34f, 0.2f, 0.52f), new Color(1.4f, 0.5f, 2.4f), new Color(0.035f, 0.015f, 0.06f));    // violet: a glowing nebula

            var content = new GalaxyContent();
            content.Asteroid = Definition<EnemyDefinition>("Hazards/Hazard_Asteroid", "hazard.asteroid", "Asteroid", so => Set(so,
                ("MaxHealth", 30f), ("MoveSpeed", 2.2f), ("ContactDamage", 12f), ("BudgetCost", 1), ("ScrapsDrop", 1), ("ScrapsDropChance", 0.6f),
                ("KnockbackMultiplier", 0.25f), ("IsHazard", true)));
            SetIfEmpty(content.Asteroid, ("Prefab", BuildAsteroid(art, content.Asteroid)));
            content.LaserPylon = Definition<EnemyDefinition>("Hazards/Hazard_LaserPylon", "hazard.laser-pylon", "Laser Pylon", so => Set(so,
                ("MaxHealth", 120f), ("MoveSpeed", 0f), ("ContactDamage", 0f), ("BudgetCost", 1), ("ScrapsDrop", 4), ("ScrapsDropChance", 1f),
                ("KnockbackMultiplier", 0f), ("IsHazard", true)));
            SetIfEmpty(content.LaserPylon, ("Prefab", BuildLaserPylon(art, content.LaserPylon)));

            // The run config learns each galaxy's look and hazard, once; after that they're tuning.
            var so = new SerializedObject(runConfig);
            var themes = so.FindProperty("<GalaxyThemes>k__BackingField");
            if (themes.arraySize == 0)
                Assign(themes, new Object[] { expanse, drift, nebula }, "GalaxyThemes");
            var hazards = so.FindProperty("<GalaxyHazards>k__BackingField");
            if (hazards.arraySize == 0)
            {
                hazards.arraySize = 3;
                SetHazard(hazards.GetArrayElementAtIndex(0), content.Asteroid, 3, respawn: true, clouds: 0, null);
                SetHazard(hazards.GetArrayElementAtIndex(1), content.LaserPylon, 2, respawn: false, clouds: 0, null);
                SetHazard(hazards.GetArrayElementAtIndex(2), null, 0, respawn: false, clouds: 2, sporeCloud);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return content;
        }

        static GalaxyThemeDefinition Theme(string asset, string id, string name, Color floor, Color walls, Color background) =>
            Definition<GalaxyThemeDefinition>($"Themes/{asset}", id, name, so => Set(so,
                ("FloorTint", floor), ("WallColor", walls), ("Background", background)));

        static void SetHazard(SerializedProperty element, EnemyDefinition enemy, int count, bool respawn, int clouds, GameObject cloudPrefab)
        {
            element.FindPropertyRelative("Enemy").objectReferenceValue = enemy;
            element.FindPropertyRelative("Count").intValue = count;
            element.FindPropertyRelative("Respawn").boolValue = respawn;
            element.FindPropertyRelative("Clouds").intValue = clouds;
            element.FindPropertyRelative("CloudPrefab").objectReferenceValue = cloudPrefab;
        }

        /// <summary>A tumbling chunk of grey rock with a faint warm rim: scenery, not a machine, so no red glow under it.</summary>
        static Enemy BuildAsteroid(Art art, EnemyDefinition definition)
        {
            const float radius = 0.9f;
            var rockMaterial = Lit("M_Asteroid", new Color(0.68f, 0.62f, 0.58f), smoothness: 0.15f, metallic: 0.05f);
            var root = new GameObject("Asteroid");
            var dust = FlatQuad("Dust", root.transform, art.FxGrey, 0.03f, radius * 3f).GetComponent<Renderer>(); // a pale haze, so it reads on a dark floor
            var model = Model(root.transform);
            var tumble = new GameObject("Rock").transform;
            tumble.SetParent(model, false);
            Primitive(PrimitiveType.Cube, "Chunk A", tumble, rockMaterial, Vector3.zero, new Vector3(1.3f, 1f, 1.15f), Quaternion.Euler(18f, 30f, 12f));
            Primitive(PrimitiveType.Cube, "Chunk B", tumble, rockMaterial, new Vector3(0.3f, 0.15f, -0.25f), new Vector3(0.9f, 0.8f, 0.85f), Quaternion.Euler(-25f, 12f, 40f));
            Primitive(PrimitiveType.Cube, "Chunk C", tumble, rockMaterial, new Vector3(-0.35f, -0.1f, 0.3f), new Vector3(0.75f, 0.7f, 0.8f), Quaternion.Euler(30f, -35f, -15f));
            SetFields(root.AddComponent<AsteroidBrain>(), ("rock", tumble));
            return FinishEnemyWithExclusions(root, definition, radius, art, new[] { dust });
        }

        /// <summary>A squat dead-station pylon with an orange core, sweeping a two-armed hazard-orange laser.</summary>
        static Enemy BuildLaserPylon(Art art, EnemyDefinition definition)
        {
            const float radius = 0.8f;
            var amber = Unlit("M_PylonCore", new Color(2.4f, 0.6f, 0.15f));
            var beamMaterial = Additive("M_FX_PylonBeam", new Color(1.9f, 0.42f, 0.1f), Load<Texture2D>(TexturesDir + "/T_LineCore.png")); // hazard orange, never weak-point yellow
            var root = new GameObject("LaserPylon");
            var model = Model(root.transform);
            Primitive(PrimitiveType.Cylinder, "Base", model, art.EnemyHull, Vector3.zero, new Vector3(1.4f, 0.18f, 1.4f));
            Primitive(PrimitiveType.Cylinder, "Column", model, art.EnemyHull, new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.3f, 0.6f));
            var core = Glow(Primitive(PrimitiveType.Sphere, "Core", model, amber, new Vector3(0f, 0.62f, 0f), Vector3.one * 0.45f));
            var beam = Line("Beam", root.transform, beamMaterial, 0.5f);
            beam.enabled = false;
            SetFields(root.AddComponent<LaserPylonBrain>(), ("beam", beam));
            return FinishEnemyWithExclusions(root, definition, radius, art, new Renderer[] { beam }, core);
        }
    }
}

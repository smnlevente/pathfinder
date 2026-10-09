using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        sealed class Actors
        {
            public PlayerShip Player;
            public Enemy Drone, Gunner, Spinner, Lancer, Carrier;
        }

        static Actors BuildActors(Art art, Data data)
        {
            var actors = new Actors
            {
                Drone = BuildDrone(art, data),
                Gunner = BuildGunner(art, data),
                Spinner = BuildSpinner(art, data),
                Lancer = BuildLancer(art, data),
            };
            actors.Carrier = BuildCarrier(art, data);
            actors.Player = BuildPlayer(art, data);
            return actors;
        }

        // Every enemy follows one visual language (GDD §12.4): a solid crimson body, small red-orange accent strips
        // that flare during wind-ups, and a red floor glow marking it as a unit.

        static Enemy BuildDrone(Art art, Data data)
        {
            var root = EnemyRoot("Drone", art, radius: 0.45f, out var halo, out var model);
            SetFields(model.gameObject.AddComponent<SimpleMotion>(), ("spinSpeed", 300f));
            Primitive(PrimitiveType.Cube, "Blade", model, art.EnemyHull, Vector3.zero, new Vector3(1f, 0.08f, 1f), Quaternion.Euler(0f, 45f, 0f));
            Primitive(PrimitiveType.Cylinder, "Hub", model, art.EnemyHull, new Vector3(0f, 0.06f, 0f), new Vector3(0.45f, 0.08f, 0.45f));
            var tips = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 tip = Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0f, 0f, 0.66f);
                tips[i] = Glow(Primitive(PrimitiveType.Cube, "Tip" + i, model, art.EnemyGlow, tip, new Vector3(0.12f, 0.1f, 0.12f), Quaternion.Euler(0f, 45f, 0f)));
            }

            root.AddComponent<DroneBrain>();
            return FinishEnemy(root, data.Drone, radius: 0.45f, art, halo, tips);
        }

        static Enemy BuildGunner(Art art, Data data)
        {
            var root = EnemyRoot("Gunner", art, radius: 0.6f, out var halo, out var model);
            Primitive(PrimitiveType.Cylinder, "Body", model, art.EnemyHull, Vector3.zero, new Vector3(1.1f, 0.22f, 1.1f));
            Primitive(PrimitiveType.Cube, "Turret", model, art.EnemyHull, new Vector3(0f, 0.22f, 0f), new Vector3(0.6f, 0.24f, 0.6f));
            var eye = Glow(Primitive(PrimitiveType.Cube, "Eye", model, art.EnemyGlow, new Vector3(0f, 0.25f, 0.3f), new Vector3(0.42f, 0.07f, 0.06f)));
            Primitive(PrimitiveType.Cube, "Barrel", model, art.EnemyHull, new Vector3(0f, 0.15f, 0.6f), new Vector3(0.18f, 0.18f, 0.7f));
            var muzzle = Glow(Primitive(PrimitiveType.Cube, "Muzzle", model, art.EnemyGlow, new Vector3(0f, 0.15f, 0.97f), new Vector3(0.22f, 0.22f, 0.08f)));

            SetFields(root.AddComponent<GunnerBrain>(), ("bulletPrefab", art.EnemyBulletVisual), ("bulletImpactPrefab", art.BulletImpact));
            return FinishEnemy(root, data.Gunner, radius: 0.6f, art, halo, eye, muzzle);
        }

        static Enemy BuildSpinner(Art art, Data data)
        {
            // A slowly turning disc ringed with emitters, which flare just before each volley.
            var root = EnemyRoot("Spinner", art, radius: 0.65f, out var halo, out var model);
            SetFields(model.gameObject.AddComponent<SimpleMotion>(), ("spinSpeed", 90f));
            Primitive(PrimitiveType.Cylinder, "Disc", model, art.EnemyHull, Vector3.zero, new Vector3(1.3f, 0.09f, 1.3f));
            Primitive(PrimitiveType.Cylinder, "Cap", model, art.EnemyHull, new Vector3(0f, 0.12f, 0f), new Vector3(0.7f, 0.08f, 0.7f));
            var emitters = new Renderer[6];
            for (int i = 0; i < emitters.Length; i++)
            {
                Vector3 position = Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(0f, 0.05f, 0.6f);
                emitters[i] = Glow(Primitive(PrimitiveType.Cube, "Emitter" + i, model, art.EnemyGlow, position, new Vector3(0.16f, 0.12f, 0.16f), Quaternion.Euler(0f, i * 60f, 0f)));
            }

            SetFields(root.AddComponent<SpinnerBrain>(), ("bulletPrefab", art.EnemyBulletVisual), ("bulletImpactPrefab", art.BulletImpact));
            return FinishEnemy(root, data.Spinner, radius: 0.65f, art, halo, emitters);
        }

        static Enemy BuildLancer(Art art, Data data)
        {
            // An arrowhead with a glowing spine. A red line shows its charge path during the wind-up.
            var root = EnemyRoot("Lancer", art, radius: 0.5f, out var halo, out var model);
            Primitive(PrimitiveType.Cube, "Body", model, art.EnemyHull, Vector3.zero, new Vector3(0.5f, 0.22f, 1.3f));
            Primitive(PrimitiveType.Cube, "Nose", model, art.EnemyHull, new Vector3(0f, 0f, 0.65f), new Vector3(0.45f, 0.2f, 0.45f), Quaternion.Euler(0f, 45f, 0f));
            Primitive(PrimitiveType.Cube, "FinL", model, art.EnemyHull, new Vector3(-0.35f, -0.04f, -0.4f), new Vector3(0.2f, 0.06f, 0.5f), Quaternion.Euler(0f, -20f, 0f));
            Primitive(PrimitiveType.Cube, "FinR", model, art.EnemyHull, new Vector3(0.35f, -0.04f, -0.4f), new Vector3(0.2f, 0.06f, 0.5f), Quaternion.Euler(0f, 20f, 0f));
            var spine = Glow(Primitive(PrimitiveType.Cube, "Spine", model, art.EnemyGlow, new Vector3(0f, 0.13f, 0f), new Vector3(0.08f, 0.06f, 1.1f)));
            var tail = Glow(Primitive(PrimitiveType.Cube, "Tail", model, art.EnemyGlow, new Vector3(0f, 0f, -0.66f), new Vector3(0.3f, 0.1f, 0.08f)));

            var brain = root.AddComponent<LancerBrain>();
            var line = FlatQuad("TelegraphLine", root.transform, art.LancerLine, 0.04f, 1f);
            line.GetComponent<Renderer>().enabled = false;
            SetFields(root.AddComponent<TelegraphLine>(), ("lancer", brain), ("line", line.GetComponent<Renderer>()));

            // The telegraph line must not flash white when the Lancer is hit.
            return FinishEnemyWithExclusions(root, data.Lancer, 0.5f, art, new[] { halo, line.GetComponent<Renderer>() }, spine, tail);
        }

        static Enemy BuildCarrier(Art art, Data data)
        {
            // A hulking tank: armoured bow, glowing launch bays along its flanks, and a yellow weak point at the stern.
            var root = EnemyRoot("Carrier", art, radius: 1.2f, out var halo, out var model);
            Primitive(PrimitiveType.Cube, "Hull", model, art.EnemyHull, Vector3.zero, new Vector3(2f, 0.55f, 2.6f));
            Primitive(PrimitiveType.Cube, "Deck", model, art.EnemyHull, new Vector3(0f, 0.38f, -0.2f), new Vector3(1.4f, 0.25f, 1.6f));
            Primitive(PrimitiveType.Cube, "Bow", model, art.EnemyHull, new Vector3(0f, 0.02f, 1.35f), new Vector3(2.1f, 0.5f, 0.25f));
            var bayL = Glow(Primitive(PrimitiveType.Cube, "BayL", model, art.EnemyGlow, new Vector3(-1.02f, 0.1f, -0.2f), new Vector3(0.06f, 0.18f, 1.5f)));
            var bayR = Glow(Primitive(PrimitiveType.Cube, "BayR", model, art.EnemyGlow, new Vector3(1.02f, 0.1f, -0.2f), new Vector3(0.06f, 0.18f, 1.5f)));
            Primitive(PrimitiveType.Cube, "WeakPoint", model, art.WeakPoint, new Vector3(0f, 0.05f, -1.32f), new Vector3(1.4f, 0.3f, 0.08f));

            SetFields(root.AddComponent<CarrierBrain>(), ("launchedEnemy", data.Drone));
            return FinishEnemy(root, data.Carrier, radius: 1.2f, art, halo, bayL, bayR);
        }

        static GameObject EnemyRoot(string name, Art art, float radius, out Renderer halo, out Transform model)
        {
            var root = new GameObject(name);
            halo = FlatQuad("Halo", root.transform, art.EnemyHalo, 0.03f, radius * 3.4f).GetComponent<Renderer>();
            model = Model(root.transform);
            return root;
        }

        static Enemy FinishEnemy(GameObject root, EnemyDefinition definition, float radius, Art art, Renderer halo, params Renderer[] glow) =>
            FinishEnemyWithExclusions(root, definition, radius, art, new[] { halo }, glow);

        static Enemy FinishEnemyWithExclusions(GameObject root, EnemyDefinition definition, float radius, Art art, Renderer[] flashExclusions, params Renderer[] glow)
        {
            var enemy = root.AddComponent<Enemy>();
            SetFields(enemy, ("radius", radius), ("definition", definition));

            var visual = AddVisual(root, enemy, art.EnemyExplosion, glow);
            SetFields(visual, ("flashExclusions", (Object[])flashExclusions));
            return SavePrefab(root, "Enemies").GetComponent<Enemy>();
        }

        static PlayerShip BuildPlayer(Art art, Data data)
        {
            var root = new GameObject("PlayerShip");
            var model = Model(root.transform);
            Primitive(PrimitiveType.Cube, "Hull", model, art.ShipHull, Vector3.zero, new Vector3(0.55f, 0.22f, 1.2f));
            Primitive(PrimitiveType.Cube, "Nose", model, art.ShipHull, new Vector3(0f, 0f, 0.6f), new Vector3(0.38f, 0.2f, 0.38f), Quaternion.Euler(0f, 45f, 0f));
            Primitive(PrimitiveType.Cube, "Wings", model, art.ShipHull, new Vector3(0f, -0.02f, -0.25f), new Vector3(1.5f, 0.06f, 0.45f));
            var tipL = Glow(Primitive(PrimitiveType.Cube, "WingtipL", model, art.ShipGlow, new Vector3(-0.75f, 0f, -0.25f), new Vector3(0.1f, 0.08f, 0.5f)));
            var tipR = Glow(Primitive(PrimitiveType.Cube, "WingtipR", model, art.ShipGlow, new Vector3(0.75f, 0f, -0.25f), new Vector3(0.1f, 0.08f, 0.5f)));
            var cockpit = Glow(Primitive(PrimitiveType.Sphere, "Cockpit", model, art.ShipGlow, new Vector3(0f, 0.12f, 0.2f), new Vector3(0.25f, 0.15f, 0.35f)));
            var engine = Glow(Primitive(PrimitiveType.Cube, "Engine", model, art.ShipGlow, new Vector3(0f, 0f, -0.62f), new Vector3(0.35f, 0.14f, 0.1f)));

            var trailObject = new GameObject("DashTrail");
            trailObject.transform.SetParent(model, false);
            trailObject.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.minVertexDistance = 0.1f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.9f, 1f, 0f);
            trail.sharedMaterial = art.FxCyan;
            trail.emitting = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;

            var ship = root.AddComponent<PlayerShip>();
            SetFields(ship, ("radius", 0.45f), ("ship", data.Ship), ("primaryWeapon", data.PhotonRepeater), ("secondaryWeapon", data.SeekerSwarm));

            // Ship glow parts brighten as a Quantum Charger shot charges.
            var visual = AddVisual(root, ship, art.PlayerExplosion, tipL, tipR, cockpit, engine);
            SetFields(visual, ("dashTrail", trail), ("hideOnDeath", true));
            return SavePrefab(root, "Player").GetComponent<PlayerShip>();
        }

        static Renderer Glow(GameObject part) => part.GetComponent<Renderer>();

        static Transform Model(Transform parent)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(parent, false);
            model.localPosition = new Vector3(0f, 0.5f, 0f);
            return model;
        }

        static EntityVisual AddVisual(GameObject root, CombatEntity entity, GameObject deathEffect, params Renderer[] glowRenderers)
        {
            var visual = root.AddComponent<EntityVisual>();
            var so = new SerializedObject(visual);
            so.FindProperty("entity").objectReferenceValue = entity;
            so.FindProperty("deathEffect").objectReferenceValue = deathEffect;
            Assign(so.FindProperty("glowRenderers"), glowRenderers, "glowRenderers");
            so.ApplyModifiedPropertiesWithoutUndo();
            return visual;
        }
    }
}

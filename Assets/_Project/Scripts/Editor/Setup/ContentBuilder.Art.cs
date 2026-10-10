using Pathfinder.Game.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        /// <summary>Generated placeholder art: materials, effects and projectile/pickup visuals.</summary>
        sealed class Art
        {
            // Visual language (GDD §12.4): player cyan; enemies solid crimson with red-orange accents and a red floor
            // glow; enemy bullets the only magenta; pickups gold; weak points yellow.
            public Material Floor, Wall, ShipHull, ShipGlow, EnemyHull, EnemyGlow, EnemyHalo, WeakPoint;
            public Material PlayerBolt, ChargeBolt, PlayerMissile, EnemyBulletCore, EnemyBulletBody, EnemyBulletHalo, Scrap;
            public Material Telegraph, LancerLine, EliteRing, NovaRing, WeakPointRing, ShockRing, FxOrange, FxCyan, FxMagenta, FxRed, FxGrey, Exhaust;

            public GameObject BoltImpact, BulletImpact, MissileImpact, EnemyExplosion, PlayerExplosion, Deflect, MuzzleFlash;
            public GameObject SpawnTelegraph, NovaPulse, EliteMarker, ChargeLane;
            public GameObject PlayerBoltVisual, PelletVisual, ChargeBoltVisual, MissileVisual, EnemyBulletVisual, ScrapVisual;

            // Galaxy 2: flames (a hazard orange), floor telegraphs, lightning and the Wardens' tether.
            public Material Flame, FlameCore, HazardArea, SlamArea, Beam, BoltCore, BoltGlow, Tether;
            public GameObject FlameVisual, ArcBolt, SlamShockwave, CoreOffline, CoreReboot, WarpIn;
        }

        static Art BuildArt()
        {
            var grid = CreateTexture("T_Grid", 128, (x, y) =>
            {
                bool line = x < 3 || y < 3;
                return line ? new Color(0.16f, 0.36f, 0.46f) : new Color(0.05f, 0.06f, 0.09f);
            }, TextureWrapMode.Repeat, alphaIsTransparency: false);
            var dot = RadialTexture("T_SoftDot", 64, d => (1f - d) * (1f - d) * (d < 1f ? 1f : 0f));
            var ring = RadialTexture("T_Ring", 128, d => Mathf.Exp(-Mathf.Pow((d - 0.88f) / 0.06f, 2f)));

            // Enemy bullet body (GDD §12.4): a tinted rim inside a dark outline. The outline separates neighbouring
            // bullets in dense patterns and against glowing backgrounds; it stays black whatever the tint.
            var bulletBody = CreateTexture("T_BulletBody", 64, (x, y) =>
            {
                float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 0.74f)
                    return Color.white;
                if (d < 0.94f)
                    return new Color(0f, 0f, 0f, 0.9f);
                return new Color(0f, 0f, 0f, Mathf.Clamp01((1f - d) / 0.06f) * 0.9f);
            }, TextureWrapMode.Clamp, alphaIsTransparency: true);

            // Charge telegraph chevron, pointing up the texture (+Z once laid flat).
            var chevron = CreateTexture("T_Chevron", 64, (x, y) =>
            {
                float u = (x + 0.5f) / 32f - 1f, v = (y + 0.5f) / 32f - 1f;
                float arm = Mathf.Abs(v - (0.45f - Mathf.Abs(u) * 0.95f));
                float alpha = Mathf.Abs(u) < 0.85f ? Mathf.Clamp01((0.2f - arm) / 0.05f) : 0f;
                return new Color(1f, 1f, 1f, alpha);
            }, TextureWrapMode.Clamp, alphaIsTransparency: true);

            var art = new Art
            {
                Floor = Material("M_ArenaFloor", LitShader, m =>
                {
                    m.SetColor("_BaseColor", Color.white);
                    m.SetTexture("_BaseMap", grid);
                    m.SetTextureScale("_BaseMap", ArenaSize / 2f);   // one grid cell = 2 × 2 units
                    m.SetFloat("_Smoothness", 0.55f);
                    m.SetTexture("_EmissionMap", grid);
                    m.SetColor("_EmissionColor", new Color(0.35f, 0.75f, 1f) * 0.6f);
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }),
                Wall = Unlit("M_ArenaWall", new Color(0.3f, 1.3f, 2.2f)),

                ShipHull = Lit("M_ShipHull", new Color(0.62f, 0.68f, 0.78f), smoothness: 0.6f, metallic: 0.35f),
                ShipGlow = Unlit("M_ShipGlow", new Color(0.3f, 1.6f, 2.6f)),
                PlayerBolt = Unlit("M_PlayerBolt", new Color(0.5f, 2.2f, 3.2f)),
                ChargeBolt = Unlit("M_ChargeBolt", new Color(2f, 4f, 6f)),
                PlayerMissile = Unlit("M_PlayerMissile", new Color(1.2f, 3f, 4.5f)),

                EnemyHull = Lit("M_EnemyHull", new Color(0.72f, 0.13f, 0.1f), smoothness: 0.5f, metallic: 0.35f, emission: new Color(0.22f, 0.03f, 0.02f)),
                EnemyGlow = Unlit("M_EnemyGlow", new Color(3.4f, 0.6f, 0.12f)),
                EnemyHalo = Additive("M_FX_EnemyHalo", new Color(1.1f, 0.08f, 0.05f), dot),
                WeakPoint = Unlit("M_WeakPoint", new Color(3.2f, 2.4f, 0.4f)),
                EnemyBulletCore = Unlit("M_EnemyBulletCore", new Color(2.4f, 1.8f, 2.4f)),
                EnemyBulletBody = Blended("M_FX_EnemyBulletBody", new Color(2.2f, 0.22f, 1.65f), bulletBody),
                EnemyBulletHalo = Blended("M_FX_EnemyBulletHalo", new Color(1.2f, 0.08f, 0.9f, 0.45f), dot),

                Scrap = Unlit("M_Scrap", new Color(2.8f, 1.9f, 0.35f)),
                Telegraph = Unlit("M_SpawnTelegraph", new Color(2.6f, 0.25f, 0.2f)),
                LancerLine = Blended("M_FX_LancerLine", Color.white, chevron), // tinted per chevron by ChevronLane
                EliteRing = Additive("M_FX_EliteRing", new Color(3.5f, 2.4f, 0.4f), ring),
                NovaRing = Additive("M_FX_NovaRing", new Color(0.35f, 1.5f, 2.8f), ring),
                WeakPointRing = Additive("M_FX_WeakPointRing", new Color(2.6f, 2f, 0.3f), ring),
                ShockRing = Additive("M_FX_ShockRing", new Color(2.4f, 0.9f, 0.2f), ring),
                FxOrange = Additive("M_FX_Orange", new Color(4f, 1.4f, 0.3f), dot),
                FxCyan = Additive("M_FX_Cyan", new Color(0.6f, 2.4f, 4f), dot),
                FxMagenta = Additive("M_FX_Magenta", new Color(2.2f, 0.35f, 1.7f), dot),
                FxRed = Additive("M_FX_Red", new Color(3f, 0.25f, 0.15f), dot),
                FxGrey = Additive("M_FX_Grey", new Color(1.1f, 1.15f, 1.25f), dot),
                Exhaust = Additive("M_FX_Exhaust", new Color(0.15f, 0.55f, 0.95f), dot), // below the bloom threshold: a hint, not a glare
            };

            BuildEffects(art);
            art.ChargeLane = SavePrefab(ChargeLane("FX_ChargeLane", art), "FX");
            BuildProjectileVisuals(art);
            BuildHazardArt(art);
            return art;
        }

        static void BuildEffects(Art art)
        {
            art.BoltImpact = SavePrefab(Burst("FX_BoltImpact", art.FxCyan, count: 6, speed: 4f, size: 0.18f, lifetime: 0.15f), "FX");
            art.BulletImpact = SavePrefab(Burst("FX_BulletImpact", art.FxMagenta, count: 10, speed: 5f, size: 0.25f, lifetime: 0.2f), "FX");
            // Armour: a dull grey spark, so a deflected shot reads as "no damage" (GDD §12.4).
            art.Deflect = SavePrefab(Burst("FX_Deflect", art.FxGrey, count: 5, speed: 6f, size: 0.14f, lifetime: 0.12f), "FX");
            art.MissileImpact = SavePrefab(Burst("FX_MissileImpact", art.FxCyan, count: 14, speed: 6f, size: 0.3f, lifetime: 0.25f), "FX");
            art.EnemyExplosion = SavePrefab(Explosion("FX_EnemyExplosion", art.FxOrange, art.FxRed, art.ShockRing, count: 28, speed: 7f, size: 0.4f, lifetime: 0.5f, ringSize: 5f), "FX");
            art.PlayerExplosion = SavePrefab(Explosion("FX_PlayerExplosion", art.FxCyan, art.FxCyan, art.NovaRing, count: 70, speed: 9f, size: 0.55f, lifetime: 0.9f, ringSize: 8f), "FX");
            art.MuzzleFlash = SavePrefab(Burst("FX_MuzzleFlash", art.FxCyan, count: 4, speed: 3f, size: 0.28f, lifetime: 0.07f), "FX");

            // A soft additive glow on the floor reads as "something is arriving here", not as a solid object.
            var telegraph = new GameObject("FX_SpawnTelegraph");
            var glow = FlatQuad("Glow", telegraph.transform, art.FxRed, 0.03f, 2.4f);
            SetFields(glow.AddComponent<SimpleMotion>(), ("pulseAmount", 0.2f), ("pulseFrequency", 3f));
            var core = Primitive(PrimitiveType.Cylinder, "Core", telegraph.transform, art.Telegraph, new Vector3(0f, 0.02f, 0f), new Vector3(0.35f, 0.01f, 0.35f));
            SetFields(core.AddComponent<SimpleMotion>(), ("pulseAmount", 0.4f), ("pulseFrequency", 3f));
            art.SpawnTelegraph = SavePrefab(telegraph, "FX");

            // Nova Pulse: an expanding cyan ring sized to the 4 u blast radius (the ring band sits at 44% of the quad).
            var nova = new GameObject("FX_NovaPulse");
            var novaRing = FlatQuad("Ring", nova.transform, art.NovaRing, -0.45f, 1f);
            SetFields(nova.AddComponent<RingPulse>(), ("ring", novaRing.GetComponent<Renderer>()), ("duration", 0.3f), ("startScale", 1f), ("endScale", 9f));
            art.NovaPulse = SavePrefab(nova, "FX");

            // Elite marker: a slowly turning gold ring under the elite, authored at unit size.
            var elite = new GameObject("FX_EliteMarker");
            var eliteRing = FlatQuad("Ring", elite.transform, art.EliteRing, 0.05f, 1f);
            SetFields(eliteRing.AddComponent<SimpleMotion>(), ("pulseAmount", 0.08f), ("pulseFrequency", 1.5f));
            SetFields(elite.AddComponent<SimpleMotion>(), ("spinSpeed", 45f));
            art.EliteMarker = SavePrefab(elite, "FX");
        }

        /// <summary>Sparks, slower debris and a shockwave ring on the floor: a death that reads at a glance.</summary>
        static GameObject Explosion(string name, Material sparks, Material debris, Material shock, int count, float speed, float size, float lifetime, float ringSize)
        {
            var root = Burst(name, sparks, count, speed, size, lifetime);
            Burst("Debris", debris, Mathf.Max(3, count / 3), speed * 0.55f, size * 1.5f, lifetime).transform.SetParent(root.transform, false);
            var wave = new GameObject("Shockwave");
            wave.transform.SetParent(root.transform, false);
            var ring = FlatQuad("Ring", wave.transform, shock, -0.45f, 1f);
            SetFields(wave.AddComponent<RingPulse>(), ("ring", ring.GetComponent<Renderer>()), ("duration", 0.3f), ("startScale", 0.6f), ("endScale", ringSize));
            return root;
        }

        static GameObject Burst(string name, Material material, int count, float speed, float size, float lifetime)
        {
            var go = new GameObject(name);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = lifetime;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return go;
        }

        /// <summary>A chevron lane telegraph (GDD §12.4). The template chevron stays disabled; the lane clones it.</summary>
        static GameObject ChargeLane(string name, Art art, Transform parent = null, float size = 0.9f, float spacing = 1.1f)
        {
            var root = new GameObject(name);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var template = FlatQuad("Chevron", root.transform, art.LancerLine, 0.05f, 1f);
            var renderer = template.GetComponent<Renderer>();
            renderer.enabled = false;
            SetFields(root.AddComponent<ChevronLane>(), ("chevronTemplate", renderer), ("size", size), ("spacing", spacing));
            return root;
        }

        static void BuildProjectileVisuals(Art art)
        {
            // Player shots are elongated cyan bolts. Enemy shots are round magenta orbs (GDD §12.4).
            var bolt = new GameObject("PV_PlayerBolt");
            Primitive(PrimitiveType.Capsule, "Mesh", bolt.transform, art.PlayerBolt, Vector3.zero, new Vector3(0.14f, 0.35f, 0.14f), Quaternion.Euler(90f, 0f, 0f));
            art.PlayerBoltVisual = SavePrefab(bolt, "Projectiles");

            var pellet = new GameObject("PV_Pellet");
            Primitive(PrimitiveType.Capsule, "Mesh", pellet.transform, art.PlayerBolt, Vector3.zero, new Vector3(0.12f, 0.16f, 0.12f), Quaternion.Euler(90f, 0f, 0f));
            art.PelletVisual = SavePrefab(pellet, "Projectiles");

            var charge = new GameObject("PV_ChargeBolt");
            Primitive(PrimitiveType.Capsule, "Core", charge.transform, art.ChargeBolt, Vector3.zero, new Vector3(0.3f, 0.45f, 0.3f), Quaternion.Euler(90f, 0f, 0f));
            FlatQuad("Glow", charge.transform, art.FxCyan, 0f, 1.4f);
            art.ChargeBoltVisual = SavePrefab(charge, "Projectiles");

            var missile = new GameObject("PV_Missile");
            Primitive(PrimitiveType.Capsule, "Body", missile.transform, art.PlayerMissile, Vector3.zero, new Vector3(0.14f, 0.22f, 0.14f), Quaternion.Euler(90f, 0f, 0f));
            var trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(missile.transform, false);
            trailObject.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.minVertexDistance = 0.08f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.22f, 1f, 0f);
            trail.sharedMaterial = art.FxCyan;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            art.MissileVisual = SavePrefab(missile, "Projectiles");

            // Layers, bottom to top: a faint soft glow, the rim-and-outline body, and a solid white core.
            var bullet = new GameObject("PV_EnemyBullet");
            var halo = FlatQuad("Halo", bullet.transform, art.EnemyBulletHalo, -0.02f, 1.05f);
            SetFields(halo.AddComponent<SimpleMotion>(), ("pulseAmount", 0.15f), ("pulseFrequency", 7f));
            FlatQuad("Body", bullet.transform, art.EnemyBulletBody, 0f, 0.62f);
            Primitive(PrimitiveType.Sphere, "Core", bullet.transform, art.EnemyBulletCore, new Vector3(0f, 0.04f, 0f), Vector3.one * 0.26f);
            art.EnemyBulletVisual = SavePrefab(bullet, "Projectiles");

            // Scraps: small spinning gold chunks.
            var scrap = new GameObject("PV_Scrap");
            var chunk = Primitive(PrimitiveType.Cube, "Chunk", scrap.transform, art.Scrap, Vector3.zero, Vector3.one * 0.24f, Quaternion.Euler(45f, 0f, 45f));
            SetFields(chunk.AddComponent<SimpleMotion>(), ("spinSpeed", 200f));
            art.ScrapVisual = SavePrefab(scrap, "Pickups");
        }
    }
}

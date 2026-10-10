using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Feedback for an entity's model. It flashes on hits, glows while an enemy telegraphs or the ship charges a
    /// shot, blinks during post-hit invulnerability, draws the dash trail, and spawns death and secondary-weapon
    /// effects. It only reads gameplay state and events and never changes them.
    /// </summary>
    public sealed class EntityVisual : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] CombatEntity entity;
        [SerializeField, Tooltip("Emissive parts that brighten while telegraphing an attack or charging a shot.")] Renderer[] glowRenderers = new Renderer[0];
        [SerializeField, Min(1f)] float telegraphGlow = 3f;
        [SerializeField, Min(0f)] float flashDuration = 0.07f;
        [SerializeField] Color flashColor = new Color(3f, 3f, 3f, 1f);
        [SerializeField, Tooltip("Renderers that never flash, e.g. the floor glow under enemies.")] Renderer[] flashExclusions = new Renderer[0];
        [SerializeField] TrailRenderer dashTrail;
        [SerializeField] GameObject deathEffect;
        [SerializeField] bool hideOnDeath;

        Renderer[] _renderers;
        Renderer[] _flashRenderers;
        Color[] _glowBaseColors;
        MaterialPropertyBlock _block;
        float _flashRemaining;
        bool _usingBlock;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            _flashRenderers = System.Array.FindAll(_renderers, r => System.Array.IndexOf(flashExclusions, r) < 0);
            _glowBaseColors = new Color[glowRenderers.Length];
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                var material = glowRenderers[i].sharedMaterial;
                _glowBaseColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            }
        }

        void OnEnable()
        {
            entity.Damaged += OnDamaged;
            entity.Died += OnDied;
            if (entity is PlayerShip ship)
                ship.SecondaryActivated += OnSecondaryActivated;
        }

        void OnDisable()
        {
            entity.Damaged -= OnDamaged;
            entity.Died -= OnDied;
            if (entity is PlayerShip ship)
                ship.SecondaryActivated -= OnSecondaryActivated;
        }

        void OnSecondaryActivated(PlayerShip ship, SecondaryWeaponDefinition weapon)
        {
            if (weapon.ActivationEffect != null)
                Instantiate(weapon.ActivationEffect, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        void OnDamaged(CombatEntity source, DamageInfo info, DamageResult result)
        {
            if (!result.Blocked)
                _flashRemaining = flashDuration;
        }

        void OnDied(CombatEntity source, DamageInfo info)
        {
            if (deathEffect != null)
                Instantiate(deathEffect, transform.position + Vector3.up * 0.5f, Quaternion.identity);

            if (hideOnDeath)
            {
                foreach (var renderer in _renderers)
                    renderer.enabled = false;
                if (dashTrail != null)
                    dashTrail.emitting = false;
            }
        }

        void LateUpdate()
        {
            if (!entity.IsAlive)
                return;

            var player = entity as PlayerShip;
            if (dashTrail != null)
                dashTrail.emitting = player != null && player.IsIntangible;

            // Blink only during post-hit invulnerability. Dash i-frames have their own trail cue, and god mode has none.
            bool hidden = player != null && player.Vitals.HasHitInvulnerability && Mathf.Repeat(Time.time * 16f, 1f) < 0.5f;
            foreach (var renderer in _renderers)
                renderer.enabled = !hidden;

            _flashRemaining -= Time.deltaTime;
            if (_flashRemaining > 0f)
            {
                ApplyToAll(flashColor);
                return;
            }

            float glow = GlowFactor(player);
            if (glow > 1.001f)
            {
                ClearBlocks();
                for (int i = 0; i < glowRenderers.Length; i++)
                {
                    _block.SetColor(BaseColorId, _glowBaseColors[i] * glow);
                    glowRenderers[i].SetPropertyBlock(_block);
                }
                _usingBlock = true;
                return;
            }

            ClearBlocks();
        }

        float GlowFactor(PlayerShip player)
        {
            if (entity is Enemy enemy)
            {
                if (enemy.IsTelegraphing)
                    return telegraphGlow;
                return enemy.IsBurning && Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? 2f : 1f;
            }
            if (player == null)
                return 1f;

            // Charging brightens the ship. Holding a full charge too long flickers as a warning.
            var status = player.Primary.Status;
            float glow = 1f + status.Charge * (telegraphGlow - 1f);
            if (status.Warning && status.Charge > 0f)
                glow *= Mathf.Repeat(Time.time * 12f, 1f) < 0.5f ? 1.4f : 0.7f;
            return glow;
        }

        void ApplyToAll(Color color)
        {
            _block.SetColor(BaseColorId, color);
            foreach (var renderer in _flashRenderers)
                renderer.SetPropertyBlock(_block);
            _usingBlock = true;
        }

        void ClearBlocks()
        {
            if (!_usingBlock)
                return;
            foreach (var renderer in _renderers)
                renderer.SetPropertyBlock(null);
            _usingBlock = false;
        }
    }
}

using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Feedback for an entity's model. It flashes on hits, glows while an enemy telegraphs, blinks during
    /// post-hit invulnerability, draws the dash trail, and spawns an effect on death. It only reads gameplay
    /// state and events and never changes them.
    /// </summary>
    public sealed class EntityVisual : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] CombatEntity entity;
        [SerializeField, Tooltip("Emissive parts that brighten while telegraphing an attack.")] Renderer[] glowRenderers = new Renderer[0];
        [SerializeField, Min(1f)] float telegraphGlow = 3f;
        [SerializeField, Min(0f)] float flashDuration = 0.07f;
        [SerializeField] Color flashColor = new Color(3f, 3f, 3f, 1f);
        [SerializeField] TrailRenderer dashTrail;
        [SerializeField] GameObject deathEffect;
        [SerializeField] bool hideOnDeath;

        Renderer[] _renderers;
        Color[] _glowBaseColors;
        MaterialPropertyBlock _block;
        float _flashRemaining;
        bool _usingBlock;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
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
        }

        void OnDisable()
        {
            entity.Damaged -= OnDamaged;
            entity.Died -= OnDied;
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

            // Blink while invulnerable after a hit. Dash i-frames have their own trail cue instead.
            bool hidden = player != null && player.Vitals.IsInvulnerable && !player.IsIntangible && Mathf.Repeat(Time.time * 16f, 1f) < 0.5f;
            foreach (var renderer in _renderers)
                renderer.enabled = !hidden;

            _flashRemaining -= Time.deltaTime;
            if (_flashRemaining > 0f)
            {
                ApplyToAll(flashColor);
                return;
            }

            bool telegraphing = entity is Enemy enemy && enemy.IsTelegraphing;
            if (telegraphing)
            {
                ClearBlocks();
                for (int i = 0; i < glowRenderers.Length; i++)
                {
                    _block.SetColor(BaseColorId, _glowBaseColors[i] * telegraphGlow);
                    glowRenderers[i].SetPropertyBlock(_block);
                }
                _usingBlock = true;
                return;
            }

            ClearBlocks();
        }

        void ApplyToAll(Color color)
        {
            _block.SetColor(BaseColorId, color);
            foreach (var renderer in _renderers)
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

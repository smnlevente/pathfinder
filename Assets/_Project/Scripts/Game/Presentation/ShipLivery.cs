using System.Collections.Generic;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// A player ship's colours. Its lights, engine exhaust and dash trail wear its pilot's paint (playtest 8). Its hull wears
    /// the ship's own colour (M7: each ship looks like itself), except in local co-op, where it wears the pilot's paint too:
    /// telling the players apart matters more there than telling the ships apart. Set on material instances, since hit
    /// flashes use property blocks and clear them afterwards.
    /// </summary>
    sealed class ShipLivery
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly PlayerShip _ship;
        readonly List<(Renderer part, Color metal)> _hull = new List<(Renderer, Color)>();
        readonly Renderer[] _lights;
        readonly TrailRenderer _dashTrail;
        readonly List<TrailRenderer> _exhaust = new List<TrailRenderer>();
        readonly List<Material> _instances = new List<Material>();
        ShipDefinition _shownShip;
        int _shownPaint = -1;
        bool _shownCoop;

        /// <summary>The lights' colour, for the charge glow to brighten.</summary>
        public Color Glow { get; private set; }

        public ShipLivery(PlayerShip ship, Renderer[] lights, TrailRenderer dashTrail)
        {
            _ship = ship;
            _lights = lights;
            _dashTrail = dashTrail;
            foreach (var part in ship.GetComponentsInChildren<Renderer>(true))
            {
                if (part.name is "Hull" or "Nose" or "Wings" && part.sharedMaterial != null)
                    _hull.Add((part, part.sharedMaterial.GetColor(BaseColorId)));
            }
            foreach (var trail in ship.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (trail != dashTrail)
                    _exhaust.Add(trail);
            }
        }

        /// <summary>Repaints the ship when its definition, its pilot's paint or co-op changes. True if it did.</summary>
        public bool Refresh()
        {
            bool coop = CombatRegistry.Players.Count > 1;
            int paintIndex = LocalPlayers.PaintOf(_ship.PlayerIndex);
            if (_ship.Definition == _shownShip && paintIndex == _shownPaint && coop == _shownCoop)
                return false;
            _shownShip = _ship.Definition;
            _shownPaint = paintIndex;
            _shownCoop = coop;

            var paint = PilotPaint.Get(paintIndex);
            var hullTint = _shownShip != null ? _shownShip.HullTint : Color.white;
            foreach (var (part, metal) in _hull)
                SetColor(part, coop ? paint.Hull(metal) : metal * hullTint);
            foreach (var light in _lights)
                SetColor(light, paint.Glow);
            if (_dashTrail != null)
                SetColor(_dashTrail, paint.DashTrail);
            foreach (var trail in _exhaust)
                SetColor(trail, paint.Exhaust);
            Glow = paint.Glow;
            return true;
        }

        void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;
            bool fresh = renderer.sharedMaterial != null && !_instances.Contains(renderer.sharedMaterial);
            var material = renderer.material; // its own copy, made on first use
            if (fresh)
                _instances.Add(material);
            material.SetColor(BaseColorId, color);
        }

        /// <summary>Destroys the material copies, which Unity doesn't do on its own.</summary>
        public void Release()
        {
            foreach (var material in _instances)
            {
                if (material != null)
                    Object.Destroy(material);
            }
            _instances.Clear();
        }
    }
}

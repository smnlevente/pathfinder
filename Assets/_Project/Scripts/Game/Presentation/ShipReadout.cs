using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// The readout on a ship, where its pilot is looking (playtest 6): an ammo/reload ring around it and hull and shield
    /// bars under it. It stays faint while all is well and brightens when it matters: reloading, low ammo or hull, or just
    /// hit. In local co-op (M8) every ship has one, tagged in its pilot's colour, and a downed pilot's wreck shows a call
    /// for help with a ring that fills as a teammate revives them.
    /// </summary>
    public sealed class ShipReadout
    {
        readonly RectTransform _rect;
        readonly CanvasGroup _group;
        readonly Image _track, _ring, _sweetSpot;
        readonly UiBar _hull, _shield;
        readonly Text _tag, _help;
        float _alpha, _lastHitTime = -10f;
        PlayerShip _subscribed;

        public bool Shown => _rect.gameObject.activeSelf;
        public float AmmoFill => _ring.fillAmount;
        public float HullFill => _hull.Fill;
        public bool SweetSpotShown => _sweetSpot.enabled;

        /// <summary>The call for help over a downed pilot's wreck, empty otherwise. For tests.</summary>
        public string Help => _help.gameObject.activeSelf ? _help.text : string.Empty;

        public ShipReadout(Transform parent, string name)
        {
            _rect = UiKit.Node(name, parent);
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(190f, 190f);
            _group = _rect.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = _group.blocksRaycasts = false;
            _track = UiKit.Image(_rect, "Ammo Track", new Color(1f, 1f, 1f, 0.12f), UiKit.Ring);
            UiKit.Place(_track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(164f, 164f));
            _sweetSpot = CombatHud.RingFill(_rect, "Sweet Spot", UiKit.Gold, 178f);
            _sweetSpot.enabled = false;
            _ring = CombatHud.RingFill(_rect, "Ammo", CombatHud.WeaponColor, 164f);
            _shield = new UiBar(_rect, "Shield", CombatHud.ShieldColor);
            UiKit.Place(_shield.Rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -94f), new Vector2(90f, 5f));
            _hull = new UiBar(_rect, "Hull", CombatHud.HullColor);
            UiKit.Place(_hull.Rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -103f), new Vector2(90f, 8f));
            _tag = UiKit.Text(_rect, "Player", string.Empty, 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -124f), new Vector2(120f, 28f));
            CombatHud.AddShadow(_tag);
            _help = UiKit.Text(_rect, "Help", string.Empty, 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_help.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 104f), new Vector2(420f, 32f));
            CombatHud.AddShadow(_help);
            _rect.gameObject.SetActive(false);
        }

        /// <summary>Shows the readout on this pilot's ship, or hides it (null, or <paramref name="visible"/> false).</summary>
        public void Refresh(PlayerShip player, bool visible, bool coop, Camera view)
        {
            Subscribe(player);
            bool downed = player != null && !player.IsAlive;
            bool shown = visible && player != null && view != null && (player.IsAlive || (coop && downed));
            _rect.gameObject.SetActive(shown);
            if (!shown)
                return;

            // Centred on the ship's body, which floats half a unit above its ground point. Anchored at the ship's
            // normalised viewport position, so it lines up at any resolution or aspect.
            Vector2 viewport = view.WorldToViewportPoint(player.transform.position + Vector3.up * 0.5f);
            _rect.anchorMin = _rect.anchorMax = viewport;
            _rect.anchoredPosition = Vector2.zero;

            var color = CoopRules.PilotColor(player.PlayerIndex);
            _tag.gameObject.SetActive(coop);
            _tag.text = CoopRules.Tag(player.PlayerIndex);
            _tag.color = color;
            _track.color = coop ? new Color(color.r, color.g, color.b, 0.22f) : new Color(1f, 1f, 1f, 0.12f);
            _hull.Rect.gameObject.SetActive(!downed);
            _help.gameObject.SetActive(downed);

            if (downed)
            {
                // A wreck: the ring is the revive, and the call for help pulses.
                _ring.fillAmount = player.ReviveProgress;
                _ring.color = Color.Lerp(color, Color.white, 0.35f);
                _sweetSpot.enabled = false;
                _shield.Rect.gameObject.SetActive(false);
                _help.text = Loc.T(player.ReviveProgress > 0f ? "coop.reviving" : "coop.help", ("player", CoopRules.Tag(player.PlayerIndex)));
                _help.color = Color.Lerp(color, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
                _group.alpha = _alpha = 1f;
                return;
            }

            var status = player.Primary.Status;
            _ring.fillAmount = status.Fill;
            _ring.color = status.Warning ? CombatHud.WarningColor : CombatHud.WeaponColor;
            var window = player.ReloadWindow;
            _sweetSpot.enabled = window.HasValue;
            if (window.HasValue)
            {
                // The sweet spot of an active reload, on the ring where the reload progress will pass it.
                _sweetSpot.fillAmount = window.Value.y - window.Value.x;
                _sweetSpot.rectTransform.localEulerAngles = new Vector3(0f, 0f, window.Value.x * 360f);
            }

            var vitals = player.Vitals;
            float hull = vitals.Hull / vitals.MaxHull;
            _hull.Set(hull);
            _shield.Rect.gameObject.SetActive(vitals.MaxShield > 0f);
            _shield.Set(vitals.MaxShield > 0f ? vitals.Shield / vitals.MaxShield : 0f);

            bool matters = status.Warning || status.Charge > 0f || status.Fill < 0.3f || hull < 0.35f || Time.unscaledTime - _lastHitTime < 1.5f;
            _alpha = Mathf.MoveTowards(_alpha, matters ? 1f : 0.45f, Time.unscaledDeltaTime * 4f);
            _group.alpha = _alpha;
        }

        void Subscribe(PlayerShip player)
        {
            if (player == _subscribed)
                return;
            if (_subscribed != null)
                _subscribed.Damaged -= OnDamaged;
            _subscribed = player;
            if (_subscribed != null)
                _subscribed.Damaged += OnDamaged;
        }

        void OnDamaged(CombatEntity entity, DamageInfo info, DamageResult result)
        {
            if (result.TotalDamage > 0f)
                _lastHitTime = Time.unscaledTime;
        }

        public void Release() => Subscribe(null);
    }
}

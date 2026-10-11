using System.Text;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// A pilot's corner panel in the HUD: hull and shield, primary and secondary weapons, dash charges, and (alone) the
    /// ship's trait and any equipment without a picture. In local co-op (M8) each pilot has one, tagged "P1", "P2"… in
    /// their colour: player 1 top left, player 2 top right, players 3 and 4 below them. Right-hand panels mirror the left.
    /// </summary>
    public sealed class PilotPanel
    {
        const int MaxDashPips = 4;
        const float Width = 380f;

        readonly RectTransform _root;
        readonly bool _right;
        readonly UiBar _hull, _shield, _primary, _secondary;
        readonly UiBar[] _dash = new UiBar[MaxDashPips];
        readonly Text _hullText, _primaryText, _secondaryText, _loadoutText, _tag;
        readonly StringBuilder _builder = new StringBuilder();

        public GameObject Root => _root.gameObject;

        /// <summary>"P2" beside the hull bar in co-op. For tests.</summary>
        public string Tag => _tag.text;

        public PilotPanel(Transform parent, string name, bool right, float top)
        {
            _right = right;
            _root = UiKit.Node(name, parent);
            var corner = new Vector2(right ? 1f : 0f, 1f);
            UiKit.Place(_root, corner, corner, new Vector2(right ? -40f : 40f, top), new Vector2(560f, 520f));

            _shield = Bar("Shield", CombatHud.ShieldColor, 0f, 8f);
            _hull = Bar("Hull", CombatHud.HullColor, 12f, 30f);
            _hullText = UiKit.Text(_hull.Rect, "Label", string.Empty, 18, Color.white, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, FontStyle.Bold);
            var label = UiKit.Stretch(_hullText.rectTransform);
            label.offsetMin = new Vector2(12f, 0f);
            label.offsetMax = new Vector2(-12f, 0f);
            CombatHud.AddShadow(_hullText);

            _tag = UiKit.Text(_root, "Player", string.Empty, 30, Color.white, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(_tag.rectTransform, corner, corner, new Vector2(right ? -(Width + 12f) : Width + 12f, -4f), new Vector2(80f, 44f));
            CombatHud.AddShadow(_tag);

            _primaryText = Label("Primary", 50f, CombatHud.WeaponColor);
            _primary = Bar("Primary Gauge", CombatHud.WeaponColor, 76f, 6f);
            _secondaryText = Label("Secondary", 88f, CombatHud.WeaponColor);
            _secondary = Bar("Secondary Gauge", CombatHud.WeaponColor, 114f, 6f);
            for (int i = 0; i < MaxDashPips; i++)
            {
                _dash[i] = new UiBar(_root, $"Dash {i + 1}", CombatHud.DashColor);
                UiKit.Place(_dash[i].Rect, corner, corner, new Vector2((right ? -1f : 1f) * i * 52f, -128f), new Vector2(46f, 10f));
            }
            _loadoutText = Label("Loadout", 150f, UiKit.TextBody);
            _loadoutText.rectTransform.sizeDelta = new Vector2(560f, 360f);
        }

        public void Refresh(PlayerShip player, bool coop)
        {
            var vitals = player.Vitals;
            _tag.text = coop ? CoopRules.Tag(player.PlayerIndex) : string.Empty;
            _tag.color = CoopRules.PilotColor(player.PlayerIndex);
            _hull.Set(vitals.Hull / vitals.MaxHull);
            _shield.Rect.gameObject.SetActive(vitals.MaxShield > 0f);
            _shield.Set(vitals.MaxShield > 0f ? vitals.Shield / vitals.MaxShield : 0f);
            _hullText.text = player.IsAlive
                ? Loc.T("hud.hull", ("hull", Mathf.CeilToInt(vitals.Hull)), ("max", Mathf.RoundToInt(vitals.MaxHull)),
                    ("shield", Mathf.CeilToInt(vitals.Shield)), ("god", player.GodMode ? Loc.T("hud.god") : string.Empty))
                : Loc.T("coop.panel_down", ("percent", Mathf.RoundToInt(player.ReviveProgress * 100f)));

            // Primary: name, state, and a gauge for ammo, reload or charge.
            var status = player.Primary.Status;
            _primaryText.text = $"{player.PrimaryWeapon.DisplayName.ToUpperInvariant()}   {status.Label}";
            _primary.Set(status.Fill, status.Warning ? CombatHud.WarningColor : CombatHud.WeaponColor);

            bool hasSecondary = player.SecondaryWeapon != null;
            _secondaryText.gameObject.SetActive(hasSecondary);
            _secondary.Rect.gameObject.SetActive(hasSecondary);
            if (hasSecondary)
            {
                _secondaryText.text = CombatHud.SecondaryLabel(player);
                // The gauge fills as the next charge comes back; full only when every charge is (playtest 5).
                var cooldown = player.SecondaryCooldown;
                bool full = cooldown.Charges >= cooldown.MaxCharges;
                _secondary.Set(full ? 1f : cooldown.RechargeProgress, cooldown.IsReady ? CombatHud.WeaponColor : CombatHud.WarningColor);
            }

            var dash = player.Dash;
            for (int i = 0; i < MaxDashPips; i++)
            {
                bool shown = i < dash.MaxCharges;
                _dash[i].Rect.gameObject.SetActive(shown);
                if (shown)
                    _dash[i].Set(i < dash.Charges ? 1f : i == dash.Charges ? dash.RechargeProgress : 0f);
            }

            // Ship trait; the equipped items are on the ship diagram (playtest 7). An item without a picture is listed here
            // by name. In co-op the panels stay compact.
            _loadoutText.gameObject.SetActive(!coop);
            if (coop)
                return;
            _builder.Clear();
            if (!string.IsNullOrEmpty(player.Definition.TraitTitle))
                _builder.Append(player.Definition.DisplayName.ToUpperInvariant()).Append("   ·   ").Append(player.Definition.TraitTitle).Append('\n');
            foreach (var entry in player.Equipment.All)
            {
                if (entry.Item.Icon != null)
                    continue;
                _builder.Append("<color=#").Append(UiKit.Hex(EquipmentChoiceScreen.RarityColor(entry.Item.Rarity))).Append('>')
                    .Append(entry.Item.DisplayName).Append(' ').Append(EquipmentChoiceScreen.Roman(entry.Level)).Append("</color>\n");
            }
            _loadoutText.text = _builder.ToString();
        }

        UiBar Bar(string name, Color color, float top, float height)
        {
            var bar = new UiBar(_root, name, color);
            var corner = new Vector2(_right ? 1f : 0f, 1f);
            UiKit.Place(bar.Rect, corner, corner, new Vector2(0f, -top), new Vector2(Width, height));
            return bar;
        }

        Text Label(string name, float top, Color color)
        {
            var corner = new Vector2(_right ? 1f : 0f, 1f);
            var text = UiKit.Text(_root, name, string.Empty, 18, color, _right ? TextAnchor.UpperRight : TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Place(text.rectTransform, corner, corner, new Vector2(0f, -top), new Vector2(560f, 26f));
            CombatHud.AddShadow(text);
            return text;
        }
    }
}

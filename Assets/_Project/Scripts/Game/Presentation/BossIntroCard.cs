using Pathfinder.Game.Audio;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Presentation.Ui;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// A boss's entrance (M7): the fight holds, the camera turns to the boss, and a card slides across the screen with its
    /// name, its title and how to hurt it; then the camera returns and the fight begins. Any confirm or back press skips it.
    /// Built by the HUD on its own canvas.
    /// </summary>
    public sealed class BossIntroCard
    {
        const float SlideIn = 0.35f, Hold = 2f, SlideOut = 0.3f;
        static readonly Color BossRed = new Color(1f, 0.3f, 0.25f);

        readonly GameObject _canvas;
        readonly RectTransform _card;
        readonly Text _name, _title, _hint;
        readonly MenuInput _input = new MenuInput();
        Enemy _boss;
        float _time;

        public bool IsPlaying => _boss != null;

        public BossIntroCard(Transform parent, Sprite skull)
        {
            var canvas = UiKit.Canvas("Boss Intro", parent, sortingOrder: 28);
            _canvas = canvas.gameObject;
            var root = canvas.transform;

            var card = UiKit.Image(root, "Card", new Color(0.02f, 0.03f, 0.06f, 0.92f));
            _card = card.rectTransform;
            UiKit.Place(_card, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 190f));
            foreach (float y in new[] { 0f, 1f })
            {
                var stripe = UiKit.Image(_card, "Stripe", BossRed);
                UiKit.Place(stripe.rectTransform, new Vector2(0.5f, y), new Vector2(0.5f, y), Vector2.zero, new Vector2(1500f, 5f));
            }

            var badge = UiKit.Image(_card, "Badge", new Color(BossRed.r, BossRed.g, BossRed.b, 0.25f), UiKit.Circle);
            UiKit.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(150f, 150f));
            if (skull != null)
            {
                var icon = UiKit.Image(badge.transform, "Skull", BossRed, skull);
                icon.preserveAspect = true;
                UiKit.Stretch(icon.rectTransform, 24f);
            }

            _name = UiKit.Text(_card, "Name", string.Empty, 60, Color.white, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Place(_name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(240f, 6f), new Vector2(1200f, 76f));
            _title = UiKit.Text(_card, "Title", string.Empty, 26, BossRed, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Place(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(242f, 0f), new Vector2(1200f, 34f));
            _hint = UiKit.Text(_card, "Hint", string.Empty, 21, UiKit.TextBody, TextAnchor.UpperLeft);
            UiKit.Place(_hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(242f, -38f), new Vector2(1200f, 30f));
            foreach (var text in new[] { _name, _title, _hint })
                text.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            _canvas.SetActive(false);
        }

        public void Play(Enemy boss)
        {
            Stop();
            _boss = boss;
            _time = 0f;
            string id = boss.Definition.Id;
            _name.text = boss.Definition.DisplayName.ToUpperInvariant();
            _title.text = Loc.TOr($"{id}.title", string.Empty);
            _hint.text = Loc.TOr($"{id}.hint", string.Empty);
            _canvas.SetActive(true);
            GameClock.Pause(this);
            ModalScreens.SetOpen(this, true); // Esc skips the card instead of opening the pause menu
            GameAudio.Play(SoundCue.Roar);
            Update();
        }

        public void Update()
        {
            if (_boss == null)
                return;
            if (_time > SlideIn && (_input.Submitted || _input.Cancelled))
                _time = Mathf.Max(_time, SlideIn + Hold);

            _time += Time.deltaTime; // the game never scales time; tests do, to run faster
            CameraRig.Spotlight = _boss != null ? _boss.transform.position : (Vector3?)null;

            float x;
            if (_time < SlideIn)
                x = Mathf.Lerp(-1900f, 0f, EaseOut(_time / SlideIn));
            else if (_time < SlideIn + Hold)
                x = 0f;
            else
                x = Mathf.Lerp(0f, 1900f, (_time - SlideIn - Hold) / SlideOut);
            _card.anchoredPosition = new Vector2(x, 0f);

            if (_time >= SlideIn + Hold + SlideOut)
                Stop();
        }

        public void Stop()
        {
            if (_boss == null && !_canvas.activeSelf)
                return;
            _boss = null;
            _canvas.SetActive(false);
            CameraRig.Spotlight = null;
            GameClock.Resume(this);
            ModalScreens.SetOpen(this, false);
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    }
}

using Pathfinder.Game.Enemies;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// A Warden core's look: its rings spin fast while it is online and wind down when it goes offline, and its glowing
    /// parts go dark. The Alpha also draws the tether to its twin: a crackling line that flickers thin while relinking.
    /// Presentation only.
    /// </summary>
    public sealed class WardenVisual : MonoBehaviour
    {
        [SerializeField] WardenBrain brain;
        [SerializeField, Tooltip("Hidden while the core is offline.")] GameObject[] poweredParts = new GameObject[0];
        [SerializeField] Transform spinner;
        [SerializeField] float onlineSpin = 140f;
        [SerializeField] float offlineSpin = 15f;

        [Header("Tether (Alpha)")]
        [SerializeField, Tooltip("Optional. Only the Alpha has one.")] LineRenderer tether;
        [SerializeField, Min(2)] int tetherPoints = 28;
        [SerializeField, Min(0f)] float tetherWidth = 0.4f;
        [SerializeField, Min(0f)] float tetherWave = 0.15f;
        [SerializeField] float tetherHeight = 0.5f;

        float _spin;

        void Awake()
        {
            if (tether != null)
            {
                tether.useWorldSpace = true;
                tether.positionCount = tetherPoints;
                tether.enabled = false;
            }
        }

        void LateUpdate()
        {
            bool online = !brain.IsOffline;
            foreach (var part in poweredParts)
            {
                if (part != null && part.activeSelf != online)
                    part.SetActive(online);
            }

            _spin = Mathf.MoveTowards(_spin, online ? onlineSpin : offlineSpin, Time.deltaTime * 300f);
            if (spinner != null)
                spinner.Rotate(0f, _spin * Time.deltaTime, 0f, Space.Self);

            UpdateTether();
        }

        void UpdateTether()
        {
            if (tether == null)
                return;
            var twin = brain.TwinEnemy;
            bool shown = brain.TetherShown && twin != null;
            bool relinking = shown && !brain.TetherActive;
            tether.enabled = shown && (!relinking || Mathf.Repeat(Time.time * 12f, 1f) < 0.5f);
            if (!tether.enabled)
                return;

            Vector3 from = transform.position + Vector3.up * tetherHeight;
            Vector3 to = twin.transform.position + Vector3.up * tetherHeight;
            Vector3 path = to - from;
            Vector3 side = Vector3.Cross(Vector3.up, path.normalized);
            tether.widthMultiplier = relinking ? tetherWidth * 0.3f : tetherWidth;
            for (int i = 0; i < tetherPoints; i++)
            {
                float t = i / (tetherPoints - 1f);
                // A standing wave that travels along the line, pinned at both cores.
                float wave = Mathf.Sin(t * Mathf.PI) * Mathf.Sin(t * 18f - Time.time * 14f) * tetherWave;
                tether.SetPosition(i, from + path * t + side * wave);
            }
        }
    }
}

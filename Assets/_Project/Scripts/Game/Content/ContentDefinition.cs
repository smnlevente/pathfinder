using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>
    /// Base for all designer-authored content. Saves and (later) network messages refer to content by
    /// <see cref="Id"/>, never by object reference or list index (GDD §14.4).
    /// </summary>
    public abstract class ContentDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable identifier used by saves and network messages. Never change it after release.")]
        string id;

        [SerializeField] string displayName;

        [SerializeField, Tooltip("Optional picture shown beside the name in menus, cards and the HUD. White on transparent; the UI tints it.")]
        Sprite icon;

        public string Id => id;

        /// <summary>The content's picture, or null (playtest 7: fewer words, more pictures).</summary>
        public Sprite Icon => icon;

        /// <summary>The localized name (<c>{id}.name</c> in the .loc files), or the name authored on the asset.</summary>
        public string DisplayName => Localization.Loc.TOr($"{id}.name", string.IsNullOrEmpty(displayName) ? name : displayName);

        /// <summary>Default ID for an asset name: "Ship_Stormbringer" → "ship.stormbringer".</summary>
        public static string ToId(string assetName) => assetName.Trim().ToLowerInvariant().Replace('_', '.').Replace(' ', '.');

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                id = ToId(name);
        }
#endif
    }
}

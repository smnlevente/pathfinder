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

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

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

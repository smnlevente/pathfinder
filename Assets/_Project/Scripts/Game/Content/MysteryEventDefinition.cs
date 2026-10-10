using System;
using Pathfinder.Core.Equipment;
using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>
    /// A Mystery event (GDD §10.5): a short scene with two or three choices, each trading something for something else.
    /// The display name is the event's title.
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Mystery Event", fileName = "Event_")]
    public sealed class MysteryEventDefinition : ContentDefinition
    {
        [field: SerializeField, TextArea(3, 8)] public string Body { get; private set; }
        [field: SerializeField] public EventChoice[] Choices { get; private set; } = Array.Empty<EventChoice>();
    }

    public enum EventRequirement
    {
        None,

        /// <summary>The pilot needs at least <see cref="EventChoice.RequiredScraps"/>.</summary>
        Scraps,

        /// <summary>The pilot has at least one equipment item.</summary>
        AnyItem,

        /// <summary>The pilot has an item below level III.</summary>
        UpgradeableItem,
    }

    public enum EventOutcomeKind
    {
        /// <summary>Gain (or, negative, lose) Scraps. Losses take what the pilot has.</summary>
        Scraps,

        /// <summary>Heal (or, negative, lose) hull. Losses skip the shield but never go below 1 hull.</summary>
        Hull,

        /// <summary>Heal a percentage of max hull.</summary>
        HullPercent,

        /// <summary>Raise (or, negative, lower) max hull for the rest of the run.</summary>
        MaxHull,

        DarkMatter,

        /// <summary>An equipment choice of 3, at least <see cref="EventOutcome.Rarity"/>.</summary>
        Equipment,

        UpgradeRandomItem,
        LoseRandomItem,

        /// <summary>A fight: waves at the ambush budget, with double rewards.</summary>
        Ambush,

        /// <summary>A percentage change to <see cref="EventOutcome.Stat"/> for the rest of the run.</summary>
        StatPercent,
    }

    [Serializable]
    public sealed class EventChoice
    {
        public string Label;
        public EventRequirement Requirement;
        public int RequiredScraps;

        [Range(0f, 1f), Tooltip("1 = certain. Otherwise the fail outcomes happen instead.")]
        public float Chance = 1f;

        public EventOutcome[] Outcomes = Array.Empty<EventOutcome>();
        public EventOutcome[] FailOutcomes = Array.Empty<EventOutcome>();
        [TextArea(2, 4)] public string ResultText;
        [TextArea(2, 4)] public string FailText;
    }

    [Serializable]
    public struct EventOutcome
    {
        public EventOutcomeKind Kind;
        public float Amount;
        public Rarity Rarity;
        public StatId Stat;

        public EventOutcome(EventOutcomeKind kind, float amount = 0f, Rarity rarity = Rarity.Common, StatId stat = StatId.MaxHull)
        {
            Kind = kind;
            Amount = amount;
            Rarity = rarity;
            Stat = stat;
        }
    }
}

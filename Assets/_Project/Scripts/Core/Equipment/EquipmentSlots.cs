using System;
using System.Collections.Generic;

namespace Pathfinder.Core.Equipment
{
    /// <summary>The four equipment categories, each with its own slots (GDD §8.1).</summary>
    public enum EquipmentCategory
    {
        Dash,
        Weapon,
        Secondary,
        Hull,
    }

    public enum Rarity
    {
        Common,
        Rare,
        Epic,
    }

    public enum EquipOutcome
    {
        /// <summary>Placed in a free slot at level I.</summary>
        Equipped,

        /// <summary>Already equipped: a duplicate raises its level for free (GDD §8.1).</summary>
        LeveledUp,

        /// <summary>Already equipped at the maximum level.</summary>
        AlreadyMaxed,

        /// <summary>The category is full. The player must swap an item out or skip.</summary>
        NeedsSwap,
    }

    public static class EquipmentRules
    {
        public const int MaxLevel = 3;

        /// <summary>Default slots: Dash 1, Weapon 2, Secondary 1, Hull 3 (GDD §8.1).</summary>
        public static int DefaultCapacity(EquipmentCategory category) => category switch
        {
            EquipmentCategory.Dash => 1,
            EquipmentCategory.Weapon => 2,
            EquipmentCategory.Secondary => 1,
            _ => 3,
        };
    }

    /// <summary>
    /// A pilot's equipped items by category, with levels I–III. Items are compared by reference, so the
    /// game uses its content definitions and tests can use plain objects.
    /// </summary>
    public sealed class EquipmentSlots<T> where T : class
    {
        public sealed class Entry
        {
            public T Item { get; internal set; }
            public int Level { get; internal set; }
            public EquipmentCategory Category { get; internal set; }
        }

        readonly Dictionary<EquipmentCategory, List<Entry>> _slots = new Dictionary<EquipmentCategory, List<Entry>>();
        readonly Dictionary<EquipmentCategory, int> _capacity = new Dictionary<EquipmentCategory, int>();

        public EquipmentSlots()
        {
            foreach (EquipmentCategory category in Enum.GetValues(typeof(EquipmentCategory)))
            {
                _slots[category] = new List<Entry>();
                _capacity[category] = EquipmentRules.DefaultCapacity(category);
            }
        }

        public IReadOnlyList<Entry> In(EquipmentCategory category) => _slots[category];

        public IEnumerable<Entry> All
        {
            get
            {
                foreach (var list in _slots.Values)
                {
                    foreach (var entry in list)
                        yield return entry;
                }
            }
        }

        public int Capacity(EquipmentCategory category) => _capacity[category];

        /// <summary>Changes the slot count, e.g. a second Dash Module slot from the Hub (GDD §11.2).</summary>
        public void SetCapacity(EquipmentCategory category, int capacity) => _capacity[category] = Math.Max(0, capacity);

        public Entry Find(T item)
        {
            foreach (var list in _slots.Values)
            {
                foreach (var entry in list)
                {
                    if (ReferenceEquals(entry.Item, item))
                        return entry;
                }
            }
            return null;
        }

        /// <summary>The outcome <see cref="TryAdd"/> would have, without changing anything.</summary>
        public EquipOutcome Preview(T item, EquipmentCategory category)
        {
            var existing = Find(item);
            if (existing != null)
                return existing.Level < EquipmentRules.MaxLevel ? EquipOutcome.LeveledUp : EquipOutcome.AlreadyMaxed;
            return _slots[category].Count < _capacity[category] ? EquipOutcome.Equipped : EquipOutcome.NeedsSwap;
        }

        public EquipOutcome TryAdd(T item, EquipmentCategory category)
        {
            var outcome = Preview(item, category);
            switch (outcome)
            {
                case EquipOutcome.LeveledUp:
                    Find(item).Level++;
                    break;
                case EquipOutcome.Equipped:
                    _slots[category].Add(new Entry { Item = item, Level = 1, Category = category });
                    break;
            }
            return outcome;
        }

        /// <summary>Replaces the item in a slot with a new one at level I. The old item is lost (GDD §8.1).</summary>
        public void Replace(EquipmentCategory category, int index, T item)
        {
            var list = _slots[category];
            if (index < 0 || index >= list.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (Find(item) != null)
                throw new InvalidOperationException("The item is already equipped. Level it up instead.");

            list[index] = new Entry { Item = item, Level = 1, Category = category };
        }

        /// <summary>Removes one item from its slot, e.g. traded away in a Mystery event. Returns false if it isn't equipped.</summary>
        public bool Remove(T item)
        {
            foreach (var list in _slots.Values)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(list[i].Item, item))
                    {
                        list.RemoveAt(i);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Removes every item, e.g. at the start of a new run.</summary>
        public void Clear()
        {
            foreach (var list in _slots.Values)
                list.Clear();
        }

        /// <summary>Raises an equipped item's level, e.g. a Shop upgrade. Fails at the maximum level.</summary>
        public bool LevelUp(T item)
        {
            var entry = Find(item);
            if (entry == null || entry.Level >= EquipmentRules.MaxLevel)
                return false;
            entry.Level++;
            return true;
        }
    }
}

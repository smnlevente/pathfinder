using System.Collections.Generic;
using NUnit.Framework;
using Pathfinder.Game.Content;
using UnityEditor;

namespace Pathfinder.Tests.Game
{
    /// <summary>Saves and network messages reference content by ID, so IDs must exist and be unique (GDD §14.4).</summary>
    public class ContentDefinitionTests
    {
        [Test]
        public void AllDefinitions_HaveUniqueNonEmptyIds()
        {
            var seen = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ContentDefinition)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<ContentDefinition>(path);

                Assert.IsFalse(string.IsNullOrWhiteSpace(definition.Id), $"{path} has no ID.");
                if (seen.TryGetValue(definition.Id, out string other))
                    Assert.Fail($"Duplicate ID '{definition.Id}' in {path} and {other}.");
                seen[definition.Id] = path;
            }

            Assert.That(seen.Count, Is.GreaterThan(0), "No content definitions found.");
        }

        [Test]
        public void ToId_NormalisesAssetNames()
        {
            Assert.AreEqual("ship.stormbringer", ContentDefinition.ToId("Ship_Stormbringer"));
            Assert.AreEqual("enemy.big.drone", ContentDefinition.ToId(" Enemy_Big Drone "));
        }
    }
}

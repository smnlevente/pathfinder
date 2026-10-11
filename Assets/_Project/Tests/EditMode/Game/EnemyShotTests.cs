using System.Collections.Generic;
using NUnit.Framework;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using UnityEditor;
using UnityEngine;

namespace Pathfinder.Tests.Game
{
    /// <summary>Playtest 7: "everything is a ball". Every shooter fires its own kind of shot.</summary>
    public class EnemyShotTests
    {
        [Test]
        public void EveryShooter_HasItsOwnShot()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Data/ContentDatabase.asset");
            GameObject Prefab(string id) => database.FindEnemy(id).Prefab.gameObject;
            var shots = new Dictionary<string, GameObject>
            {
                ["Gunner"] = Prefab("enemy.gunner").GetComponent<GunnerBrain>().BulletPrefab,
                ["Spinner"] = Prefab("enemy.spinner").GetComponent<SpinnerBrain>().BulletPrefab,
                ["Quarry Maw"] = Prefab("boss.quarry-maw").GetComponent<QuarryMawBrain>().BulletPrefab,
                ["Binary Wardens"] = Prefab("boss.binary-wardens").GetComponent<WardenBrain>().BulletPrefab,
                ["Hive Matriarch"] = Prefab("boss.hive-matriarch").GetComponent<MatriarchBrain>().BulletPrefab,
            };

            var seen = new HashSet<GameObject>();
            foreach (var pair in shots)
            {
                Assert.IsNotNull(pair.Value, $"{pair.Key} has a shot.");
                Assert.AreNotEqual("PV_EnemyBullet", pair.Value.name, $"{pair.Key} fires its own shot, not the generic orb.");
                Assert.IsTrue(seen.Add(pair.Value), $"{pair.Key}'s shot is its own.");
            }
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pathfinder.Game.Boot
{
    /// <summary>
    /// Entry point of the Boot scene. It will initialise persistent services (save data, MetaState,
    /// audio) as they are added, then hand over to the first gameplay scene.
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        [SerializeField] string firstScene = SceneNames.Hub;

        void Start()
        {
            SceneManager.LoadScene(firstScene);
        }
    }
}

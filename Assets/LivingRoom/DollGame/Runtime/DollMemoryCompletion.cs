using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace TeamRoulette.Room3.DollMemory
{
    public sealed class DollMemoryCompletion : MonoBehaviour
    {
        public static bool Pending;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { Pending=false; SceneManager.sceneLoaded-=Loaded; SceneManager.sceneLoaded+=Loaded; }
        static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(!Pending || scene.name!=Room3FlowController.LivingRoom) return;
            Pending=false; new GameObject("Doll memory completion").AddComponent<DollMemoryCompletion>().StartCoroutine(Apply(scene));
        }
        static IEnumerator Apply(Scene scene)
        {
            yield return null;
            if(GameManager.Instance) { GameManager.Instance.RecordToyMemoryCompletion(DollMemorySequence.ToyId); GameManager.Instance.Flow.PrepareScene(scene); }
        }
    }
}

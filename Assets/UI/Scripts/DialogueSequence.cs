using UnityEngine;
using UnityEngine.Events;

namespace TeamRoulette.UI
{
    [CreateAssetMenu(menuName = "Team Roulette/UI/독백", fileName = "Dialogue")]
    public class DialogueSequence : ScriptableObject
    {
        [TextArea(2, 5)] public string[] lines;
    }
}

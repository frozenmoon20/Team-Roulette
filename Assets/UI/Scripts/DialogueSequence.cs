using UnityEngine;
using UnityEngine.Events;

namespace TeamRoulette.UI
{
    [CreateAssetMenu(menuName = "Team Roulette/UI/독백", fileName = "Dialogue")]
    public class DialogueSequence : ScriptableObject
    {
        [TextArea(2, 5)] public string[] lines;
        [HideInInspector] public string[] colorCodes;

        public Color GetLineColor(int index)
        {
            if (colorCodes != null && index >= 0 && index < colorCodes.Length &&
                ColorUtility.TryParseHtmlString(colorCodes[index], out var value)) return value;
            return Color.white;
        }
    }
}

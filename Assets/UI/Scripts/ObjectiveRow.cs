using TMPro;
using UnityEngine;

namespace TeamRoulette.UI
{
    public class ObjectiveRow : MonoBehaviour
    {
        public TMP_Text label;
        public GameObject check;
        public void Set(string text, bool completed)
        {
            label.text = text;
            label.fontStyle = completed ? FontStyles.Strikethrough : FontStyles.Normal;
            label.color = completed ? new Color(.53f, .55f, .57f) : new Color(.94f, .94f, .94f);
            check.SetActive(completed);
        }
    }
}

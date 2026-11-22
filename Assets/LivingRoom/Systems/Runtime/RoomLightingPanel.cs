using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomLightingPanel : MonoBehaviour
{
    public enum SettingsPage { Environment, GlobalIllumination }
    [SerializeField, HideInInspector] SettingsPage page;
    public SettingsPage Page => page;
}

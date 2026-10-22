using UnityEngine;
using UnityEngine.InputSystem;

namespace SousakuTeam8.MeterGame.Timing
{
    public sealed class TimingSpaceInput : MonoBehaviour
    {
        // isPressed is intentionally not used: holding Space cannot win following sets.
        public bool PressedThisFrame => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }
}

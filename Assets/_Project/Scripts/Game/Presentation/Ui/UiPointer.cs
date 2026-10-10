using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>Mouse hover and click on a UI element. Needs a raycast-target graphic on the same object.</summary>
    public sealed class UiPointer : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public event Action Entered;
        public event Action Clicked;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Audio.GameAudio.Play(Audio.SoundCue.UiMove);
            Entered?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            Audio.GameAudio.Play(Audio.SoundCue.UiConfirm);
            Clicked?.Invoke();
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>Mouse hover, click and right click on a UI element. Needs a raycast-target graphic on the same object.</summary>
    public sealed class UiPointer : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public event Action Entered;
        public event Action Clicked;

        /// <summary>A right click, e.g. to step an option down.</summary>
        public event Action RightClicked;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Audio.GameAudio.Play(Audio.SoundCue.UiMove);
            Entered?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (RightClicked == null)
                    return;
                Audio.GameAudio.Play(Audio.SoundCue.UiBack);
                RightClicked.Invoke();
                return;
            }
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            Audio.GameAudio.Play(Audio.SoundCue.UiConfirm);
            Clicked?.Invoke();
        }
    }
}

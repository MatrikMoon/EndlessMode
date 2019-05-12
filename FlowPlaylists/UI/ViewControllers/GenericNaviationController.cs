using CustomUI.BeatSaber;
using System;
using UnityEngine.UI;
using VRUI;

namespace FlowPlaylists.UI.ViewControllers
{
    class GenericNavigationController : VRUINavigationController
    {
        private Button _backButton;
        public event Action<GenericNavigationController> didFinishEvent;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _backButton = BeatSaberUI.CreateBackButton(rectTransform, () => didFinishEvent?.Invoke(this));
            }
        }
    }
}

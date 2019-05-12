using CustomUI.BeatSaber;
using FlowPlaylists.UI.ViewControllers;
using VRUI;

namespace FlowPlaylists.UI.FlowCoordinators
{
    class FlowPlaylistsFlowCoordinator : FlowCoordinator
    {
        private MainFlowCoordinator mainFlowCoordinator;
        private GenericNavigationController navigationController;
        private CenterViewController centerViewController;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                title = "FlowPlaylists";

                navigationController = BeatSaberUI.CreateViewController<GenericNavigationController>();
                navigationController.didFinishEvent += (_) => mainFlowCoordinator.InvokeMethod("DismissFlowCoordinator", this, null, false);

                if (centerViewController == null) centerViewController = BeatSaberUI.CreateViewController<CenterViewController>();

                ProvideInitialViewControllers(navigationController);
                SetViewControllersToNavigationConctroller(navigationController, new VRUIViewController[] { centerViewController });
            }
        }

        public void PresentUI(MainFlowCoordinator mainFlowCoordinator)
        {
            this.mainFlowCoordinator = mainFlowCoordinator;
            mainFlowCoordinator.InvokeMethod("PresentFlowCoordinatorOrAskForTutorial", this);
        }
    }
}

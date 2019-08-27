using CustomUI.BeatSaber;
using EndlessMode.Misc;
using EndlessMode.UI.ViewControllers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRUI;
using Logger = EndlessMode.Misc.Logger;

namespace EndlessMode.UI.FlowCoordinators
{
    class EndlessModeFlowCoordinator : FlowCoordinator
    {
        private IDifficultyBeatmap currentMap;
        private MainFlowCoordinator mainFlowCoordinator;
        private GenericNavigationController navigationController;
        private CenterViewController centerViewController;

        private SoloFreePlayFlowCoordinator soloFreePlayFlowCoordinator;
        private AlwaysOwnedContentModelSO _alwaysOwnedContentModelSO;
        private BeatmapLevelCollectionSO _primaryLevelCollection;
        private BeatmapLevelCollectionSO _secondaryLevelCollection;
        private BeatmapLevelCollectionSO _extrasLevelCollection;
        private IBeatmapLevelPackCollection beatmapLevelPackCollection;

        private static System.Random rand = new System.Random();

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (activationType == ActivationType.AddedToHierarchy)
            {
                title = Plugin.Name;

                navigationController = BeatSaberUI.CreateViewController<GenericNavigationController>();
                navigationController.didFinishEvent += (_) => mainFlowCoordinator.InvokeMethod("DismissFlowCoordinator", this, null, false);

                if (soloFreePlayFlowCoordinator == null) soloFreePlayFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                if (_alwaysOwnedContentModelSO == null) _alwaysOwnedContentModelSO = Resources.FindObjectsOfTypeAll<AlwaysOwnedContentModelSO>().First();
                if (_primaryLevelCollection == null) _primaryLevelCollection = _alwaysOwnedContentModelSO.alwaysOwnedPacks.First(x => x.packID == "OstVol1").beatmapLevelCollection as BeatmapLevelCollectionSO;
                if (_secondaryLevelCollection == null) _secondaryLevelCollection = _alwaysOwnedContentModelSO.alwaysOwnedPacks.First(x => x.packID == "OstVol2").beatmapLevelCollection as BeatmapLevelCollectionSO;
                if (_extrasLevelCollection == null) _extrasLevelCollection = _alwaysOwnedContentModelSO.alwaysOwnedPacks.First(x => x.packID == "Extras").beatmapLevelCollection as BeatmapLevelCollectionSO;
                if (beatmapLevelPackCollection == null) beatmapLevelPackCollection = soloFreePlayFlowCoordinator.GetField<IBeatmapLevelPackCollection>("_levelPackCollection");
                if (centerViewController == null)
                {
                    centerViewController = BeatSaberUI.CreateViewController<CenterViewController>();
                    centerViewController.GenerateButtonPressed += () =>
                    {
                        centerViewController.SetUIType(CenterViewController.UIType.ProgressBar);
                        GeneratePlaylistWithMinTime(centerViewController.GetTimeValue(), centerViewController.UseOnlyPreferredDifficulty ? centerViewController.PreferredDifficulty : (BeatmapDifficulty?)null, async (playlist) =>
                        {
                            var duration = 0f;
                            foreach (var song in playlist)
                            {
                                duration += song.songDuration;
                                Logger.Debug($"PLAYLIST ITEM: {song.songName} ({song.songDuration})");
                            }
                            Logger.Debug($"TOTAL DURATION: {duration}");

                            centerViewController.SetUIType(CenterViewController.UIType.GenerationButton);

                            //Launch first level
                            Config.Enabled = true;
                            Plugin.instance.loadedLevels = new Queue<IPreviewBeatmapLevel>(playlist);

                            //Ensure the first level is an IBeatmapLevel
                            var firstBeatmapLevel = Plugin.instance.loadedLevels.First();
                            if (firstBeatmapLevel is CustomPreviewBeatmapLevel)
                            {
                                var result = await SongHelpers.GetLevelFromPreview(firstBeatmapLevel);
                                if (result != null && !(result?.isError == true))
                                {
                                    firstBeatmapLevel = result?.beatmapLevel;
                                }
                            }

                            var firstMap = SongHelpers.GetClosestDifficultyPreferLower(firstBeatmapLevel as IBeatmapLevel, centerViewController.PreferredDifficulty);

                            SongStitcher.songSwitched -= SongSwitched;
                            SongStitcher.songSwitched += SongSwitched;

                            var playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
                            MenuTransitionsHelperSO menuTransitionHelper = Resources.FindObjectsOfTypeAll<MenuTransitionsHelperSO>().FirstOrDefault();
                            menuTransitionHelper.StartStandardLevel(firstMap, playerDataModel.currentLocalPlayer.gameplayModifiers, playerDataModel.currentLocalPlayer.playerSpecificSettings, null, "Menu", false, null, SongFinished);
                        });
                    };
                }

                ProvideInitialViewControllers(navigationController);
                SetViewControllersToNavigationConctroller(navigationController, new VRUIViewController[] { centerViewController });
            }
        }

        public void PresentUI(MainFlowCoordinator mainFlowCoordinator)
        {
            this.mainFlowCoordinator = mainFlowCoordinator;
            mainFlowCoordinator.InvokeMethod("PresentFlowCoordinatorOrAskForTutorial", this);
        }

        private void SongSwitched(IDifficultyBeatmap from, IDifficultyBeatmap to)
        {
            currentMap = to;
        }

        private void SongFinished(StandardLevelScenesTransitionSetupDataSO sceneTransitionData, LevelCompletionResults results)
        {
            if (results.levelEndAction != LevelCompletionResults.LevelEndAction.Restart) Config.LoadConfig(); //Reset Enabled status we changed above
            else {
                var playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
                MenuTransitionsHelperSO menuTransitionHelper = Resources.FindObjectsOfTypeAll<MenuTransitionsHelperSO>().FirstOrDefault();
                menuTransitionHelper.StartStandardLevel(currentMap, playerDataModel.currentLocalPlayer.gameplayModifiers, playerDataModel.currentLocalPlayer.playerSpecificSettings, null, "Menu", false, null, SongFinished);
            }
        }

        private void GeneratePlaylistWithMinTime(float minTime, BeatmapDifficulty? difficulty = null, Action<List<IPreviewBeatmapLevel>> playlistLoaded = null)
        {
            var totalDuration = 0f;
            var pickFrom = new List<IPreviewBeatmapLevel>();

            foreach (var pack in beatmapLevelPackCollection.beatmapLevelPacks) pickFrom = pickFrom.Union(pack.beatmapLevelCollection.beatmapLevels).ToList();

            var ret = new List<IPreviewBeatmapLevel>();

            Action addAnotherSong = null;
            addAnotherSong = async () => {
                var currentIndex = rand.Next(0, pickFrom.Count);
                var currentLevel = pickFrom.ElementAt(currentIndex);
                pickFrom.RemoveAt(currentIndex);

                Action<IBeatmapLevel> SongLoaded = (loadedLevel) =>
                {
                    //If a difficulty was specified, we'll only pick songs that have that difficulty
                    //NOTE: We can't filter out maps during the union because this beatmap data might not
                    //be populated until the level is loaded
                    if (difficulty == null ||
                        loadedLevel.beatmapLevelData.difficultyBeatmapSets.Any(x => x.difficultyBeatmaps.Any(y => y.difficulty == difficulty)))
                    {
                        Logger.Debug($"ADDED: {loadedLevel.songName} ({loadedLevel.songDuration}) (Total time: {totalDuration})");
                        totalDuration += loadedLevel.beatmapLevelData.audioClip.length;
                        ret.Add(loadedLevel);

                        centerViewController.SetProgress(totalDuration / minTime);
                    }

                    if (totalDuration < minTime && pickFrom.Count > 0) addAnotherSong();
                    else playlistLoaded(ret);
                };

                if ((currentLevel is PreviewBeatmapLevelSO && await SongHelpers.HasDLCLevel(currentLevel.levelID)) ||
                        currentLevel is CustomPreviewBeatmapLevel)
                {
                    Logger.Debug("Loading DLC/Custom level...");
                    var result = await SongHelpers.GetLevelFromPreview(currentLevel);
                    if (result != null && !(result?.isError == true))
                    {
                        SongLoaded(result?.beatmapLevel);
                    }
                }
                else if (currentLevel is BeatmapLevelSO)
                {
                    Logger.Debug("Reading OST data without songloader...");
                    SongLoaded(currentLevel as IBeatmapLevel);
                }
                else
                {
                    Logger.Debug($"Skipping unowned DLC ({currentLevel.songName})");
                    if (pickFrom.Count > 0) addAnotherSong();
                    else playlistLoaded(ret);
                }
            };

            addAnotherSong();
        }
    }
}

using CustomUI.MenuButton;
using EndlessMode.Misc;
using EndlessMode.UI;
using EndlessMode.UI.FlowCoordinators;
using IPA;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = EndlessMode.Misc.Logger;

namespace EndlessMode
{
    public class Plugin : IBeatSaberPlugin
    {
        public const string Name = "EndlessMode";
        public const string Version = "0.0.8";

        public static Plugin instance;
        public Queue<IPreviewBeatmapLevel> loadedLevels;
        public event Action<Queue<IPreviewBeatmapLevel>> levelsLoaded;

        private EndlessModeFlowCoordinator endlessModeFlowCoordinator;
        private MenuButton menuButton;

        public Plugin() => instance = this;

        public void OnApplicationStart()
        {
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.name == "MenuCore")
            {
                SharedCoroutineStarter.instance.StartCoroutine(SetupUI());
            }
        }

        public void OnSceneUnloaded(Scene scene)
        {
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {
            if (Config.Enabled && nextScene.name == "GameCore")
            {
                var stitcher = new GameObject("SongStitcher").AddComponent<SongStitcher>();
                levelsLoaded += stitcher.LevelsLoaded;

                var panel = CountdownPanel.Create();
                levelsLoaded += panel.LevelsLoaded;

                //If the levels were loaded prior to the scene change,
                //we can go ahead and re-invoke this to make sure our new
                //components have the right information
                if (loadedLevels != null)
                {
                    levelsLoaded?.Invoke(loadedLevels);
                    loadedLevels = null;
                }
            }
        }

        private void SongsLoaded(SongCore.Loader _ , Dictionary<string, CustomPreviewBeatmapLevel> __)
        {
            SongCore.Loader.SongsLoadedEvent -= SongsLoaded;
            if (menuButton != null) menuButton.interactable = true;
        }

        //Waits for menu scenes to be loaded then creates UI elements
        //Courtesy of BeatSaverDownloader
        private IEnumerator SetupUI()
        {
            List<Scene> menuScenes = new List<Scene>() { SceneManager.GetSceneByName("MenuCore"), SceneManager.GetSceneByName("MenuViewControllers"), SceneManager.GetSceneByName("MainMenu") };
            yield return new WaitUntil(() => { return menuScenes.All(x => x.isLoaded); });

            var standardLevelDetailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().FirstOrDefault();
            standardLevelDetailViewController.didPressPlayButtonEvent += didPressPlay;

            var missionLevelDetailViewController = Resources.FindObjectsOfTypeAll<MissionLevelDetailViewController>().FirstOrDefault();
            missionLevelDetailViewController.didPressPlayButtonEvent += didPressMissionPlay;

            var mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();

            if (endlessModeFlowCoordinator == null) endlessModeFlowCoordinator = mainFlowCoordinator.gameObject.AddComponent<EndlessModeFlowCoordinator>();

            var gameOption = CustomUI.GameplaySettings.GameplaySettingsUI.CreateToggleOption(CustomUI.GameplaySettings.GameplaySettingsPanels.ModifiersLeft, $"Enable {Name}", "When you press play, songs after the song you select will be stitched together into one", null, 0f);
            menuButton = MenuButtonUI.AddButton(Name, $"Open {Name} Menu", () => endlessModeFlowCoordinator.PresentUI(mainFlowCoordinator));
            menuButton.interactable = SongCore.Loader.AreSongsLoaded;
            SongCore.Loader.SongsLoadedEvent += SongsLoaded;

            Config.LoadConfig();
            gameOption.GetValue = Config.Enabled;
            gameOption.OnToggle += (b) =>
            {
                Config.Enabled = b;
                Config.SaveConfig();
            };
        }

        private void didPressMissionPlay(MissionLevelDetailViewController standardLevelDetailViewController)
        {
            levelsLoaded?.Invoke(null);
        }

        private async void didPressPlay(StandardLevelDetailViewController standardLevelDetailViewController)
        {
            //Disable score submission, for now
            if (Config.Enabled) BS_Utils.Gameplay.ScoreSubmission.DisableSubmission(Name);

            var currentView = Resources.FindObjectsOfTypeAll<LevelPackLevelsTableView>().First();
            var currentPack = currentView.GetField<IBeatmapLevelPack>("_pack");
            var currentCollection = currentPack.beatmapLevelCollection;

            var newCollection = currentCollection.beatmapLevels.SkipWhile(x => x.levelID != standardLevelDetailViewController.selectedDifficultyBeatmap.level.levelID);

            //If we're dealing with DLC, we have to load all the levels that the user has
            //now, because loading them mid-Update() would require Update to be async,
            //and *that* would cause multiple level loads to be started before the first level load finishes

            var first = newCollection.First();
            var type = first.GetType();
    
            loadedLevels = new Queue<IPreviewBeatmapLevel>();

            foreach (var level in newCollection.ToList())
            {
                if (level is PreviewBeatmapLevelSO && await SongHelpers.HasDLCLevel(level.levelID))
                {
                    var result = await SongHelpers.GetLevelFromPreview(level);
                    if (result != null && !(result?.isError == true))
                    {
                        loadedLevels.Enqueue(result?.beatmapLevel);
                    }
                }
                else if (level is BeatmapLevelSO || level is CustomPreviewBeatmapLevel) loadedLevels.Enqueue(level);
            }

            foreach (var level in loadedLevels) Logger.Debug($"LOADED LEVEL: {level.songName}");

            levelsLoaded?.Invoke(loadedLevels);
        }

        public void OnApplicationQuit()
        {
        }

        public void OnLevelWasLoaded(int level)
        {

        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }
    }
}

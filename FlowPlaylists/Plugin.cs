﻿using FlowPlaylists.Misc;
using FlowPlaylists.UI;
using FlowPlaylists.UI.FlowCoordinators;
using IPA;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = FlowPlaylists.Misc.Logger;

namespace FlowPlaylists
{
    public class Plugin : IBeatSaberPlugin
    {
        public const string Name = "FlowPlaylists";
        public const string Version = "0.0.6";

        public static Plugin instance;
        public Queue<IBeatmapLevel> loadedLevels;
        public event Action<Queue<IBeatmapLevel>> levelsLoaded;

        //For the purpose of Replays
        public static Scene MenuScene;
        public static Scene GameScene;

        private FlowPlaylistsFlowCoordinator flowPlaylistsFlowCoordinator;

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
            if (nextScene.name == "GameCore") GameScene = nextScene;
            if (nextScene.name == "MenuViewControllers") MenuScene = nextScene;

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

            if (flowPlaylistsFlowCoordinator == null) flowPlaylistsFlowCoordinator = mainFlowCoordinator.gameObject.AddComponent<FlowPlaylistsFlowCoordinator>();

            var gameOption = CustomUI.GameplaySettings.GameplaySettingsUI.CreateToggleOption(CustomUI.GameplaySettings.GameplaySettingsPanels.ModifiersLeft, $"Enable {Name}", "When you press play, songs after the song you select will be stitched together into one", null, 0f);
            var menuButton = CustomUI.MenuButton.MenuButtonUI.AddButton("FlowPlaylists", "Open FlowPlaylists Menu", () => flowPlaylistsFlowCoordinator.PresentUI(mainFlowCoordinator));

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

        private void BSUtilsDisableOtherPlugins()
        {
            BS_Utils.Gameplay.Gamemode.NextLevelIsIsolated("FlowPlaylists");
            Logger.Debug("Disabled game-modifying plugins through bs_utils :)");
        }

        private async void didPressPlay(StandardLevelDetailViewController standardLevelDetailViewController)
        {
            if (IPA.Loader.PluginManager.AllPlugins.Any(x => x.Metadata.Name.ToLower() == "Beat Saber Utils".ToLower()))
            {
                BSUtilsDisableOtherPlugins();
            }
            else Logger.Debug("BSUtils not installed, not disabling other plugins");

            var currentView = Resources.FindObjectsOfTypeAll<LevelPackLevelsTableView>().First();
            var currentPack = currentView.GetField<IBeatmapLevelPack>("_pack");
            var currentCollection = currentPack.beatmapLevelCollection;

            var newCollection = currentCollection.beatmapLevels.SkipWhile(x => x.levelID != standardLevelDetailViewController.selectedDifficultyBeatmap.level.levelID);

            //If we're dealing with DLC, we have to load all the levels that the user has
            //now, because loading them mid-Update() would require Update to be async,
            //and *that* would cause multiple level loads to be started before the first level load finishes
            if (!(newCollection.First() is IBeatmapLevel))
            {
                loadedLevels = new Queue<IBeatmapLevel>();

                foreach (var level in newCollection.ToList())
                {
                    if (await SongHelpers.HasDLCLevel(level.levelID))
                    {
                        var result = await SongHelpers.GetDLCLevel(level);
                        if (result != null && !(result?.isError == true))
                        {
                            loadedLevels.Enqueue(result?.beatmapLevel);
                        }
                    }
                }
            }
            else loadedLevels = new Queue<IBeatmapLevel>(newCollection.Select(x => x as IBeatmapLevel));

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

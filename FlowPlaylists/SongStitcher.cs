#pragma warning disable 0649

using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace FlowPlaylists
{
    class SongStitcher : MonoBehaviour
    {
        public Queue<BeatmapLevelSO> Playlist { get; set; }

        private GameplayCoreSceneSetup gameplayCoreSceneSetup;
        private GameplayCoreSceneSetupData gameplayCoreSceneSetupData;
        private GamePauseManager gamePauseManager;
        private PauseMenuManager pauseMenuManager;
        private AudioTimeSyncController audioTimeSyncController;
        private BeatmapObjectSpawnController beatmapObjectSpawnController;
        private BeatmapObjectCallbackController beatmapObjectCallbackController;
        private BeatmapDataModel beatmapDataModel;

        public void Start()
        {
            gameplayCoreSceneSetup = Resources.FindObjectsOfTypeAll<GameplayCoreSceneSetup>().First();
            gameplayCoreSceneSetupData = gameplayCoreSceneSetup.GetProperty<GameplayCoreSceneSetupData>("sceneSetupData");
            gamePauseManager = Resources.FindObjectsOfTypeAll<GamePauseManager>().First();
            pauseMenuManager = gameplayCoreSceneSetup.GetField<PauseMenuManager>("_pauseMenuManager");
            audioTimeSyncController = gameplayCoreSceneSetup.GetField<AudioTimeSyncController>("_audioTimeSyncController");
            beatmapObjectSpawnController = gameplayCoreSceneSetup.GetField<BeatmapObjectSpawnController>("_beatmapObjectSpawnController");
            beatmapObjectCallbackController = gameplayCoreSceneSetup.GetField<BeatmapObjectCallbackController>("_beatmapObjectCallbackController");
            beatmapDataModel = gameplayCoreSceneSetup.GetField<BeatmapDataModel>("_beatmapDataModel");
        }

        public void Update()
        {
            if (gamePauseManager.pause) return; //Don't do anything if we're paused

            //if (audioTimeSyncController.songTime > 10f && Playlist.Count > 0)
            if (audioTimeSyncController.songTime >= audioTimeSyncController.songLength - 0.3f && Playlist.Count > 0)
            {
                ClearOldData();

                var gameplayModifiers = gameplayCoreSceneSetupData.gameplayModifiers;
                float songSpeedMul = gameplayModifiers.songSpeedMul;

                BeatmapLevelSO level = Playlist.Dequeue();
                
                //Since the first song in the playlist is the current song, we'll skip that for our first exchange
                if (level.levelID == gameplayCoreSceneSetupData.difficultyBeatmap.level.levelID) level = Playlist.Dequeue();

                Action<IBeatmapLevel> SongLoaded = (loadedLevel) =>
                {
                    IDifficultyBeatmap map = SongHelpers.GetClosestDifficultyPreferLower(level, BeatmapDifficulty.ExpertPlus);
                    BeatmapData beatmapData = BeatDataTransformHelper.CreateTransformedBeatmapData(map.beatmapData, gameplayModifiers, gameplayCoreSceneSetupData.practiceSettings, gameplayCoreSceneSetupData.playerSpecificSettings);
                    beatmapDataModel.beatmapData = beatmapData;

                    audioTimeSyncController.Init(map.level.beatmapLevelData.audioClip, 0f, map.level.songTimeOffset, songSpeedMul);
                    beatmapObjectSpawnController.Init(level.beatsPerMinute, beatmapData.beatmapLinesData.Length, gameplayModifiers.fastNotes ? 20f : map.difficulty.NoteJumpMovementSpeed(), map.noteJumpStartBeatOffset, gameplayModifiers.disappearingArrows, gameplayModifiers.ghostNotes);
                    pauseMenuManager.Init(map.level.songName, map.level.songSubName, map.difficulty.Name());
                    audioTimeSyncController.StartSong();
                };

                //Load audio if it's custom
                if (level is CustomLevel)
                {
                    SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)level, SongLoaded);
                }
                else
                {
                    SongLoaded(level);
                }
            }            
        }

        private void ClearOldData()
        {
            //Wipe score data


            //Wipe notes
            var noteAPool = beatmapObjectSpawnController.GetField<NoteController.Pool>("_noteAPool");
            var noteBPool = beatmapObjectSpawnController.GetField<NoteController.Pool>("_noteBPool");
            var bombNotePool = beatmapObjectSpawnController.GetField<NoteController.Pool>("_bombNotePool");
            var fullHeightObstaclePool = beatmapObjectSpawnController.GetField<ObstacleController.Pool>("_fullHeightObstaclePool");
            var topObstaclePool = beatmapObjectSpawnController.GetField<ObstacleController.Pool>("_topObstaclePool");

            noteAPool.activeItems.ToList().ForEach(x => beatmapObjectSpawnController.Despawn(x));
            noteBPool.activeItems.ToList().ForEach(x => beatmapObjectSpawnController.Despawn(x));
            bombNotePool.activeItems.ToList().ForEach(x => beatmapObjectSpawnController.Despawn(x));
            fullHeightObstaclePool.activeItems.ToList().ForEach(x => beatmapObjectSpawnController.Despawn(x));
            topObstaclePool.activeItems.ToList().ForEach(x => beatmapObjectSpawnController.Despawn(x));
        }

        public virtual void OnDestroy()
        {

        }
    }
}

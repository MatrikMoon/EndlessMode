#pragma warning disable 0649

using FlowPlaylists.Misc;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Logger = FlowPlaylists.Misc.Logger;

namespace FlowPlaylists
{
    class SongStitcher : MonoBehaviour
    {
        public static event Action<IDifficultyBeatmap, IDifficultyBeatmap> songSwitched;

        private Queue<IBeatmapLevel> playlist;

        //Stitch related instances
        [Inject]
        private GameplayCoreSceneSetup gameplayCoreSceneSetup;

        [Inject]
        private GamePauseManager gamePauseManager;

        [Inject]
        private PauseMenuManager pauseMenuManager;

        [Inject]
        private AudioTimeSyncController audioTimeSyncController;

        [Inject]
        private BeatmapObjectSpawnController beatmapObjectSpawnController;

        [Inject]
        private BeatmapDataModel beatmapDataModel;

        private GameplayCoreSceneSetupData gameplayCoreSceneSetupData;

        private NoteCutSoundEffectManager noteCutSoundEffectManager;

        private ButtonBinder buttonBinder;

        private bool _loadingSong = false;

        //Score related instances
        [Inject]
        private PrepareLevelCompletionResults prepareLevelCompletionResults;

        [Inject]
        private BeatmapObjectExecutionRatingsRecorder beatmapObjectExecutionRatingsRecorder;

        [Inject]
        private MultiplierValuesRecorder multiplierValuesRecorder;

        [Inject]
        private ScoreController scoreController;

        [Inject]
        private SaberActivityCounter saberActivityCounter;

        private StandardLevelDetailViewController levelDetailViewController;

        public void Start()
        {
            gameplayCoreSceneSetupData = gameplayCoreSceneSetup.GetProperty<GameplayCoreSceneSetupData>("sceneSetupData");
            noteCutSoundEffectManager = gameplayCoreSceneSetup.GetField<NoteCutSoundEffectManager>("_noteCutSoundEffectManager");
            levelDetailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();

            //Listen for restarts so that we can set up the playlist properly on restart
            var restartButton = pauseMenuManager.GetField<Button>("_restartButton");
            buttonBinder = new ButtonBinder();
            buttonBinder.AddBinding(restartButton, () => {
                playlist = new Queue<IBeatmapLevel>(playlist.Prepend(gameplayCoreSceneSetupData.difficultyBeatmap.level));
                Plugin.instance.loadedLevels = playlist;
            });
        }

        public void LevelsLoaded(Queue<IBeatmapLevel> levels)
        {
            Plugin.instance.levelsLoaded -= LevelsLoaded;
            playlist = new Queue<IBeatmapLevel>(levels);

            //Since the first song in the playlist is the current song, we'll skip that
            playlist.Dequeue();
        }

        public void Update()
        {
            if (gamePauseManager.pause) return; //Don't do anything if we're paused

            //if (audioTimeSyncController.songTime > 10f && playlist.Count > 0 && !_loadingSong)
            if (audioTimeSyncController.songTime >= audioTimeSyncController.songLength - 0.3f && playlist.Count > 0 && !_loadingSong)
            {
                Logger.Debug("Switching song...");

                //Submit score for the song which was just completed
                var results = prepareLevelCompletionResults.FillLevelCompletionResults(LevelCompletionResults.LevelEndStateType.Cleared);
                SubmitScore(results, gameplayCoreSceneSetupData.difficultyBeatmap);

                Logger.Debug($"Current song: {gameplayCoreSceneSetupData.difficultyBeatmap.level.songName}");

                //Clear out old data from objects that would have ideally been recreated
                ClearOldData();

                //Set up new song
                var gameplayModifiers = gameplayCoreSceneSetupData.gameplayModifiers;
                var playerSpecificSettings = gameplayCoreSceneSetupData.playerSpecificSettings;
                float songSpeedMul = gameplayModifiers.songSpeedMul;

                IPreviewBeatmapLevel level = playlist.Dequeue();

                Logger.Debug($"New song: {level.songName}");

                Action<IBeatmapLevel> SongLoaded = (loadedLevel) =>
                {
                    var oldMap = gameplayCoreSceneSetupData.difficultyBeatmap;

                    Logger.Debug($"Getting closest difficulty to {gameplayCoreSceneSetupData.difficultyBeatmap.difficulty} with characteristic {gameplayCoreSceneSetupData.difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic}...");
                    IDifficultyBeatmap map = SongHelpers.GetClosestDifficultyPreferLower(loadedLevel as IBeatmapLevel, gameplayCoreSceneSetupData.difficultyBeatmap.difficulty, gameplayCoreSceneSetupData.difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic);
                    //IDifficultyBeatmap map = SongHelpers.GetClosestDifficultyPreferLower(level as IBeatmapLevel, BeatmapDifficulty.ExpertPlus);

                    Logger.Debug($"Got: {map.difficulty} ({map.parentDifficultyBeatmapSet.beatmapCharacteristic})");

                    gameplayCoreSceneSetupData.SetField("_difficultyBeatmap", map);
                    BeatmapData beatmapData = BeatDataTransformHelper.CreateTransformedBeatmapData(map.beatmapData, gameplayModifiers, gameplayCoreSceneSetupData.practiceSettings, gameplayCoreSceneSetupData.playerSpecificSettings);
                    beatmapDataModel.beatmapData = beatmapData;

                    //If this is the last song, set up the viewcontrollers in a way that the proper data is displayed after the song
                    var currentPack = levelDetailViewController.GetField<IBeatmapLevelPack>("_pack");
                    var currentPlayer = levelDetailViewController.GetField<IPlayer>("_player");
                    var currentShowPlayerStats = levelDetailViewController.GetField<bool>("_showPlayerStats");
                    levelDetailViewController.SetData(currentPack, map.level, currentPlayer, currentShowPlayerStats);

                    audioTimeSyncController.Init(map.level.beatmapLevelData.audioClip, 0f, map.level.songTimeOffset, songSpeedMul);
                    beatmapObjectSpawnController.Init(loadedLevel.beatsPerMinute, beatmapData.beatmapLinesData.Length, gameplayModifiers.fastNotes ? 20f : map.difficulty.NoteJumpMovementSpeed(), map.noteJumpStartBeatOffset, gameplayModifiers.disappearingArrows, gameplayModifiers.ghostNotes);
                    pauseMenuManager.Init(map.level.songName, map.level.songSubName, map.difficulty.Name());

                    //Deal with characteristic issues
                    Saber.SaberType saberType;
                    if (gameplayCoreSceneSetup.UseOneSaberOnly(map.parentDifficultyBeatmapSet.beatmapCharacteristic, playerSpecificSettings, out saberType))
                    {
                        gameplayCoreSceneSetup.GetField<PlayerController>("_playerController").AllowOnlyOneSaber(saberType);
                    }
                    else
                    {
                        gameplayCoreSceneSetup
                            .GetField<PlayerController>("_playerController")
                            .GetField<SaberManager>("_saberManager")
                            .SetField("_allowOnlyOneSaber", false);
                    }

                    Logger.Debug("Starting new song...");
                    audioTimeSyncController.StartSong();
                    Logger.Debug("Song started!");

                    songSwitched?.Invoke(oldMap, map);

                    _loadingSong = false;
                };

                //Load audio if it's custom
                if (level is CustomLevel)
                {
                    Logger.Debug("Loading custom song data...");
                    _loadingSong = true;
                    SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)level, SongLoaded);
                }
                else
                {
                    Logger.Debug("Starting OST without songloader...");
                    SongLoaded(level as IBeatmapLevel);
                }
            }
            //else if (audioTimeSyncController.songTime > 10f && (playlist == null || playlist.Count <= 0) && !_loadingSong)
            else if (audioTimeSyncController.songTime >= audioTimeSyncController.songLength - 0.3f && (playlist == null || playlist.Count <= 0))
            {
                _loadingSong = true; //Only show the following log message once
                Logger.Debug($"Would have switched songs, but playlist was null ({playlist == null}) or empty ({playlist?.Count <= 0})");
            }
        }

        private static bool BSUtilsScoreDisabled()
        {
            return BS_Utils.Gameplay.ScoreSubmission.Disabled || BS_Utils.Gameplay.ScoreSubmission.ProlongedDisabled;
        }

        public static void SubmitScore(LevelCompletionResults results, IDifficultyBeatmap map)
        {
            
            //If bs_utils disables score submission, we do too
            if (IPA.Loader.PluginManager.AllPlugins.Any(x => x.Metadata.Name.ToLower() == "Beat Saber Utils".ToLower()))
            {
                Logger.Debug("Score submission disabled by bsutils");
                if (BSUtilsScoreDisabled()) return;
            }

            Logger.Debug($"Prepping to submit score for: {map.level.levelID} {results.unmodifiedScore} ({results.score})");

            var platformLeaderboardsModel = Resources.FindObjectsOfTypeAll<PlatformLeaderboardsModel>().First();
            var playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
            playerDataModel.currentLocalPlayer.playerAllOverallStatsData.soloFreePlayOverallStatsData.UpdateWithLevelCompletionResults(results);
            playerDataModel.Save();

            PlayerDataModelSO.LocalPlayer currentLocalPlayer = playerDataModel.currentLocalPlayer;
            GameplayModifiers gameplayModifiers = results.gameplayModifiers;
            bool cleared = results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared;
            string levelID = map.level.levelID;
            BeatmapDifficulty difficulty = map.difficulty;
            PlayerLevelStatsData playerLevelStatsData = currentLocalPlayer.GetPlayerLevelStatsData(levelID, difficulty, map.parentDifficultyBeatmapSet.beatmapCharacteristic);
            bool isHighScore = playerLevelStatsData.highScore < results.score;
            playerLevelStatsData.IncreaseNumberOfGameplays();
            if (cleared && isHighScore)
            {
                playerLevelStatsData.UpdateScoreData(results.score, results.maxCombo, results.fullCombo, results.rank);
                platformLeaderboardsModel.AddScore(map, results.unmodifiedScore, gameplayModifiers);
                Logger.Success($"Score uploaded successfully! {map.level.songName} {results.score} ({results.unmodifiedScore})");
            }
            else Logger.Debug("Player failed, or old score was greater than new score.");
        }

        private void ClearOldData()
        {
            //Wipe score data
            beatmapObjectExecutionRatingsRecorder.beatmapObjectExecutionRatings.Clear();
            beatmapObjectExecutionRatingsRecorder.GetField<HashSet<int>>("_hitObstacles").Clear();
            beatmapObjectExecutionRatingsRecorder.GetField<List<ObstacleController>>("_prevIntersectingObstacles").Clear();

            multiplierValuesRecorder.GetField<List<MultiplierValuesRecorder.MultiplierValue>>("_multiplierValues").Clear();
            
            scoreController.SetField("_baseScore", 0);
            scoreController.SetField("_prevFrameScore", 0);
            scoreController.SetField("_multiplier", 1);
            scoreController.SetField("_multiplierIncreaseProgress", 0);
            scoreController.SetField("_multiplierIncreaseMaxProgress", 2);
            scoreController.SetField("_combo", 0);
            scoreController.SetField("_maxCombo", 0);
            scoreController.SetField("_feverIsActive", false);
            scoreController.SetField("_feverStartTime", 0f);
            scoreController.SetField("_feverCombo", 0);
            scoreController.SetField("_playerHeadWasInObstacle", false);
            scoreController.SetField("_immediateMaxPossibleScore", 0);
            scoreController.SetField("_cutOrMissedNotes", 0);
            scoreController.GetField<List<AfterCutScoreBuffer>>("_afterCutScoreBuffers").Clear();

            saberActivityCounter.GetField<MovementHistoryRecorder>("_saberMovementHistoryRecorder").SetField("_accum", 0);
            saberActivityCounter.GetField<MovementHistoryRecorder>("_handMovementHistoryRecorder").SetField("_accum", 0);

            saberActivityCounter.saberMovementAveragingValueRecorder.GetField<Queue<AveragingValueRecorder.AverageValueData>>("_averageWindowValues").Clear();
            saberActivityCounter.saberMovementAveragingValueRecorder.GetField<Queue<float>>("_historyValues").Clear();
            saberActivityCounter.saberMovementAveragingValueRecorder.SetField("_time", 0);
            saberActivityCounter.saberMovementAveragingValueRecorder.SetField("_historyTime", 0);
            saberActivityCounter.saberMovementAveragingValueRecorder.SetField("_averageValue", 0);
            saberActivityCounter.saberMovementAveragingValueRecorder.SetField("_averageWindowValuesDuration", 0);
            saberActivityCounter.saberMovementAveragingValueRecorder.SetField("_lastValue", 0);

            saberActivityCounter.handMovementAveragingValueRecorder.GetField<Queue<AveragingValueRecorder.AverageValueData>>("_averageWindowValues").Clear();
            saberActivityCounter.handMovementAveragingValueRecorder.GetField<Queue<float>>("_historyValues").Clear();
            saberActivityCounter.handMovementAveragingValueRecorder.SetField("_time", 0);
            saberActivityCounter.handMovementAveragingValueRecorder.SetField("_historyTime", 0);
            saberActivityCounter.handMovementAveragingValueRecorder.SetField("_averageValue", 0);
            saberActivityCounter.handMovementAveragingValueRecorder.SetField("_averageWindowValuesDuration", 0);
            saberActivityCounter.handMovementAveragingValueRecorder.SetField("_lastValue", 0);

            saberActivityCounter.SetField("_leftSaberMovementDistance", 0);
            saberActivityCounter.SetField("_rightSaberMovementDistance", 0);
            saberActivityCounter.SetField("_leftHandMovementDistance", 0);
            saberActivityCounter.SetField("_rightHandMovementDistance", 0);

            noteCutSoundEffectManager.SetField("_prevNoteATime", -1f);
            noteCutSoundEffectManager.SetField("_prevNoteBTime", -1f);

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
            buttonBinder?.ClearBindings();
        }
    }
}

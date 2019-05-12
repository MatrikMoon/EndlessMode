using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomUI;
using CustomUI.BeatSaber;
using CustomUI.Settings;
using FlowPlaylists.Misc;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VRUI;
using Logger = FlowPlaylists.Misc.Logger;

namespace FlowPlaylists.UI.ViewControllers
{
    class CenterViewController : CustomViewController
    {
        private int hours = 0;
        private int tenMinutes = 0;
        private int minutes = 0;
        private BeatmapDifficulty preferredDifficulty;
        private bool useOnlyPreferredDifficulty;

        private TextMeshProUGUI timeText;
        private Image progressBar;
        private Image progressBarBackground;
        private Button generateButton;

        private IDifficultyBeatmap currentMap;

        private SoloFreePlayFlowCoordinator soloFreePlayFlowCoordinator;
        private StandardLevelDetailViewController levelDetailViewController;
        private AdditionalContentModelSO _additionalContentModel;
        private BeatmapLevelCollectionSO _primaryLevelCollection;
        private BeatmapLevelCollectionSO _secondaryLevelCollection;
        private BeatmapLevelCollectionSO _extrasLevelCollection;
        private BeatmapLevelPackCollectionSO beatmapLevelPackCollection;

        private static System.Random rand = new System.Random();

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            base.DidActivate(firstActivation, activationType);
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                if (soloFreePlayFlowCoordinator == null) soloFreePlayFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                if (levelDetailViewController == null) levelDetailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();
                if (_additionalContentModel == null) _additionalContentModel = Resources.FindObjectsOfTypeAll<AdditionalContentModelSO>().First();
                if (_primaryLevelCollection == null) _primaryLevelCollection = _additionalContentModel.alwaysOwnedPacks.First(x => x.packID == "OstVol1").beatmapLevelCollection as BeatmapLevelCollectionSO;
                if (_secondaryLevelCollection == null) _secondaryLevelCollection = _additionalContentModel.alwaysOwnedPacks.First(x => x.packID == "OstVol2").beatmapLevelCollection as BeatmapLevelCollectionSO;
                if (_extrasLevelCollection == null) _extrasLevelCollection = _additionalContentModel.alwaysOwnedPacks.First(x => x.packID == "Extras").beatmapLevelCollection as BeatmapLevelCollectionSO;
                if (beatmapLevelPackCollection == null) beatmapLevelPackCollection = soloFreePlayFlowCoordinator.GetField<BeatmapLevelPackCollectionSO>("_levelPackCollection");

                //Difficulty selection toggle
                AddMultiSelectOption("Preferred Difficulty", new Dictionary<float, string>
                {
                    { 0, "Easy" },
                    { 1, "Normal" },
                    { 2, "Hard" },
                    { 3, "Expert" },
                    { 4, "Expert+" }
                }, rectTransform, gameObject, (v) =>
                {
                    preferredDifficulty = (BeatmapDifficulty)(int)v;
                    Logger.Debug($"Preferred difficulty: {preferredDifficulty}");
                }, new Vector2(84, 9));

                AddMultiSelectOption("Play <i>only</i> Preferred Difficulty", new Dictionary<float, string>
                {
                    { 0, "No" },
                    { 1, "Yes" }
                }, rectTransform, gameObject, (v) =>
                {
                    useOnlyPreferredDifficulty = v == 1;
                    Logger.Debug($"Use only preferred difficulty: {useOnlyPreferredDifficulty}");
                }, new Vector2(84, -5));

                //Help text
                var helpText = BeatSaberUI.CreateText(rectTransform, "Welcome to FlowPlaylists!\nRemember: <color=\"green\">You can also enable FlowPlaylists as a Game Option on the left hand panel when you're in the song menu.</color>", new Vector2(0, 20f));
                helpText.enableWordWrapping = true;
                helpText.alignment = TextAlignmentOptions.Center;

                var timeLabelText = BeatSaberUI.CreateText(rectTransform, "Minimum time for generated playlist:", new Vector2(0, 5));
                timeLabelText.enableWordWrapping = true;
                timeLabelText.alignment = TextAlignmentOptions.Center;

                var displayPositionX = 8f;
                var displayPositionY = -12f;
                
                //Time selection text
                timeText = BeatSaberUI.CreateText(rectTransform, "0  :  0   0", new Vector2(displayPositionX - 8.4f, displayPositionY + 3));
                timeText.autoSizeTextContainer = true;
                timeText.fontSize = 12f;

                //Time selection buttons
                AddArrowButton(rectTransform, () => {
                    if (hours < 9) hours++;
                    UpdateTimeText();
                }, new Vector2(displayPositionX - 15, displayPositionY + 8));

                AddArrowButton(rectTransform, () => {
                    if (tenMinutes < 5) tenMinutes++;
                    UpdateTimeText();
                }, new Vector2(displayPositionX, displayPositionY + 8));

                AddArrowButton(rectTransform, () => {
                    if (minutes < 9) minutes++;
                    UpdateTimeText();
                }, new Vector2(displayPositionX + 10.7f, displayPositionY + 8));

                AddArrowButton(rectTransform, () => {
                    if (hours > 0) hours--;
                    UpdateTimeText();
                }, new Vector2(displayPositionX - 15, displayPositionY - 8), true);

                AddArrowButton(rectTransform, () => {
                    if (tenMinutes > 0) tenMinutes--;
                    UpdateTimeText();
                }, new Vector2(displayPositionX, displayPositionY - 8), true);

                AddArrowButton(rectTransform, () => {
                    if (minutes > 0) minutes--;
                    UpdateTimeText();
                }, new Vector2(displayPositionX + 10.7f, displayPositionY - 8), true);

                //Playlist generate button
                generateButton = BeatSaberUI.CreateUIButton(rectTransform, "QuitButton", new Vector2(0, -28), new Vector2(50, 10), () => {
                    generateButton.gameObject.SetActive(false);
                    progressBarBackground.gameObject.SetActive(true);
                    progressBar.gameObject.SetActive(true);

                    GeneratePlaylistWithMinTime(TimeTextToFloat(), useOnlyPreferredDifficulty ? preferredDifficulty : (BeatmapDifficulty?)null, (playlist) =>
                    {
                        var duration = 0f;
                        foreach (var song in playlist) {
                            duration += song.songDuration;
                            Logger.Debug($"PLAYLIST ITEM: {song.songName} ({song.songDuration})");
                        }
                        Logger.Debug($"TOTAL DURATION: {duration}");

                        generateButton.gameObject.SetActive(true);
                        progressBarBackground.gameObject.SetActive(false);
                        progressBar.gameObject.SetActive(false);

                        //Launch first level
                        Config.Enabled = true;
                        Plugin.instance.loadedLevels = new Queue<IBeatmapLevel>(playlist);
                        var firstMap = SongHelpers.GetClosestDifficultyPreferLower(Plugin.instance.loadedLevels.First(), preferredDifficulty);

                        SongStitcher.songSwitched -= SongSwitched;
                        SongStitcher.songSwitched += SongSwitched;

                        var playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
                        MenuTransitionsHelperSO menuTransitionHelper = Resources.FindObjectsOfTypeAll<MenuTransitionsHelperSO>().FirstOrDefault();
                        menuTransitionHelper.StartStandardLevel(firstMap, playerDataModel.currentLocalPlayer.gameplayModifiers, playerDataModel.currentLocalPlayer.playerSpecificSettings, null, false, null, SongFinished);
                    });
                }, "Generate random playlist");

                //Progress bar
                progressBarBackground = new GameObject().AddComponent<Image>();
                var progressBackgroundTransform = progressBarBackground.transform as RectTransform;
                progressBackgroundTransform.SetParent(rectTransform, false);
                progressBackgroundTransform.sizeDelta = new Vector2(100, 10);
                progressBackgroundTransform.anchoredPosition = new Vector2(0, -28);
                progressBarBackground.color = new Color(0f, 0f, 0f, 0.2f);
                progressBarBackground.material = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(x => x.name == "UINoGlow");
                progressBarBackground.gameObject.SetActive(false);

                progressBar = new GameObject().AddComponent<Image>();
                var progressBarTransform = progressBar.transform as RectTransform;
                progressBarTransform.SetParent(rectTransform, false);
                progressBarTransform.sizeDelta = new Vector2(100, 10);
                progressBarTransform.anchoredPosition = new Vector2(0, -28);
                progressBar.color = Color.white;
                progressBar.material = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(x => x.name == "UINoGlow");
                var whiteTexture = Texture2D.whiteTexture;
                var sprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, whiteTexture.width, whiteTexture.height), Vector2.one * 0.5f, 100f, 1u);
                progressBar.sprite = sprite;
                progressBar.type = Image.Type.Filled;
                progressBar.fillMethod = Image.FillMethod.Horizontal;
                progressBar.gameObject.SetActive(false);
            }
        }

        private void SongSwitched(IDifficultyBeatmap from, IDifficultyBeatmap to)
        {
            currentMap = to;
        }

        private void SongFinished(StandardLevelScenesTransitionSetupDataSO sceneTransitionData, LevelCompletionResults results)
        {
            if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.Restart) Config.LoadConfig(); //Reset Enabled status we changed above

            if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Restart)
            {
                var playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
                MenuTransitionsHelperSO menuTransitionHelper = Resources.FindObjectsOfTypeAll<MenuTransitionsHelperSO>().FirstOrDefault();
                menuTransitionHelper.StartStandardLevel(currentMap, playerDataModel.currentLocalPlayer.gameplayModifiers, playerDataModel.currentLocalPlayer.playerSpecificSettings, null, false, null, SongFinished);
            }
            else if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
            {
                SongStitcher.SubmitScore(results, currentMap);
            }
        }

    private void AddArrowButton(RectTransform parent, UnityAction pressed = null, Vector2? position = null, bool downArrow = false)
        {
            if (position == null) position = Vector2.zero;
            var originalUpArrow = Resources.FindObjectsOfTypeAll<Button>().Last(x => x.name == "PageUpButton");

            var button = BeatSaberUI.CreateUIButton(parent, "SettingsButton", (Vector2)position, new Vector2(12.5f, 7.75f), pressed);
            Destroy(button.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(x => x.name == "Text").gameObject);
            Destroy(button.GetComponentsInChildren<Image>().First(x => x.name == "Stroke"));
            var img = button.GetComponentsInChildren<Image>(true).FirstOrDefault(x => x.name == "Icon");
            img.sprite = originalUpArrow.GetComponentsInChildren<Image>().FirstOrDefault(x => x.name == "Arrow").sprite;
            if (!downArrow) img.rectTransform.Rotate(0, 0, 180);
        }

        private void AddMultiSelectOption(string optionName, Dictionary<float, string> options, Transform parent, GameObject destinationObject, Action<float> onSet = null, Vector2? position = null)
        {
            if (position == null) position = Vector2.zero;

            destinationObject.SetActive(false);

            FormattedFloatListSettingsController formattedFloatListSettingsController = Resources.FindObjectsOfTypeAll<FormattedFloatListSettingsController>().FirstOrDefault<FormattedFloatListSettingsController>();
            var multiSelectGameObject = Instantiate(formattedFloatListSettingsController.gameObject, parent);
            multiSelectGameObject.name = optionName;
            multiSelectGameObject.GetComponentInChildren<TMP_Text>().text = optionName;
            var oldListSettingsController = multiSelectGameObject.GetComponent<ListSettingsController>();
            var newListViewController = (ListViewController)ReflectionUtil.CopyComponent(oldListSettingsController, typeof(ListSettingsController), typeof(ListViewController), multiSelectGameObject);
            DestroyImmediate(oldListSettingsController);

            newListViewController.applyImmediately = true;
            newListViewController.values = options.Keys.ToList();
            newListViewController.SetValue = onSet;
            newListViewController.GetValue = () => options.Keys.ElementAt(0);
            newListViewController.GetTextForValue = (float v) =>
            {
                if (!options.ContainsKey(v))
                {
                    return "UNKNOWN";
                }
                if (options[v] == null)
                {
                    return v.ToString();
                }
                return options[v];
            };

            var valueTextTransform = newListViewController.gameObject.transform.Find("Value");
            var valueTextComponent = valueTextTransform.Find("ValueText").GetComponent<TMP_Text>();
            valueTextComponent.lineSpacing = -50f;
            valueTextComponent.alignment = TextAlignmentOptions.CenterGeoAligned;

            var nameTextTransform = newListViewController.gameObject.transform.Find("NameText");
            nameTextTransform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
            valueTextTransform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            valueTextTransform.Find("DecButton").transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            valueTextTransform.Find("IncButton").transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            valueTextTransform.localPosition -= new Vector3(71f, 5.3f);
            newListViewController.transform.localPosition = (Vector2)position;

            destinationObject.SetActive(true);
        }

        private void UpdateTimeText()
        {
            timeText.text = $"{hours}  :  {tenMinutes}   {minutes}";
        }

        private float TimeTextToFloat()
        {
            return (hours * 60 * 60) + (tenMinutes * 10 * 60) + (minutes * 60);
        }

        //TODO: Add support for DLCs
        private void GeneratePlaylistWithMinTime(float minTime, BeatmapDifficulty? difficulty = null, Action<List<IBeatmapLevel>> playlistLoaded = null)
        {
            var totalDuration = 0f;
            var pickFrom = new List<IPreviewBeatmapLevel>();

            foreach (var pack in beatmapLevelPackCollection.beatmapLevelPacks) pickFrom = pickFrom.Union(pack.beatmapLevelCollection.beatmapLevels).ToList();

            var ret = new List<IBeatmapLevel>();

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
                        Logger.Debug($"ADDED: {loadedLevel.songName} ({loadedLevel.songDuration})");
                        totalDuration += loadedLevel.songDuration;
                        ret.Add(loadedLevel);

                        progressBar.fillAmount = totalDuration / minTime;
                    }

                    if (totalDuration < minTime && pickFrom.Count > 0) addAnotherSong();
                    else playlistLoaded(ret);
                };

                if (!(currentLevel is IBeatmapLevel))
                {
                    if (await SongHelpers.HasDLCLevel(currentLevel.levelID))
                    {
                        Logger.Debug("Loading DLC level...");
                        var result = await SongHelpers.GetDLCLevel(currentLevel);
                        if (result != null && !(result?.isError == true))
                        {
                            SongLoaded(result?.beatmapLevel);
                        }
                    }
                    else
                    {
                        Logger.Debug($"Skipping unowned DLC ({currentLevel.songName})");
                        if (pickFrom.Count > 0) addAnotherSong();
                        else playlistLoaded(ret);
                    }
                }
                else if (currentLevel is CustomLevel)
                {
                    Logger.Debug("Loading custom song data...");
                    SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)currentLevel, SongLoaded);
                }
                else
                {
                    Logger.Debug("Reading OST data without songloader...");
                    SongLoaded(currentLevel as IBeatmapLevel);
                }
            };

            addAnotherSong();
        }
    }
}
using FlowPlaylists.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Logger = FlowPlaylists.Misc.Logger;

namespace FlowPlaylists.UI
{
    class CountdownPanel : MonoBehaviour
    {
        private Canvas mainCanvas;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI timeText;
        private GamePauseManager gamePauseManager;
        private float timeRemaining = 0f;
        private int songsRemaining = 0;

        private void SongsSwitched(IDifficultyBeatmap oldMap, IDifficultyBeatmap newMap) => songsRemaining--;

        private static CountdownPanel instance;

        public static CountdownPanel Create()
        {
            if (instance == null) instance = new GameObject("Countdown Panel").AddComponent<CountdownPanel>();
            return instance;
        }

        public void LevelsLoaded(Queue<IBeatmapLevel> levels)
        {
            Plugin.instance.levelsLoaded -= LevelsLoaded;

            songsRemaining = levels.Count;

            foreach (var level in levels)
            {
                //If there's a level with duration 0.001, we know it's not loaded yet, and we can't get an accurate
                //reading of the entire playlist time. We'll fall back to showing the number of songs remaining instead
                if (level.songDuration <= 0.001f)
                {
                    timeRemaining = 0f;
                    SongStitcher.songSwitched += SongsSwitched;
                    break;
                }
                timeRemaining += level.songDuration;
            }
        }

        public void Update()
        {
            if (gamePauseManager.pause) return; //Don't do anything if we're paused

            if (timeRemaining > 0f)
            {
                timeRemaining -= Time.deltaTime;
                timeText.text = $"{TimeSpan.FromSeconds(timeRemaining).ToString(@"hh\:mm\:ss")}";
            }
            else
            {
                titleText.text = "Songs remaining:";
                timeText.text = $"{songsRemaining}";
            }
        }

        private void Awake()
        {
            Config.LoadConfig();

            gamePauseManager = Resources.FindObjectsOfTypeAll<GamePauseManager>().First();

            gameObject.transform.position = Config.Position;
            gameObject.transform.eulerAngles = Config.Rotation;
            gameObject.transform.localScale = Config.Scale;

            mainCanvas = gameObject.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.WorldSpace;
            var canvasTransform = mainCanvas.transform as RectTransform;
            canvasTransform.sizeDelta = Config.Size;

            var fontAsset = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().First((TMP_FontAsset x) => x.name == "Teko-Medium SDF No Glow");

            var titleGameObject = new GameObject("Countdown Title");
            titleGameObject.SetActive(false);
            titleText = titleGameObject.AddComponent<TextMeshProUGUI>();
            var textTransform = titleText.transform as RectTransform;
            titleText.font = fontAsset;
            textTransform.SetParent(mainCanvas.transform, false);
            titleText.text = "Play time remaining:";
            titleText.enableWordWrapping = true;
            titleText.autoSizeTextContainer = true;
            titleText.alignment = TextAlignmentOptions.Center;
            textTransform.anchorMin = new Vector2(0.5f, 0.7f);
            textTransform.anchorMax = new Vector2(0.5f, 0.7f);
            titleGameObject.SetActive(true);

            var timeGameObject = new GameObject("Countdown Text");
            timeGameObject.SetActive(false);
            timeText = timeGameObject.AddComponent<TextMeshProUGUI>();
            textTransform = timeText.transform as RectTransform;
            timeText.font = fontAsset;
            textTransform.SetParent(mainCanvas.transform, false);
            timeText.text = $"{TimeSpan.FromSeconds(timeRemaining).ToString(@"hh\:mm\:ss")}";
            timeText.enableWordWrapping = true;
            timeText.autoSizeTextContainer = true;
            timeText.alignment = TextAlignmentOptions.Center;
            timeGameObject.SetActive(true);
        }

        public virtual void OnDestroy()
        {
            SongStitcher.songSwitched -= SongsSwitched;
        }
    }
}

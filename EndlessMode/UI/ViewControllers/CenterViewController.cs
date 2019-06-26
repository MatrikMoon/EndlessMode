using CustomUI.BeatSaber;
using CustomUI.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Logger = EndlessMode.Misc.Logger;

namespace EndlessMode.UI.ViewControllers
{
    class CenterViewController : CustomViewController
    {
        public event Action GenerateButtonPressed;

        public BeatmapDifficulty PreferredDifficulty { get; private set; }
        public bool UseOnlyPreferredDifficulty { get; private set; }

        private int hours = 0;
        private int tenMinutes = 0;
        private int minutes = 0;

        private TextMeshProUGUI timeText;
        private Image progressBar;
        private Image progressBarBackground;
        private Button generateButton;

        public enum UIType
        {
            GenerationButton,
            ProgressBar
        }

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            base.DidActivate(firstActivation, activationType);
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
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
                    PreferredDifficulty = (BeatmapDifficulty)(int)v;
                    Logger.Debug($"Preferred difficulty: {PreferredDifficulty}");
                }, new Vector2(20, -4));

                AddMultiSelectOption("Play <i>only</i> Preferred Difficulty", new Dictionary<float, string>
                {
                    { 0, "No" },
                    { 1, "Yes" }
                }, rectTransform, gameObject, (v) =>
                {
                    UseOnlyPreferredDifficulty = v == 1;
                    Logger.Debug($"Use only preferred difficulty: {UseOnlyPreferredDifficulty}");
                }, new Vector2(20, -18));

                //Help text
                var helpText = BeatSaberUI.CreateText(rectTransform, $"Welcome to {Plugin.Name}!\nRemember: <color=\"green\">You can also enable {Plugin.Name} as a Game Option on the left hand panel when you're in the song menu.</color>", new Vector2(0, 20f));
                helpText.enableWordWrapping = true;
                helpText.alignment = TextAlignmentOptions.Center;

                var disclaimer = BeatSaberUI.CreateText(rectTransform, $"<color=\"red\">Play responsibly! Remember to stay hydrated and always pause to take a break if you feel tired.</color>", new Vector2(-46f, -8f));
                disclaimer.rectTransform.sizeDelta -= new Vector2(20f, 0);
                disclaimer.enableWordWrapping = true;
                disclaimer.alignment = TextAlignmentOptions.Center;

                var timeLabelText = BeatSaberUI.CreateText(rectTransform, "Minimum time for generated playlist:", new Vector2(0, 5));
                timeLabelText.enableWordWrapping = true;
                timeLabelText.alignment = TextAlignmentOptions.Center;

                //Display position presets
                var displayPositionX = 0f;
                var displayPositionY = -13f;
                
                //Time selection text
                timeText = BeatSaberUI.CreateText(rectTransform, "0  :  0   0", new Vector2(displayPositionX - 2f, displayPositionY + 3));
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
                generateButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton", new Vector2(0, -28), new Vector2(50, 10), () => GenerateButtonPressed?.Invoke(), "Generate random playlist");

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

        public void SetUIType(UIType type)
        {
            if (type == UIType.GenerationButton)
            {
                generateButton.gameObject.SetActive(true);
                progressBarBackground.gameObject.SetActive(false);
                progressBar.gameObject.SetActive(false);
            }
            else
            {
                generateButton.gameObject.SetActive(false);
                progressBarBackground.gameObject.SetActive(true);
                progressBar.gameObject.SetActive(true);
            }
        }

        public void SetProgress(float progress)
        {
            progressBar.fillAmount = progress;
        }

        private void AddArrowButton(RectTransform parent, UnityAction pressed = null, Vector2? position = null, bool downArrow = false)
        {
            if (position == null) position = Vector2.zero;
            var originalUpArrow = Resources.FindObjectsOfTypeAll<Button>().Last(x => x.name == "PageUpButton");

            var button = BeatSaberUI.CreateUIButton(parent, "BeatmapEditorButton", (Vector2)position, new Vector2(12.5f, 7.75f), pressed);

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

            Polyglot.LocalizedTextMeshProUGUI localizer = newListViewController.GetComponentInChildren<Polyglot.LocalizedTextMeshProUGUI>();
            if (localizer != null) Destroy(localizer);

            var valueTextTransform = newListViewController.gameObject.transform.Find("Value");
            var valueTextComponent = valueTextTransform.Find("ValueText").GetComponent<TMP_Text>();
            valueTextComponent.lineSpacing = -50f;
            valueTextComponent.alignment = TextAlignmentOptions.CenterGeoAligned;

            var nameTextTransform = newListViewController.gameObject.transform.Find("NameText");
            nameTextTransform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
            nameTextTransform.localPosition += new Vector3(57f, 5f);
            (nameTextTransform as RectTransform).sizeDelta = new Vector2(47f, 8f); 
            valueTextTransform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            valueTextTransform.Find("DecButton").transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            valueTextTransform.Find("IncButton").transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            newListViewController.transform.localPosition = (Vector2)position;
            newListViewController.Init();

            destinationObject.SetActive(true);
        }

        private void UpdateTimeText()
        {
            timeText.text = $"{hours}  :  {tenMinutes}   {minutes}";
        }

        public float GetTimeValue()
        {
            return (hours * 60 * 60) + (tenMinutes * 10 * 60) + (minutes * 60);
        }
    }
}
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingsView : UIView
{
    [SerializeField] TMP_Text titleText;
    [SerializeField] Button closeButton;
    [SerializeField] Button restoreButton;
    [SerializeField] AnimatedPanel topPanel;
    [SerializeField] AnimatedPanel tabPanel;
    [SerializeField] DetailTabItem[] tabs;
    [SerializeField] GameObject graphicsPage;
    [SerializeField] GameObject audioPage;
    [SerializeField] Slider masterSlider;
    [SerializeField] TMP_Text masterValueText;
    [SerializeField] Slider uiSlider;
    [SerializeField] TMP_Text uiValueText;
    [SerializeField] TMP_Dropdown muteDropdown;
    [SerializeField] TMP_Dropdown windowModeDropdown;
    [SerializeField] TMP_Dropdown resolutionDropdown;
    [SerializeField] AudioClip volumeAdjustClip;
    [SerializeField] AudioClip confirmClip;
    [SerializeField] UIAnimator viewAnimator;
    [SerializeField] GameObject inputBlocker;
    [SerializeField] AnimatedPanelGroup contentAnimation;
    [SerializeField] AnimatedPanel[] graphicsItems;
    [SerializeField] AnimatedPanel[] audioItems;
    [SerializeField] CanvasGroup pageGroup;

    readonly CompositeDisposable bindings = new();
    SettingsViewModel viewModel;
    bool tabSelectionInitialized;
    bool closing;
    float lastVolumeFeedbackAt = float.NegativeInfinity;

    void OnEnable()
    {
        // Reopening reads device preferences without replaying value-change feedback.
        viewModel = new SettingsViewModel();
        tabSelectionInitialized = false;
        closing = false;
        pageGroup.interactable = true;
        inputBlocker.SetActive(false);
        viewAnimator.OnAnimationEnded.AddListener(FinishTransition);
        topPanel.Show(true).Forget();
        tabPanel.Show(true).Forget();
        tabs[0].Bind(0, "图像", () => viewModel.SelectedPage.Value = SettingsViewModel.Page.Graphics);
        tabs[1].Bind(1, "声音", () => viewModel.SelectedPage.Value = SettingsViewModel.Page.Audio);
        closeButton.onClick.AddListener(OnCancel);
        restoreButton.onClick.AddListener(RestoreDefaults);

        windowModeDropdown.ClearOptions();
        windowModeDropdown.AddOptions(new List<string>(SettingsViewModel.WindowModeChoices));
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(new List<string>(viewModel.ResolutionChoices));
        muteDropdown.ClearOptions();
        muteDropdown.AddOptions(new List<string> { "关闭", "开启" });

        viewModel.MasterVolume.Subscribe(value =>
        {
            masterSlider.SetValueWithoutNotify(value);
            masterValueText.text = value.ToString();
        }).AddTo(bindings);
        viewModel.UIVolume.Subscribe(value =>
        {
            uiSlider.SetValueWithoutNotify(value);
            uiValueText.text = value.ToString();
        }).AddTo(bindings);
        viewModel.Muted.Subscribe(value => muteDropdown.SetValueWithoutNotify(value ? 1 : 0)).AddTo(bindings);
        viewModel.WindowMode.Subscribe(windowModeDropdown.SetValueWithoutNotify).AddTo(bindings);
        viewModel.Resolution.Subscribe(resolutionDropdown.SetValueWithoutNotify).AddTo(bindings);
        viewModel.SelectedPage.Subscribe(ShowPage).AddTo(bindings);

        masterSlider.onValueChanged.AddListener(ChangeMasterVolume);
        uiSlider.onValueChanged.AddListener(ChangeUIVolume);
        muteDropdown.onValueChanged.AddListener(ChangeMute);
        windowModeDropdown.onValueChanged.AddListener(ChangeWindowMode);
        resolutionDropdown.onValueChanged.AddListener(ChangeResolution);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        pageGroup.interactable = false;
        inputBlocker.SetActive(true);
    }

    void FinishTransition()
    {
        if (!closing)
        {
            pageGroup.interactable = true;
            inputBlocker.SetActive(false);
        }
    }

    void ShowPage(SettingsViewModel.Page page)
    {
        bool audio = page == SettingsViewModel.Page.Audio;
        // Cancel delayed rows before hiding their page, so a previous selection cannot reappear.
        contentAnimation.HideAllImmediate();
        graphicsPage.SetActive(!audio);
        audioPage.SetActive(audio);
        titleText.text = audio ? "设置 / 声音" : "设置 / 图像";
        // Initial selection is steady; user changes play the particle entrance once.
        bool instant = !tabSelectionInitialized;
        tabs[0].SetSelected(!audio, instant);
        tabs[1].SetSelected(audio, instant);
        contentAnimation.Show(audio ? audioItems : graphicsItems).Forget();
        if (!instant)
        {
            AudioManager.Instance.PlayUI(UISound.ButtonClick);
        }
        tabSelectionInitialized = true;
    }

    void ChangeMasterVolume(float value)
    {
        if (viewModel.MasterVolume.Value == Mathf.RoundToInt(value))
            return;
        viewModel.SetMasterVolume(value);
        PlayVolumeFeedback();
    }

    void ChangeUIVolume(float value)
    {
        if (viewModel.UIVolume.Value == Mathf.RoundToInt(value))
            return;
        viewModel.SetUIVolume(value);
        PlayVolumeFeedback();
    }

    void PlayVolumeFeedback()
    {
        // Dragging can cross several integer steps in one frame; never stack adjustment sounds.
        if (Time.unscaledTime - lastVolumeFeedbackAt < 0.075f)
            return;
        lastVolumeFeedbackAt = Time.unscaledTime;
        AudioManager.Instance.PlayUI(volumeAdjustClip, 0.8f);
    }

    void ChangeMute(int value)
    {
        viewModel.SetMuted(value != 0);
        AudioManager.Instance.PlayUI(UISound.ButtonClick);
    }

    void ChangeWindowMode(int value)
    {
        viewModel.SetWindowMode(value);
        AudioManager.Instance.PlayUI(UISound.ButtonClick);
    }

    void ChangeResolution(int value)
    {
        viewModel.SetResolution(value);
        AudioManager.Instance.PlayUI(UISound.ButtonClick);
    }

    void RestoreDefaults()
    {
        viewModel.RestoreCurrentPage();
        AudioManager.Instance.PlayUI(confirmClip);
    }

    public override void OnCancel()
    {
        if (closing || inputBlocker.activeSelf)
            return;

        if (muteDropdown.IsExpanded || windowModeDropdown.IsExpanded || resolutionDropdown.IsExpanded)
        {
            muteDropdown.Hide();
            windowModeDropdown.Hide();
            resolutionDropdown.Hide();
            return;
        }

        closing = true;
        pageGroup.interactable = false;
        inputBlocker.SetActive(true);
        if (Handle != null)
            base.OnCancel();
        else
        {
            AudioManager.Instance.PlayUI(UISound.PanelClose);
            viewAnimator.PlayGroup("Close", () => gameObject.SetActive(false));
        }
    }

    void OnDisable()
    {
        viewAnimator.OnAnimationEnded.RemoveListener(FinishTransition);
        viewAnimator.Kill();
        contentAnimation.HideAllImmediate();
        pageGroup.interactable = true;
        inputBlocker.SetActive(false);
        closeButton.onClick.RemoveListener(OnCancel);
        restoreButton.onClick.RemoveListener(RestoreDefaults);
        masterSlider.onValueChanged.RemoveListener(ChangeMasterVolume);
        uiSlider.onValueChanged.RemoveListener(ChangeUIVolume);
        muteDropdown.onValueChanged.RemoveListener(ChangeMute);
        windowModeDropdown.onValueChanged.RemoveListener(ChangeWindowMode);
        resolutionDropdown.onValueChanged.RemoveListener(ChangeResolution);
        // Reused tabs retain pointer state while inactive; reset it before reopening.
        foreach (var tab in tabs)
        {
            tab.GetComponent<Button>().onClick.RemoveAllListeners();
            tab.OnPointerExit(null);
            tab.SetSelected(false, true);
        }
        bindings.Clear();
        viewModel.Dispose();
        viewModel = null;
    }

    void OnDestroy()
    {
        bindings.Dispose();
    }
}

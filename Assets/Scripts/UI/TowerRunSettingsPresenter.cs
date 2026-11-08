using System;
using UnityEngine;

public sealed class TowerRunSettingsPresenter : IDisposable
{
    private const string VolumeKey = "tower-run.audio.master-volume.v1";
    private const string MutedKey = "tower-run.audio.muted.v1";
    private readonly TowerRunUiViews view;
    private readonly TowerRunSaveSlotsPresenter saves;
    private AudioClip previewClip;
    public TowerRunSettingsPresenter(TowerRunUiViews view, TowerRunSaveSlotsPresenter saves) {this.view=view;this.saves=saves;}
    public void ShowAudioPage()
    { view.audioPage.SetActive(true);view.savePage.SetActive(false);saves.Reset();view.StyleSettingsTabs(true); }
    public void ShowSavePage() { StopPreviewSound();saves.ShowPage(); }
    public void Close() { StopPreviewSound();saves.Reset(); }
    public void Dispose() {StopPreviewSound();if(previewClip!=null)UnityEngine.Object.Destroy(previewClip);}
    public void LoadAudioPreferences()
    {
        ITowerRunPreferenceStore preferences = TowerRunRuntimeEnvironment.Preferences;
        float volume = Mathf.Clamp01(preferences.GetFloat(VolumeKey, 1f));
        bool muted = preferences.GetInt(MutedKey, 0) != 0;
        view.volumeSlider.SetValueWithoutNotify(volume);
        view.muteToggle.SetIsOnWithoutNotify(muted);
        ApplyAudio(volume, muted, persist: false);
    }

    public void HandleVolumeChanged(float value)
    {
        ApplyAudio(value, view.muteToggle.isOn, persist: true);
    }

    public void HandleMuteChanged(bool muted)
    {
        ApplyAudio(view.volumeSlider.value, muted, persist: true);
    }

    private static void ApplyAudio(float volume, bool muted, bool persist)
    {
        volume = Mathf.Clamp01(volume);
        AudioListener.volume = muted ? 0f : volume;

        if (!persist)
            return;

        ITowerRunPreferenceStore preferences = TowerRunRuntimeEnvironment.Preferences;
        preferences.SetFloat(VolumeKey, volume);
        preferences.SetInt(MutedKey, muted ? 1 : 0);
        preferences.Save();
    }

    public void PlayPreviewSound()
    {
        if (previewClip == null)
        {
            const int sampleRate = 44100;
            const float duration = 0.18f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float envelope = 1f - i / (float)sampleCount;
                samples[i] = Mathf.Sin(2f * Mathf.PI * 660f * i / sampleRate) * envelope * 0.25f;
            }

            previewClip = AudioClip.Create("TowerRunSettingsPreview", sampleCount, 1, sampleRate, false);
            previewClip.SetData(samples, 0);
        }

        view.previewSource.Stop();
        view.previewSource.clip = previewClip;
        view.previewSource.Play();
    }

    public void StopPreviewSound()
    {
        if (view.previewSource == null)
            return;

        view.previewSource.Stop();
        view.previewSource.clip = null;
    }
}

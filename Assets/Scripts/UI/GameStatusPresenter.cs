using TMPro;
using UnityEngine;

/// <summary>Event-driven wave, equipment and interaction UI. Only the timed announcement expires in Update.</summary>
public sealed class GameStatusPresenter : MonoBehaviour
{
    [SerializeField]
    private WaveManager waves;
    [SerializeField]
    private PlayerEquipment equipment;
    [SerializeField]
    private PlayerInteractor interaction;
    [SerializeField]
    private TMP_Text waveText, remainingText, medkitText, grenadeText, promptText, announcementText;
    private float hideAt;
    private string previousAnnouncement;
    private void Awake()
    {
        if (waves != null && equipment != null && interaction != null && waveText != null && remainingText != null && medkitText != null && grenadeText != null && promptText != null && announcementText != null)
            return;
        Debug.LogError("GameStatusPresenter requires its wave, equipment, interaction and text references.", this);
        enabled = false;
    }

    private void OnEnable()
    {
        waves.Changed += RefreshWave;
        equipment.Changed += RefreshEquipment;
        interaction.PromptChanged += RefreshPrompt;
        Refresh();
    }

    private void Start() => Refresh();
    private void OnDisable()
    {
        if (waves != null)
            waves.Changed -= RefreshWave;
        if (equipment != null)
            equipment.Changed -= RefreshEquipment;
        if (interaction != null)
            interaction.PromptChanged -= RefreshPrompt;
    }

    private void Refresh()
    {
        RefreshWave();
        RefreshEquipment(equipment.Medkits, equipment.Grenades);
        RefreshPrompt(interaction.Prompt);
    }

    private void RefreshWave()
    {
        waveText.text = waves.CurrentWave == 0 ? "--" : waves.CurrentWave.ToString();
        remainingText.text = waves.Remaining.ToString();
        if (previousAnnouncement == waves.Announcement)
            return;
        previousAnnouncement = waves.Announcement;
        announcementText.text = waves.Announcement;
        hideAt = Time.time + 4;
    }

    private void RefreshEquipment(int medkits, int grenades)
    {
        medkitText.text = "MED " + medkits;
        grenadeText.text = "FRAG " + grenades;
    }

    private void RefreshPrompt(string value) => promptText.text = value;
    private void Update()
    {
        if (hideAt > 0 && Time.time >= hideAt)
        {
            announcementText.text = "";
            hideAt = 0;
        }
    }
}

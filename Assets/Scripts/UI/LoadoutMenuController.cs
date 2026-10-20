using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public sealed class LoadoutMenuController : MonoBehaviour
{
    [SerializeField]
    private LoadoutCatalog catalog;
    [SerializeField]
    private bool isLoadout;
    [SerializeField]
    private UnityEngine.UI.Button startButton;
    [SerializeField]
    private UnityEngine.UI.Button backButton;
    [SerializeField]
    private UnityEngine.UI.Button quitButton;
    [SerializeField]
    private UnityEngine.UI.Button[] primaryButtons = System.Array.Empty<UnityEngine.UI.Button>();
    [SerializeField]
    private UnityEngine.UI.Button[] equipmentButtons = System.Array.Empty<UnityEngine.UI.Button>();
    [SerializeField]
    private TMP_Text selectionText;
    private LoadoutSession session;
    private readonly List<KeyValuePair<UnityEngine.UI.Button, UnityAction>> listeners = new List<KeyValuePair<UnityEngine.UI.Button, UnityAction>>();
    private void OnEnable()
    {
        if (catalog == null || startButton == null)
        {
            Debug.LogError("Loadout menu requires a catalog and Start button.", this);
            enabled = false;
            return;
        }

        Time.timeScale = 1;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        session = LoadoutSession.Ensure(catalog);
        Bind(startButton, StartGame);
        if (backButton != null)
            Bind(backButton, Back);
        if (quitButton != null)
            Bind(quitButton, Application.Quit);
        for (int i = 0; i < primaryButtons.Length; i++)
        {
            int selected = i;
            Bind(primaryButtons[i], () =>
            {
                session.Select(selected, session.Equipment);
                Refresh();
            });
        }

        for (int i = 0; i < equipmentButtons.Length; i++)
        {
            int selected = i;
            Bind(equipmentButtons[i], () =>
            {
                session.Select(session.Primary, selected);
                Refresh();
            });
        }

        Refresh();
    }

    private void Bind(UnityEngine.UI.Button button, UnityAction action)
    {
        button.onClick.AddListener(action);
        listeners.Add(new KeyValuePair<UnityEngine.UI.Button, UnityAction>(button, action));
    }

    private void OnDisable()
    {
        foreach (var entry in listeners)
            if (entry.Key != null)
                entry.Key.onClick.RemoveListener(entry.Value);
        listeners.Clear();
    }

    private void Refresh()
    {
        startButton.interactable = !isLoadout || session.IsValid;
        if (!isLoadout)
            return;
        selectionText.text = session.IsValid ? catalog.primaries[session.Primary].WeaponName.ToUpperInvariant() + " + PISTOL\n" + catalog.equipment[session.Equipment].name : "SELECT A VALID LOADOUT";
        for (int i = 0; i < primaryButtons.Length; i++)
            primaryButtons[i].interactable = i != session.Primary;
        for (int i = 0; i < equipmentButtons.Length; i++)
            equipmentButtons[i].interactable = i != session.Equipment;
    }

    public void StartGame()
    {
        if (!isLoadout)
            SceneManager.LoadScene("Loadout");
        else if (session.IsValid)
            SceneManager.LoadScene("ZombieMap");
    }

    public void Back() => SceneManager.LoadScene("MainMenu");
}

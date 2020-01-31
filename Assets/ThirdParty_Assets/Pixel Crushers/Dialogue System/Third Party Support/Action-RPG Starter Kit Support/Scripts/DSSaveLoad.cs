using UnityEngine;
using System.Collections;
using System.Reflection;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.UnityGUI;

namespace PixelCrushers.DialogueSystem.ARPGSupport
{

    /// <summary>
    /// This runs on top of ARPG's SaveLoad. It replaces SaveLoad's menu with its
    /// own menu, which allows it to also save and load Dialogue System data.
    /// It supports multiple saved game slots, and you can override the GetSlotSummary(),
    /// SaveSlot(), and LoadSlot() methods to save somewhere other than the default,
    /// which is PlayerPrefs.
    /// </summary>
    [AddComponentMenu("Pixel Crushers/Dialogue System/Third Party/Action-RPG Starter Kit/DS SaveLoad (Player prefab)")]
    public class DSSaveLoad : MonoBehaviour
    {

        // Public properties:
        public string menuTitle = "Menu";
        public string saveMenuTitle = "Select Save Slot";
        public string loadMenuTitle = "Load From Slot";
        public ScaledRect scaledRect = ScaledRect.FromOrigin(ScaledRectAlignment.MiddleCenter,
                                                             ScaledValue.FromPixelValue(300),
                                                             ScaledValue.FromPixelValue(370));
        public GUISkin guiSkin;
        public KeyCode menuKey = KeyCode.Escape;
        public string savedGameKey = "SavedGame";
        public int numSavedGameSlots = 5;
        public string titleLevelName = "Title";
        public QuestLogWindow questLogWindow;

        // Private variables:
        private bool isMenuOpen = false;
        private Rect windowRect = new Rect(0, 0, 500, 500);
        private Rect slotWindowRect = new Rect(0, 0, 500, 500);
        private Rect confirmRect = new Rect(0, 0, 300, 200);
        private GameObject player = null;
        private SaveLoadC saveLoad = null;

        private enum WindowMode { Menu, Save, Load, ConfirmQuitToTitle, ConfirmExitProgram }
        private WindowMode windowMode = WindowMode.Menu;

        private const float ButtonHeight = 48;

        // On Start, let SaveLoad initialize itself, then disable SaveLoad.
        public void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player");
            saveLoad = GetComponent<SaveLoadC>();
            StartCoroutine(DisableSaveLoad());
            SetupQuestLogWindow();
        }

        private IEnumerator DisableSaveLoad()
        {
            yield return null;
            if (saveLoad != null) saveLoad.enabled = false;
        }

        // On Update, allow the player to open/close the menu.
        public void Update()
        {
            if (Input.GetKeyDown(menuKey) && !DialogueManager.IsConversationActive && !IsQuestLogOpen())
            {
                SetMenuStatus(!isMenuOpen);
            }
        }

        void OnGUI()
        {
            //--- Debug: GUILayout.Label("lockCursor=" + Screen.lockCursor + ", showCursor=" + Screen.showCursor + ", isMenuOpen=" + isMenuOpen + ", timeScale=" + Time.timeScale);
            if (isMenuOpen)
            {
                if (guiSkin != null) GUI.skin = guiSkin;
                switch (windowMode)
                {
                    case WindowMode.Menu:
                        windowRect = GUI.Window(0, windowRect, MainWindowFunction, menuTitle);
                        break;
                    case WindowMode.Save:
                        slotWindowRect = GUI.Window(0, slotWindowRect, SaveWindowFunction, saveMenuTitle);
                        break;
                    case WindowMode.Load:
                        slotWindowRect = GUI.Window(0, slotWindowRect, LoadWindowFunction, loadMenuTitle);
                        break;
                    case WindowMode.ConfirmQuitToTitle:
                        confirmRect = GUI.Window(0, confirmRect, ConfirmWindowFunction, "Quit To Title");
                        break;
                    case WindowMode.ConfirmExitProgram:
                        confirmRect = GUI.Window(0, confirmRect, ConfirmWindowFunction, "Exit Program");
                        break;
                }
            }
        }

        private void MainWindowFunction(int windowID)
        {
            int y = 60;
            if (questLogWindow != null)
            {
                if (GUI.Button(new Rect(10, y, windowRect.width - 20, ButtonHeight), "Quest Log"))
                {
                    SetMenuStatus(false);
                    OpenQuestLog();
                }
                y += 50;
            }
            if (GUI.Button(new Rect(10, y, windowRect.width - 20, ButtonHeight), "Save Game"))
            {
                windowMode = WindowMode.Save;
            }
            y += 50;
            if (GUI.Button(new Rect(10, y, windowRect.width - 20, ButtonHeight), "Load Game"))
            {
                windowMode = WindowMode.Load;
            }
            y += 50;
            if (GUI.Button(new Rect(10, y, windowRect.width - 20, ButtonHeight), "Quit To Title"))
            {
                windowMode = WindowMode.ConfirmQuitToTitle;
                confirmRect = new Rect((Screen.width - confirmRect.width) / 2, (Screen.height - confirmRect.height) / 2, confirmRect.width, confirmRect.height);
            }
            y += 50;
            if (GUI.Button(new Rect(10, y, windowRect.width - 20, ButtonHeight), "Exit Program"))
            {
                windowMode = WindowMode.ConfirmExitProgram;
                confirmRect = new Rect((Screen.width - confirmRect.width) / 2, (Screen.height - confirmRect.height) / 2, confirmRect.width, confirmRect.height);
            }
            y += 50;
            if (GUI.Button(new Rect(10, y, windowRect.width - 20, ButtonHeight), "Close Menu"))
            {
                SetMenuStatus(false);
            }
        }

        private void SaveWindowFunction(int windowID)
        {
            int y = 60;
            for (int i = 0; i < numSavedGameSlots; i++)
            {
                var buttonRect = new Rect(10, y, slotWindowRect.width - 20, ButtonHeight);
                var summary = GetSlotSummary(i);
                if (string.IsNullOrEmpty(summary)) summary = "(empty)";
                if (GUI.Button(buttonRect, "Slot " + i + ": " + summary))
                {
                    SaveGame(i);
                    SetMenuStatus(false);
                }
                y += 50;
            }
            if (GUI.Button(new Rect(10, y, slotWindowRect.width - 20, ButtonHeight), "Back"))
            {
                windowMode = WindowMode.Menu;
            }
        }

        private void LoadWindowFunction(int windowID)
        {
            int y = 60;
            for (int i = 0; i < numSavedGameSlots; i++)
            {
                var buttonRect = new Rect(10, y, slotWindowRect.width - 20, ButtonHeight);
                var summary = GetSlotSummary(i);
                GUI.enabled = !string.IsNullOrEmpty(summary);
                if (string.IsNullOrEmpty(summary)) summary = "(empty)";
                if (GUI.Button(buttonRect, "Slot " + i + ": " + summary))
                {
                    LoadGame(i);
                    SetMenuStatus(false);
                }
                GUI.enabled = true;
                y += 50;
            }
            if (GUI.Button(new Rect(10, y, slotWindowRect.width - 20, ButtonHeight), "Back"))
            {
                windowMode = WindowMode.Menu;
            }
        }

        private void ConfirmWindowFunction(int windowID)
        {
            int y = 60;
            float buttonWidth = (confirmRect.width - 40) / 2;
            GUI.Label(new Rect(10, y, confirmRect.width - 20, ButtonHeight), "Are you sure?");
            y += 50;
            if (GUI.Button(new Rect(10, y, buttonWidth, ButtonHeight), "Cancel"))
            {
                SetMenuStatus(false);
            }
            if (GUI.Button(new Rect(30 + buttonWidth, y, buttonWidth, ButtonHeight), "OK"))
            {
                switch (windowMode)
                {
                    case WindowMode.ConfirmQuitToTitle:
                        QuitToTitle();
                        break;
                    case WindowMode.ConfirmExitProgram:
                        ExitProgram();
                        break;
                }
                SetMenuStatus(false);
            }
        }

        private void SetMenuStatus(bool open)
        {
            isMenuOpen = open;
            if (open)
            {
                windowMode = WindowMode.Menu;
                windowRect = scaledRect.GetPixelRect();
                var slotWindowHeight = 110 + numSavedGameSlots * 50;
                slotWindowRect = new Rect((Screen.width - windowRect.width) / 2,
                                          (Screen.height - slotWindowHeight) / 2,
                                          windowRect.width, slotWindowHeight);
            }
            Time.timeScale = open ? 0 : 1;
            //Screen.lockCursor = !open;
            Tools.SetCursorActive(open);
        }

        private void SetupQuestLogWindow()
        {
            if (questLogWindow == null)
            {
                questLogWindow = FindObjectOfType<QuestLogWindow>();
                if (questLogWindow == null)
                {
                    Debug.LogWarning("No quest log window assigned to DSSaveLoad.");
                    return;
                }
            }
            if (questLogWindow.transform.parent == null && !questLogWindow.gameObject.activeInHierarchy)
            {
                GameObject go = Instantiate(questLogWindow.gameObject) as GameObject;
                if (go != null)
                {
                    go.transform.parent = DialogueManager.Instance.GetComponentInChildren<Canvas>().transform;
                    questLogWindow = go.GetComponent<QuestLogWindow>();
                }
            }
        }

        private bool IsQuestLogOpen()
        {
            return (questLogWindow != null) && questLogWindow.IsOpen;
        }

        private void OpenQuestLog()
        {
            if ((questLogWindow != null) && !IsQuestLogOpen())
            {
                questLogWindow.Open();
            }
        }

        /// <summary>
        /// Saves the Dialogue System state and ARPG data.
        /// </summary>
        private void SaveGame(int slot)
        {
            string saveData = PersistentDataManager.GetSaveData();
            string summary = OverrideActorName.GetActorName(player.transform) + System.DateTime.Now.ToString(" M/d h:mm");
            SaveSlot(slot, summary, saveData);
            Debug.Log("Dialogue System Save Game Data (" + summary + "): " + saveData);
            if (saveLoad != null) saveLoad.SendMessage("SaveData");
            DialogueManager.ShowAlert("Game Saved");
            windowMode = WindowMode.Save;
        }

        /// <summary>
        /// Loads the Dialogue System state and ARPG data.
        /// </summary>
        private void LoadGame(int slot)
        {
            string saveData = LoadSlot(slot);
            if (string.IsNullOrEmpty(saveData)) return;
            Debug.Log("Load Game Data: " + saveData);
            LevelManager levelManager = GetComponentInChildren<LevelManager>();
            if (levelManager == null) levelManager = DialogueManager.Instance.GetComponentInChildren<LevelManager>();
            if (levelManager != null) levelManager.LoadGame(saveData);
            if (saveLoad != null) saveLoad.SendMessage("LoadData");
            PersistentDataManager.ApplySaveData(saveData); // Apply DS data after, to handle PersistentPositionData.
            DialogueManager.ShowAlert("Game Loaded");
        }

        /// <summary>
        /// Quits the game, returning to the title level.
        /// </summary>
        public void QuitToTitle()
        {
            SetMenuStatus(false);
            Destroy(Camera.main.gameObject);
            Destroy(player);
            Tools.LoadLevel(titleLevelName); //---Was: Application.LoadLevel(titleLevelName);
        }

        public void ExitProgram()
        {
            Application.Quit();
        }

        /// <summary>
        /// Gets the slot summary, which is text shown in the slot's load button.
        /// This method uses PlayerPrefs. Override it if you want to save elsewhere
        /// such as a local file.
        /// </summary>
        /// <returns>The slot summary.</returns>
        /// <param name="slot">Slot.</param>
        public virtual string GetSlotSummary(int slot)
        {
            return PlayerPrefs.GetString(savedGameKey + slot + "_Summary");
        }

        /// <summary>
        /// Loads the saved data for a slot. This method uses PlayerPrefs.
        /// Override it if you want to load from elsewhere.
        /// </summary>
        /// <returns>The slot.</returns>
        /// <param name="slot">Slot.</param>
        public virtual string LoadSlot(int slot)
        {
            return PlayerPrefs.GetString(savedGameKey + slot);
        }

        /// <summary>
        /// Saves to a slot. This method uses PlayerPrefs.
        /// Override it if you want to save elsewhere.
        /// </summary>
        /// <param name="slot">Slot.</param>
        /// <param name="summary">Summary.</param>
        /// <param name="saveData">Save data.</param>
        public virtual void SaveSlot(int slot, string summary, string saveData)
        {
            PlayerPrefs.SetString(savedGameKey + slot, saveData);
            PlayerPrefs.SetString(savedGameKey + slot + "_Summary", summary);
        }

    }

}

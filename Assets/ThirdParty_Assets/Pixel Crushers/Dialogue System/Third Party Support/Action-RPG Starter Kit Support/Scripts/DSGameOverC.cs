using UnityEngine;
using System.Collections;

namespace PixelCrushers.DialogueSystem.ARPGSupport
{

    [AddComponentMenu("Pixel Crushers/Dialogue System/Third Party/Action-RPG Starter Kit/DS GameOver (Death prefab; replaces GameOverC)")]
    public class DSGameOverC : MonoBehaviour
    {

        public float delay = 3.0f;
        public GameObject player;
        public GUISkin guiSkin;
        public string gameOverText = "Game Over";
        public string reloadButtonText = "Reload";
        public string quitButtonText = "Quit Game";
        public string titleLevel = "Title";
        public string savedGameKey = "SavedGame";
        private bool menu = false;
        private Vector3 lastPosition;
        private Transform mainCam;
        GameObject oldPlayer;

        void Start()
        {
            StartCoroutine(Delay());
        }

        IEnumerator Delay()
        {
            yield return new WaitForSeconds(delay);
            menu = true;
            //Cursor.visible = true;
            //Screen.lockCursor = false;
            Tools.SetCursorActive(true);
        }

        void OnGUI()
        {
            if (menu)
            {
                if (guiSkin != null) GUI.skin = guiSkin;
                GUI.Box(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 120, 200, 160), gameOverText);
                if (PlayerPrefs.HasKey(savedGameKey) && GUI.Button(new Rect(Screen.width / 2 - 80, Screen.height / 2 - 80, 160, 40), reloadButtonText))
                {
                    LoadGame();
                }
                if (GUI.Button(new Rect(Screen.width / 2 - 80, Screen.height / 2 - 20, 160, 40), quitButtonText))
                {
                    mainCam = GameObject.FindWithTag("MainCamera").transform;
                    Destroy(mainCam.gameObject); //Destroy Main Camera                                                 
                    Tools.LoadLevel(titleLevel); //---Was: //Application.LoadLevel (titleLevel);
                    //---Was: Application.Quit();
                }
            }
        }

        /// <summary>
        /// Loads the Dialogue System state and ARPG data. Copied from DSSaveLoad.
        /// </summary>
        private void LoadGame()
        {
            if (PlayerPrefs.HasKey(savedGameKey))
            {
                string saveData = PlayerPrefs.GetString(savedGameKey);
                Debug.Log("Load Game Data: " + saveData);
                LevelManager levelManager = GetComponentInChildren<LevelManager>();
                if (levelManager == null) levelManager = DialogueManager.Instance.GetComponentInChildren<LevelManager>();
                if (levelManager != null) levelManager.LoadGame(saveData);
                LoadData();
                PersistentDataManager.ApplySaveData(saveData); // Apply DS data after, to handle PersistentPositionData.
                DialogueManager.ShowAlert("Game Loaded from PlayerPrefs");
            }
            else
            {
                DialogueManager.ShowAlert("Save a game first");
            }
        }

        void LoadData()
        {
            oldPlayer = GameObject.FindWithTag("Player");
            if (oldPlayer)
            {
                Destroy(gameObject);
            }
            {
                //}else{
                //lastPosition.x = PlayerPrefs.GetFloat("PlayerX");
                //lastPosition.y = PlayerPrefs.GetFloat("PlayerY");
                //lastPosition.z = PlayerPrefs.GetFloat("PlayerZ");
                //GameObject respawn = Instantiate(player, lastPosition , transform.rotation) as GameObject;
                //respawn.transform.position = lastPosition;
                GameObject respawn = Instantiate(player) as GameObject;
                respawn.GetComponent<StatusC>().level = PlayerPrefs.GetInt("TempPlayerLevel");
                respawn.GetComponent<StatusC>().atk = PlayerPrefs.GetInt("TempPlayerATK");
                respawn.GetComponent<StatusC>().def = PlayerPrefs.GetInt("TempPlayerDEF");
                respawn.GetComponent<StatusC>().matk = PlayerPrefs.GetInt("TempPlayerMATK");
                respawn.GetComponent<StatusC>().mdef = PlayerPrefs.GetInt("TempPlayerMDEF");
                respawn.GetComponent<StatusC>().mdef = PlayerPrefs.GetInt("TempPlayerMDEF");
                respawn.GetComponent<StatusC>().exp = PlayerPrefs.GetInt("TempPlayerEXP");
                respawn.GetComponent<StatusC>().maxExp = PlayerPrefs.GetInt("TempPlayerMaxEXP");
                respawn.GetComponent<StatusC>().maxHealth = PlayerPrefs.GetInt("TempPlayerMaxHP");
                //respawn.GetComponent<StatusC>().health = PlayerPrefs.GetInt("PlayerHP");
                respawn.GetComponent<StatusC>().health = PlayerPrefs.GetInt("TempPlayerMaxHP");
                respawn.GetComponent<StatusC>().maxMana = PlayerPrefs.GetInt("TempPlayerMaxMP");
                respawn.GetComponent<StatusC>().mana = PlayerPrefs.GetInt("TempPlayerMaxMP");
                respawn.GetComponent<StatusC>().statusPoint = PlayerPrefs.GetInt("TempPlayerSTP");
                mainCam = GameObject.FindWithTag("MainCamera").transform;
                mainCam.GetComponent<ARPGcameraC>().target = respawn.transform;
                //-------------------------------
                respawn.GetComponent<InventoryC>().cash = PlayerPrefs.GetInt("TempCash");
                int itemSize = player.GetComponent<InventoryC>().itemSlot.Length;
                int a = 0;
                if (itemSize > 0)
                {
                    while (a < itemSize)
                    {
                        respawn.GetComponent<InventoryC>().itemSlot[a] = PlayerPrefs.GetInt("TempItem" + a.ToString());
                        respawn.GetComponent<InventoryC>().itemQuantity[a] = PlayerPrefs.GetInt("TempItemQty" + a.ToString());
                        //-------
                        a++;
                    }
                }

                int equipSize = player.GetComponent<InventoryC>().equipment.Length;
                a = 0;
                if (equipSize > 0)
                {
                    while (a < equipSize)
                    {
                        respawn.GetComponent<InventoryC>().equipment[a] = PlayerPrefs.GetInt("TempEquipm" + a.ToString());
                        a++;
                    }
                }
                respawn.GetComponent<InventoryC>().weaponEquip = 0;
                respawn.GetComponent<InventoryC>().armorEquip = PlayerPrefs.GetInt("TempArmoEquip");
                if (PlayerPrefs.GetInt("TempWeaEquip") == 0)
                {
                    respawn.GetComponent<InventoryC>().RemoveWeaponMesh();
                }
                else
                {
                    respawn.GetComponent<InventoryC>().EquipItem(PlayerPrefs.GetInt("TempWeaEquip"), respawn.GetComponent<InventoryC>().equipment.Length + 5);
                }
                //----------------------------------
                //Screen.lockCursor = true;
                Tools.SetCursorActive(false);
                //--------------Set Target to Monster---------------
                GameObject[] mon;
                mon = GameObject.FindGameObjectsWithTag("Enemy");
                foreach (GameObject mo in mon)
                {
                    if (mo)
                    {
                        mo.GetComponent<AIsetC>().followTarget = respawn.transform;
                    }
                }
                //---------------Set Target to Minimap--------------
                GameObject minimap = GameObject.FindWithTag("Minimap");
                if (minimap)
                {
                    GameObject mapcam = minimap.GetComponent<MinimapOnOffC>().minimapCam;
                    mapcam.GetComponent<MinimapCameraC>().target = respawn.transform;
                }

                //Load Quest
                respawn.GetComponent<QuestStatC>().questProgress = new int[PlayerPrefs.GetInt("TempQuestSize")];
                int questSize = respawn.GetComponent<QuestStatC>().questProgress.Length;
                a = 0;
                if (questSize > 0)
                {
                    while (a < questSize)
                    {
                        respawn.GetComponent<QuestStatC>().questProgress[a] = PlayerPrefs.GetInt("TempQuestp" + a.ToString());
                        a++;
                    }
                }

                respawn.GetComponent<QuestStatC>().questSlot = new int[PlayerPrefs.GetInt("TempQuestSlotSize")];
                int questSlotSize = respawn.GetComponent<QuestStatC>().questSlot.Length;
                a = 0;
                if (questSlotSize > 0)
                {
                    while (a < questSlotSize)
                    {
                        respawn.GetComponent<QuestStatC>().questSlot[a] = PlayerPrefs.GetInt("TempQuestslot" + a.ToString());
                        a++;
                    }
                }
                //Load Skill Slot
                a = 0;
                while (a <= 2)
                {
                    respawn.GetComponent<SkillWindowC>().skill[a] = PlayerPrefs.GetInt("TempSkill" + a.ToString());
                    a++;
                }
                respawn.GetComponent<SkillWindowC>().AssignAllSkill();

                Destroy(gameObject);
            }
        }

    }

}
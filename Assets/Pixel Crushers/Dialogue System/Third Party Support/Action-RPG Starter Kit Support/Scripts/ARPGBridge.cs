using UnityEngine;
using PixelCrushers.DialogueSystem;

namespace PixelCrushers.DialogueSystem.ARPGSupport
{

    /// <summary>
    /// This provides a data bridge between ARPG and the Dialogue System.
    /// Add it to your Dialogue Manager object.
    /// </summary>
    [AddComponentMenu("Pixel Crushers/Dialogue System/Third Party/Action-RPG Starter Kit/ARPG Bridge (on Dialogue Manager)")]
    public class ARPGBridge : MonoBehaviour
    {

        public string playerNameInDatabase = "Player";

        /// <summary>
        /// If ticked, save data will include dialogue entry status (offered and/or spoken).
        /// </summary>
        public bool includeSimStatus = false;

        public bool debug = false;

        public static ARPGBridge instance { get; private set; }
        public GameObject player { get; private set; }
        public StatusC status { get; private set; }
        public InventoryC inventory { get; private set; }
        public SkillWindowC skillWindow { get; private set; }

        public bool showDebug { get { return debug && Debug.isDebugBuild; } }

        private bool monitorExpGains = false;

        void Awake()
        {
            instance = this;
            player = null;
            status = null;
            inventory = null;
        }

        void Start()
        {
            RegisterLuaFunctions();
            PersistentDataManager.includeSimStatus = includeSimStatus;
            GetPlayerComponents();
        }

        private void GetPlayerComponents()
        {
            if ((player != null) && player.activeInHierarchy) return;
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                if (DialogueDebug.LogWarnings) Debug.LogWarning(string.Format("{0}: ARPGBridge couldn't find the player object! Make sure the player prefab is tagged as 'Player' If the player hasn't been spawned yet, you can ignore this warning.", DialogueDebug.Prefix));
            }
            else
            {
                status = player.GetComponent<StatusC>();
                inventory = player.GetComponent<InventoryC>();
                skillWindow = player.GetComponent<SkillWindowC>();
            }
        }

        /// <summary>
        /// Prepares to run a conversation by freezing the player (if ticked), syncing data to Lua,
        /// and setting the NPC to "talking" mode.
        /// </summary>
        /// <param name="actor">The other actor.</param>
        public void OnConversationStart(Transform actor)
        {
            SyncToLua();
            monitorExpGains = true;
        }

        /// <summary>
        /// At the end of a conversation, unfreezes the player, syncs data back from Lua, and
        /// turns off the NPC's "talking" mode.
        /// </summary>
        /// <param name="actor">Actor.</param>
        public void OnConversationEnd(Transform actor)
        {
            SyncFromLua();
            monitorExpGains = false;
        }

        public void OnRecordPersistentData()
        {
            SyncToLua();
        }

        public void OnApplyPersistentData()
        {
            SyncFromLua();
            SyncMinimap();
        }

        /// <summary>
        /// Syncs data to Lua.
        /// </summary>
        public void SyncToLua()
        {
            GetPlayerComponents();
            SyncStatusToLua();
            SyncInventoryToLua();
            SyncSkillsToLua();
        }

        /// <summary>
        /// Syncs back from Lua.
        /// </summary>
        public void SyncFromLua()
        {
            GetPlayerComponents();
            SyncStatusFromLua();
            SyncInventoryFromLua();
            SyncSkillsFromLua();
        }

        private void SetPlayerField(string fieldName, int fieldValue)
        {
            if (showDebug) Debug.Log("Dialogue System ARPG: Set Actor[" + playerNameInDatabase + "]." + fieldName + " to " + fieldValue);
            DialogueLua.SetActorField(playerNameInDatabase, fieldName, fieldValue);
        }

        private int GetPlayerField(string fieldName)
        {
            var fieldValue = DialogueLua.GetActorField(playerNameInDatabase, fieldName).AsInt;
            if (showDebug) Debug.Log("Dialogue System ARPG: Actor[" + playerNameInDatabase + "]." + fieldName + " is " + fieldValue);
            return fieldValue;
        }

        private void SyncStatusToLua()
        {
            if (status == null) return;
            SetPlayerField("level", status.level);
            SetPlayerField("atk", status.atk);
            SetPlayerField("def", status.def);
            SetPlayerField("matk", status.matk);
            SetPlayerField("mdef", status.mdef);
            SetPlayerField("exp", status.exp);
            SetPlayerField("maxExp", status.maxExp);
            SetPlayerField("health", status.health);
            SetPlayerField("maxHealth", status.maxHealth);
            SetPlayerField("mana", status.mana);
            SetPlayerField("maxMana", status.maxMana);
            SetPlayerField("statusPoint", status.statusPoint);
        }

        private void SyncStatusFromLua()
        {
            if (status == null) return;
            status.level = GetPlayerField("level");
            status.atk = GetPlayerField("atk");
            status.def = GetPlayerField("def");
            status.matk = GetPlayerField("matk");
            status.mdef = GetPlayerField("mdef");
            int newExp = GetPlayerField("exp");
            if (monitorExpGains && (newExp != status.exp))
            {
                status.gainEXP(newExp - status.exp);
            }
            else
            {
                status.exp = newExp;
            }
            status.maxExp = GetPlayerField("maxExp");
            status.health = GetPlayerField("health");
            status.maxHealth = GetPlayerField("maxHealth");
            status.mana = GetPlayerField("mana");
            status.maxMana = GetPlayerField("maxMana");
            status.statusPoint = GetPlayerField("statusPoint");
        }

        private void SyncInventoryToLua()
        {
            if (inventory == null) return;
            SetPlayerField("cash", inventory.cash);

            //=====================================================================
            int itemSize = inventory.itemSlot.Length;
            int a = 0;
            if (itemSize > 0)
            {
                while (a < itemSize)
                {
                    SetPlayerField("Item" + a.ToString(), inventory.itemSlot[a]);
                    SetPlayerField("ItemQty" + a.ToString(), inventory.itemQuantity[a]);
                    a++;
                }
            }

            int equipSize = inventory.equipment.Length;
            a = 0;
            if (equipSize > 0)
            {
                while (a < equipSize)
                {
                    SetPlayerField("Equipm" + a.ToString(), inventory.equipment[a]);
                    a++;
                }
            }
            SetPlayerField("WeaEquip", inventory.weaponEquip);
            SetPlayerField("ArmoEquip", inventory.armorEquip);
            //=====================================================================
            /*
			for (int i = 0; i < inventory.itemSlot.Length; i++) {
				SetPlayerField("Item" + i.ToString(), inventory.itemSlot[i]);
				SetPlayerField("ItemQty" + i.ToString(), inventory.itemQuantity[i]);
			}

			int equipSize = inventory.equipment.Length;
			int a = 0;
			if (equipSize > 0){
				while (a < equipSize){
					SetPlayerField("Equipm" + a.ToString(), inventory.equipment[a]);
					a++;
				}
			}
			SetPlayerField("WeaEquip", inventory.weaponEquip);
			SetPlayerField("ArmoEquip", inventory.armorEquip);
			*/
        }

        private void SyncInventoryFromLua(bool equip = true)
        {
            if (inventory == null) return;
            inventory.cash = GetPlayerField("cash");

            //=====================================================================
            int itemSize = inventory.itemSlot.Length;
            int a = 0;
            if (itemSize > 0)
            {
                while (a < itemSize)
                {
                    inventory.itemSlot[a] = GetPlayerField("Item" + a.ToString());
                    inventory.itemQuantity[a] = GetPlayerField("ItemQty" + a.ToString());
                    //-------
                    a++;
                }
            }

            int equipSize = inventory.equipment.Length;
            a = 0;
            if (equipSize > 0)
            {
                while (a < equipSize)
                {
                    inventory.equipment[a] = GetPlayerField("Equipm" + a.ToString());
                    a++;
                }
            }
            inventory.weaponEquip = 0;
            inventory.armorEquip = GetPlayerField("ArmoEquip");
            if (GetPlayerField("WeaEquip") == 0)
            {
                inventory.RemoveWeaponMesh();
            }
            else
            {
                inventory.EquipItem(GetPlayerField("WeaEquip"), inventory.equipment.Length + 5);
            }
            //=====================================================================
            /*
			for (int i = 0; i < inventory.itemSlot.Length; i++) {
				inventory.itemSlot[i] = GetPlayerField("Item" + i.ToString());
				inventory.itemQuantity[i] = GetPlayerField("ItemQty" + i.ToString());
			}

			int equipSize = inventory.equipment.Length;
			int a = 0;
			if(equipSize > 0){
				while(a < equipSize){
					inventory.equipment[a] = GetPlayerField("Equipm" + a.ToString());
					a++;
				}
			}
			inventory.weaponEquip = 0;
			inventory.armorEquip = GetPlayerField("ArmoEquip");
			if(GetPlayerField("WeaEquip") == 0){
				inventory.RemoveWeaponMesh();
			}else{
				inventory.EquipItem(GetPlayerField("WeaEquip") , inventory.equipment.Length + 5);
			}
			*/
        }

        private void SyncSkillsToLua()
        {
            if (skillWindow == null) return;
            for (int i = 0; i <= 2; i++)
            {
                SetPlayerField("Skill" + i.ToString(), skillWindow.skill[i]);
            }
            for (int i = 0; i < skillWindow.skillListSlot.Length; i++)
            {
                SetPlayerField("SkillList" + i.ToString(), skillWindow.skillListSlot[i]);
            }
        }

        private void SyncSkillsFromLua()
        {
            if (skillWindow == null) return;
            for (int i = 0; i <= 2; i++)
            {
                skillWindow.skill[i] = GetPlayerField("Skill" + i.ToString());
            }
            for (int i = 0; i < skillWindow.skillListSlot.Length; i++)
            {
                skillWindow.skillListSlot[i] = GetPlayerField("SkillList" + i.ToString());
            }
            skillWindow.AssignAllSkill();
        }

        private void SyncMinimap()
        {
            var minimap = GameObject.FindWithTag("Minimap");
            if (minimap == null) return;
            var mapcam = minimap.GetComponent<MinimapOnOffC>().minimapCam;
            mapcam.GetComponent<MinimapCameraC>().target = GameObject.FindWithTag("Player").transform;
        }

        private void RegisterLuaFunctions()
        {
            Lua.RegisterFunction("SetPlayerLevel", null, SymbolExtensions.GetMethodInfo(() => SetPlayerLevel((double)0)));
            Lua.RegisterFunction("SetPlayerAtk", null, SymbolExtensions.GetMethodInfo(() => SetPlayerAtk((double)0)));
            Lua.RegisterFunction("SetPlayerDef", null, SymbolExtensions.GetMethodInfo(() => SetPlayerDef((double)0)));
            Lua.RegisterFunction("SetPlayerMAtk", null, SymbolExtensions.GetMethodInfo(() => SetPlayerMAtk((double)0)));
            Lua.RegisterFunction("SetPlayerMDef", null, SymbolExtensions.GetMethodInfo(() => SetPlayerMDef((double)0)));
            Lua.RegisterFunction("SetPlayerExp", null, SymbolExtensions.GetMethodInfo(() => SetPlayerExp((double)0)));
            Lua.RegisterFunction("AdjustPlayerExp", null, SymbolExtensions.GetMethodInfo(() => AdjustPlayerExp((double)0)));
            Lua.RegisterFunction("SetPlayerMaxExp", null, SymbolExtensions.GetMethodInfo(() => SetPlayerMaxExp((double)0)));
            Lua.RegisterFunction("SetPlayerHealth", null, SymbolExtensions.GetMethodInfo(() => SetPlayerHealth((double)0)));
            Lua.RegisterFunction("AdjustPlayerHealth", null, SymbolExtensions.GetMethodInfo(() => AdjustPlayerHealth((double)0, (double)0)));
            Lua.RegisterFunction("SetPlayerMaxHealth", null, SymbolExtensions.GetMethodInfo(() => SetPlayerMaxHealth((double)0)));
            Lua.RegisterFunction("SetPlayerMana", null, SymbolExtensions.GetMethodInfo(() => SetPlayerMana((double)0)));
            Lua.RegisterFunction("SetPlayerMaxMana", null, SymbolExtensions.GetMethodInfo(() => SetPlayerMaxMana((double)0)));
            Lua.RegisterFunction("SetPlayerStatusPoint", null, SymbolExtensions.GetMethodInfo(() => SetPlayerStatusPoint((double)0)));
            Lua.RegisterFunction("GetItemCount", null, SymbolExtensions.GetMethodInfo(() => GetItemCount((double)0)));
            Lua.RegisterFunction("AddItem", null, SymbolExtensions.GetMethodInfo(() => AddItem((double)0, (double)0)));
            Lua.RegisterFunction("RemoveItem", null, SymbolExtensions.GetMethodInfo(() => RemoveItem((double)0, (double)0)));
            Lua.RegisterFunction("HasEquipment", null, SymbolExtensions.GetMethodInfo(() => HasEquipment((double)0)));
            Lua.RegisterFunction("AddEquipment", null, SymbolExtensions.GetMethodInfo(() => AddEquipment((double)0)));
            Lua.RegisterFunction("RemoveEquipment", null, SymbolExtensions.GetMethodInfo(() => RemoveEquipment((double)0)));
        }

        public static void SetPlayerLevel(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null) instance.status.level = (int)value;
            DialogueLua.SetActorField(instance.playerNameInDatabase, "level", value);
        }

        public static void SetPlayerAtk(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null) instance.status.atk = (int)value;
            DialogueLua.SetActorField(instance.playerNameInDatabase, "atk", value);
        }

        public static void SetPlayerDef(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null) instance.status.def = (int)value;
            DialogueLua.SetActorField(instance.playerNameInDatabase, "def", value);
        }

        public static void SetPlayerMAtk(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null) instance.status.matk = (int)value;
            DialogueLua.SetActorField(instance.playerNameInDatabase, "matk", value);
        }

        public static void SetPlayerMDef(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null) instance.status.mdef = (int)value;
            DialogueLua.SetActorField(instance.playerNameInDatabase, "mdef", value);
        }

        public static void SetPlayerExp(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null)
            {
                AdjustPlayerExp((int)value - instance.status.exp);
            }
            else
            {
                DialogueLua.SetActorField(instance.playerNameInDatabase, "exp", value);
            }
        }

        public static void AdjustPlayerExp(double amount)
        {
            instance.GetPlayerComponents();
            if (instance.status != null)
            {
                if (amount < 0)
                {
                    instance.status.exp -= (int)amount;
                }
                else
                {
                    instance.status.gainEXP((int)amount);
                }
            }
            DialogueLua.SetActorField(instance.playerNameInDatabase, "exp", instance.status.exp);
        }

        public static void SetPlayerMaxExp(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null) instance.status.maxExp = (int)value;
            DialogueLua.SetActorField(instance.playerNameInDatabase, "maxExp", value);
        }

        public static void SetPlayerHealth(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null)
            {
                AdjustPlayerHealth((int)value - instance.status.health, 0);
            }
            else
            {
                DialogueLua.SetActorField(instance.playerNameInDatabase, "health", value);
            }
        }

        public static void AdjustPlayerHealth(double amount, double element)
        {
            instance.GetPlayerComponents();
            if (instance.status != null)
            {
                if (amount < 0)
                {
                    instance.status.OnDamage(Mathf.Abs((int)amount), (int)element);
                }
                else
                {
                    instance.status.Heal((int)amount, (int)element);
                }
            }
            DialogueLua.SetActorField(instance.playerNameInDatabase, "health", instance.status.health);
        }

        public static void SetPlayerMaxHealth(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null) instance.status.maxHealth = (int)value;
            DialogueLua.SetActorField(instance.playerNameInDatabase, "maxHealth", value);
        }

        public static void SetPlayerMana(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null) instance.status.mana = (int)value;
            DialogueLua.SetActorField(instance.playerNameInDatabase, "mana", value);
        }

        public static void SetPlayerMaxMana(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null) instance.status.maxMana = (int)value;
            DialogueLua.SetActorField(instance.playerNameInDatabase, "maxMana", value);
        }

        public static void SetPlayerStatusPoint(double value)
        {
            instance.GetPlayerComponents();
            if (instance.status != null) instance.status.statusPoint = (int)value;
            DialogueLua.SetActorField(instance.playerNameInDatabase, "statusPoint", value);
        }

        public static int GetItemCount(double itemID)
        {
            instance.GetPlayerComponents();
            int total = 0;
            if (instance.inventory != null)
            {
                for (int i = 0; i < instance.inventory.equipment.Length; i++)
                {
                    if (instance.inventory.itemSlot[i] == (int)itemID)
                    {
                        total += instance.inventory.itemQuantity[i];
                    }
                }
            }
            return total;
        }

        public static void AddItem(double itemID, double quantity)
        {
            instance.GetPlayerComponents();
            if (instance.inventory != null)
            {
                int intItemID = (int)itemID;
                if (DialogueManager.IsConversationActive) instance.SyncInventoryFromLua(false);
                instance.inventory.AddItem(intItemID, (int)quantity);
                if (DialogueManager.IsConversationActive) instance.SyncInventoryToLua();
            }
        }

        public static void RemoveItem(double itemID, double quantity)
        {
            instance.GetPlayerComponents();
            int intItemID = (int)itemID;
            int total = 0;
            if (instance.inventory != null)
            {
                if (DialogueManager.IsConversationActive) instance.SyncInventoryFromLua(false);
                for (int i = 0; i < instance.inventory.itemSlot.Length; i++)
                {
                    if (instance.inventory.itemSlot[i] == intItemID)
                    {
                        total += instance.inventory.itemQuantity[i];
                        instance.inventory.itemQuantity[i] = Mathf.Max(0, instance.inventory.itemQuantity[i] - (int)quantity);
                        if (instance.inventory.itemQuantity[i] == 0)
                        {
                            instance.inventory.itemSlot[i] = 0;
                        }
                        if (total >= (int)quantity) break;
                    }
                }
                if (DialogueManager.IsConversationActive) instance.SyncInventoryToLua();
            }
        }

        public static bool HasEquipment(double itemID)
        {
            instance.GetPlayerComponents();
            if (instance.inventory != null)
            {
                if (instance.inventory.weaponEquip == (int)itemID)
                {
                    return true;
                }
                for (int i = 0; i < instance.inventory.equipment.Length; i++)
                {
                    if (instance.inventory.equipment[i] == (int)itemID)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static void AddEquipment(double itemID)
        {
            instance.GetPlayerComponents();
            if (instance.inventory != null)
            {
                int intItemID = (int)itemID;
                if (DialogueManager.IsConversationActive) instance.SyncInventoryFromLua(false);
                instance.inventory.AddEquipment(intItemID);
                if (DialogueManager.IsConversationActive) instance.SyncInventoryToLua();
            }
        }

        public static void RemoveEquipment(double itemID)
        {
            instance.GetPlayerComponents();
            if (instance.inventory != null)
            {
                int intItemID = (int)itemID;
                if (DialogueManager.IsConversationActive) instance.SyncInventoryFromLua(false);
                if (instance.inventory.weaponEquip == intItemID)
                {
                    instance.inventory.UnEquip((int)itemID);
                }
                for (int i = 0; i < instance.inventory.equipment.Length; i++)
                {
                    if (instance.inventory.equipment[i] == intItemID)
                    {
                        instance.inventory.equipment[i] = 0;
                        break;
                    }
                }
                instance.inventory.AutoSortEquipment();
                if (DialogueManager.IsConversationActive) instance.SyncInventoryToLua();
            }
        }

    }

}

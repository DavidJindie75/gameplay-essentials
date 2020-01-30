using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace PixelCrushers.DialogueSystem.ARPGSupport
{

    static public class ActionRPGKitMenuItems
    {

        [MenuItem("Tools/Pixel Crushers/Dialogue System/Third Party/Action-RPG Starter Kit/Setup Player Prefab")]
        public static void SetupPlayerPrefab()
        {
            if (Selection.activeGameObject == null)
            {
                Debug.LogError(string.Format("{0}: Select the player prefab first.", DialogueDebug.Prefix));
            }
            else
            {
                var inputController = Selection.activeGameObject.GetComponent<PlayerInputControllerC>();
                if (inputController == null)
                {
                    Debug.LogError(string.Format("{0}: Select the player prefab first. It should have all the regular Action-RPG Kit components already.", DialogueDebug.Prefix));
                }
                else
                {
                    GameObject player = Selection.activeGameObject;

                    // Add DSSaveLoad:
                    if (player.GetComponent<DSSaveLoad>() == null)
                    {
                        player.AddComponent<DSSaveLoad>();
                        Debug.Log(string.Format("{0}: On DSSave Load: Remember to set the GUI Skin and Quest Log Window properties.", DialogueDebug.Prefix));
                    }

                    // Add ProximitySelector:
                    if (player.GetComponent<ProximitySelector>() == null)
                    {
                        var proximitySelector = player.AddComponent<ProximitySelector>();
                        proximitySelector.defaultUseMessage = "(E to interact)";
                        proximitySelector.useKey = KeyCode.E;
                        proximitySelector.useButton = string.Empty;
                    }

                    // Add SetComponentEnabledOnDialogueEvent:
                    if (player.GetComponent<SetComponentEnabledOnDialogueEvent>() == null)
                    {
                        var setEnabled = player.AddComponent<SetComponentEnabledOnDialogueEvent>();
                        setEnabled.trigger = DialogueEvent.OnConversation;
                        List<SetComponentEnabledOnDialogueEvent.SetComponentEnabledAction> disableList = new List<SetComponentEnabledOnDialogueEvent.SetComponentEnabledAction>();
                        List<SetComponentEnabledOnDialogueEvent.SetComponentEnabledAction> reenableList = new List<SetComponentEnabledOnDialogueEvent.SetComponentEnabledAction>();
                        AddSetEnabledAction(disableList, reenableList, player.GetComponent<SkillWindowC>(), "SkillWindowC");
                        AddSetEnabledAction(disableList, reenableList, player.GetComponent<InventoryC>(), "InventoryC");
                        if (player.GetComponent<PlayerAnimationC>() != null)
                        {
                            AddSetEnabledAction(disableList, reenableList, player.GetComponent<PlayerAnimationC>(), "PlayerAnimationC");
                        }
                        if (player.GetComponent<PlayerMecanimAnimationC>() != null)
                        {
                            AddSetEnabledAction(disableList, reenableList, player.GetComponent<PlayerMecanimAnimationC>(), "PlayerMecanimAnimationC");
                        }
                        AddSetEnabledAction(disableList, reenableList, player.GetComponent<PlayerInputControllerC>(), "PlayerInputControllerC");
                        AddSetEnabledAction(disableList, reenableList, player.GetComponent<AttackTriggerC>(), "AttackTriggerC");
                        AddSetEnabledAction(disableList, reenableList, player.GetComponent<QuestStatC>(), "QuestStatC");
                        AddSetEnabledAction(disableList, reenableList, player.GetComponent<StatusWindowC>(), "StatusWindowC");
                        AddSetEnabledAction(disableList, reenableList, player.GetComponent<DSSaveLoad>(), "DSSaveLoad");
                        AddSetEnabledAction(disableList, reenableList, player.GetComponent<ProximitySelector>(), "ProximitySelector");
                        AddSetEnabledAction(disableList, reenableList, player.GetComponent<CharacterMotorC>(), "CharacterMotorC");
                        setEnabled.onStart = disableList.ToArray();
                        setEnabled.onEnd = reenableList.ToArray();
                    }

                    // Add SetAnimation (legacy):
                    if (player.GetComponent<PlayerAnimationC>() != null)
                    {
                        if (player.GetComponent<SetAnimationOnDialogueEvent>() == null)
                        {
                            var setAnim = player.AddComponent<SetAnimationOnDialogueEvent>();
                            setAnim.trigger = DialogueEvent.OnConversation;
                            setAnim.onStart = new SetAnimationOnDialogueEvent.SetAnimationAction[1];
                            setAnim.onStart[0] = new SetAnimationOnDialogueEvent.SetAnimationAction();
                            setAnim.onStart[0].target = player.transform;
                            Debug.Log(string.Format("{0}: On Set Animation On Dialogue Event: Remember to set the idle animation clip.", DialogueDebug.Prefix));
                        }
                    }

                    // Add SetAnimatorState (Mecanim):
                    if (player.GetComponent<PlayerMecanimAnimationC>() != null)
                    {
                        if (player.GetComponent<StopPlayerMecanimOnConversation>() == null)
                        {
                            player.AddComponent<StopPlayerMecanimOnConversation>();
                        }
                        //if (player.GetComponent<SetAnimatorStateOnDialogueEvent>() == null) {
                        //                      var setAnimState = player.AddComponent<SetAnimatorStateOnDialogueEvent>();
                        //                      setAnimState.trigger = DialogueEvent.OnConversation;
                        //                      setAnimState.onStart = new SetAnimatorStateOnDialogueEvent.SetAnimatorStateAction[1];
                        //                      setAnimState.onStart[0] = new SetAnimatorStateOnDialogueEvent.SetAnimatorStateAction();
                        //                      setAnimState.onStart[0].target = player.transform;
                        //                      Debug.Log(string.Format("{0}: On Set Animator State On Dialogue Event: Remember to set the idle animation state.", DialogueDebug.Prefix));
                        //                  }
                    }

                    // Add PauseARPGCameraOnConversation:
                    if (player.GetComponent<PauseARPGCameraOnConversation>() == null)
                    {
                        player.AddComponent<PauseARPGCameraOnConversation>();
                    }

                    // Add ShowCursorOnConversation:
                    if (player.GetComponent<ShowCursorOnConversation>() == null)
                    {
                        player.AddComponent<ShowCursorOnConversation>();
                    }

                    // Add OverrideActorName:
                    if (player.GetComponent<DialogueActor>() == null)
                    {
                        var dialogueActor = player.AddComponent<DialogueActor>();
                        dialogueActor.actor = "Player";
                    }

                    Selection.activeGameObject = player;
                }
            }
        }

        private static void AddSetEnabledAction(
            List<SetComponentEnabledOnDialogueEvent.SetComponentEnabledAction> disableList,
            List<SetComponentEnabledOnDialogueEvent.SetComponentEnabledAction> reenableList,
            MonoBehaviour monoBehaviour,
            string behaviourName)
        {
            if (monoBehaviour == null)
            {
                Debug.LogWarning(string.Format("{0}: No {1} found on the player.", DialogueDebug.Prefix, behaviourName));
            }
            else
            {
                SetComponentEnabledOnDialogueEvent.SetComponentEnabledAction disableAction = new SetComponentEnabledOnDialogueEvent.SetComponentEnabledAction();
                SetComponentEnabledOnDialogueEvent.SetComponentEnabledAction reenableAction = new SetComponentEnabledOnDialogueEvent.SetComponentEnabledAction();
                disableAction.target = monoBehaviour;
                reenableAction.target = monoBehaviour;
                disableAction.state = Toggle.False;
                reenableAction.state = Toggle.True;
                disableList.Add(disableAction);
                reenableList.Add(reenableAction);
            }
        }

    }
}

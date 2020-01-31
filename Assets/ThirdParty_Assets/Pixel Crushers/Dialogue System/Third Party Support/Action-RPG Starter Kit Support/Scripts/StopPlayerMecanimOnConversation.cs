using UnityEngine;

namespace PixelCrushers.DialogueSystem.ARPGSupport
{

    /// <summary>
    /// Stops ARPG's player mecanim on conversations.
    /// </summary>
    [AddComponentMenu("Pixel Crushers/Dialogue System/Third Party/Action-RPG Starter Kit/Stop Player Mecanim On Conversation (Player prefab)")]
    public class StopPlayerMecanimOnConversation : MonoBehaviour
    {

        public void OnConversationStart(Transform actor)
        {
            var playerMecanim = GetComponent<PlayerMecanimAnimationC>();
            if (playerMecanim == null) return;
            playerMecanim.enabled = false;
            if (playerMecanim.animator == null) return;
            //--- Not present in ARPG 6.0: playerMecanim.animator.SetBool(playerMecanim.hurtState, false);
            playerMecanim.animator.SetBool(playerMecanim.jumpState, false);
            playerMecanim.animator.SetFloat(playerMecanim.moveHorizontalState, 0);
            playerMecanim.animator.SetFloat(playerMecanim.moveVerticalState, 0);
        }

        public void OnConversationEnd(Transform actor)
        {
            var playerMecanim = GetComponent<PlayerMecanimAnimationC>();
            if (playerMecanim == null) return;
            playerMecanim.enabled = true;
        }

    }
}
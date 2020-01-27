using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AttackTriggerC))]
[RequireComponent(typeof(PlayerInputControllerC))]
[AddComponentMenu("Action-RPG Kit(C#)/Create Player(Mecanim)")]
[RequireComponent(typeof(ClickBasedMove))]

public class PlayerMecanimAnimationC : MonoBehaviour {

	private GameObject player;
	private GameObject mainModel;
	public Animator animator;
	private CharacterController controller;
	private ClickBasedMove clickController;

	public string moveHorizontalState = "horizontal";
	public string moveVerticalState = "vertical";
	public string jumpState = "jump";
	private bool jumping = false;
	private bool attacking = false;
	private bool flinch = false;

	public JoystickCanvas joyStick;// For Mobile
	private float moveHorizontal;
	private float moveVertical;
	private StatusC stat;

	void Start() {
		if (!player) {
			player = this.gameObject;
		}
		mainModel = GetComponent<AttackTriggerC>().mainModel;
		if (!mainModel) {
			mainModel = this.gameObject;
		}
		if (!animator) {
			animator = mainModel.GetComponent<Animator>();
		}
		clickController = player.GetComponent<ClickBasedMove>();
		controller = player.GetComponent<CharacterController>();
		GetComponent<AttackTriggerC>().useMecanim = true;
		stat = GetComponent<StatusC>();
	}

	void Update() {
		//Set attacking variable = onAttacking in AttackTrigger
		attacking = GetComponent<AttackTriggerC>().onAttacking;
		flinch = GetComponent<AttackTriggerC>().flinch;

		if (attacking || flinch || GlobalConditionC.freezeAll || GlobalConditionC.freezePlayer || stat.dodge) {
			return;
		}

		if (clickController.isMoving)
		{
			animator.SetFloat(moveVerticalState, 0.3f);
			PlayAnim("run");
		}
		else animator.SetFloat(moveVerticalState, 0.0f);

	}

	/*--Animation list:
	 * "run" = forward run
	 * 
	 */
	
	public void AttackAnimation(string anim){
		animator.SetBool(jumpState , false);
		animator.Play(anim);
	}
	
	public void PlayAnim(string anim){
		animator.Play(anim);
	}
	
	public void SetWeaponType(int val){
		mainModel = GetComponent<AttackTriggerC>().mainModel;
		if(!mainModel){
			mainModel = this.gameObject;
		}
		if(!animator){
			animator = mainModel.GetComponent<Animator>();
		}
		animator.SetInteger("weaponType" , val);
		animator.SetTrigger("changeWeapon");
	}
}
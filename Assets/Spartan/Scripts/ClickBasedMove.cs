using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ClickBasedMove : MonoBehaviour
{
    //movements
    [Header("Movement")]
    [Space(10)]
    public float walkSpeed = 6.0f;
    public float sprintSpeed = 12.0f;
    public bool canSprint = true;
    private bool sprint = false;
    public bool canControl = true;
    public bool unableToMove = false;
    public bool wasMoving;
    private GameObject mainModel;

    [HideInInspector]//stamina
    public bool recover = false;
    private float staminaRecover = 1.4f;
    private float useStamina = 0.04f;

    [Header("Effects & Indicators")]
    [Space(10)]
    public GameObject DestinationEffect;
    bool ClickEffectUsed;
    float MouseUpClickEffectTimer = 0f;
    Vector3 LastMousePosition;

    public Texture2D staminaGauge;
    public Texture2D staminaBorder;

    public float maxStamina = 100.0f;
    public float stamina = 100.0f;

    private bool useMecanim = true;

    [SerializeField] //layer to walk
    private LayerMask moveableground;

    [SerializeField]//camera & targeting
    private Camera cam;
    public bool isMoving;
    public NavMeshAgent pathFinderAgent;
    public bool enemyTargeted;
    public bool closeToEnemy;
    public bool dontRepeat;
    public GameObject target;

    Ray theRay;
    RaycastHit rayCastInfo;

    private Vector3 currPanDirection = Vector3.zero; //holds the current and last direction for the next movement
    private Vector3 lastPanDirection = Vector3.zero;
    public bool IsPanning() { return currPanDirection != Vector3.zero; } //is the camera moving according to the panning inputs?

    [System.Serializable]//dodge settings
    public class DodgeSetting
    {
        public bool canDodgeRoll = false;
        public int staminaUse = 25;

        public AnimationClip dodgeForward;
        public AnimationClip dodgeLeft;
        public AnimationClip dodgeRight;
        public AnimationClip dodgeBack;
    }
    public DodgeSetting dodgeRollSetting;
    [HideInInspector]
    public bool dodging = false;

    private float lastTime = 0.0f;
    [HideInInspector]
    public float recoverStamina = 0.0f;
    private Vector3 dir = Vector3.forward;

    void Awake()
    {
        pathFinderAgent = GetComponent<NavMeshAgent>();
        stamina = maxStamina;
        if (!mainModel)
        {
            mainModel = GetComponent<StatusC>().mainModel;
        }
        useMecanim = GetComponent<AttackTriggerC>().useMecanim;

    }

    void FixedUpdate()
    {
        New_Input();
        FreezeMovement();
        RightClickInput();
        CloseToEnemyCheck();
        DodgeRoll_Input();
    }

    void FreezeMovement()
    {
        if (unableToMove)
        {
            if (isMoving)
            {
                NavMeshExtensions.StopMoving(GetComponent<ClickBasedMove>().pathFinderAgent);
                wasMoving = true;
            }
        }
        else
        {
            if (wasMoving)
            {
                wasMoving = false;
            }
        }
    }

    void RightClickInput()
    {
        if (Input.GetKeyDown(KeyCode.Mouse1) || Input.GetMouseButton(1))
        {
            theRay = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(theRay, out rayCastInfo, 1000f))
            {
                target = rayCastInfo.collider.gameObject;

                if (target.tag == StaticTags.Enemy)
                {
                    enemyTargeted = true;

                    //if the enemy is our destination,  make sure we set the stopping distance to our offset
                    pathFinderAgent.stoppingDistance = GetComponent<AttackTriggerC>().requiredDistanceForAttack;
                }
                else
                {
                    if (target.tag != StaticTags.Enemy)
                    {
                        if (enemyTargeted)
                            enemyTargeted = false;

                        if (closeToEnemy)
                            closeToEnemy = false;
                    }
                    else
                    {
                        isMoving = false;
                        NavMeshExtensions.StopAgent(pathFinderAgent);
                    }
                }

            }

        }

        if (enemyTargeted)
        {
            MoveToAttackHeavyAttack();
        }

        if (pathFinderAgent.remainingDistance <= pathFinderAgent.stoppingDistance)
            NavMeshExtensions.StopAgent(pathFinderAgent);
        else
            NavMeshExtensions.ResumeAgent(pathFinderAgent);

        if (pathFinderAgent.remainingDistance <= pathFinderAgent.stoppingDistance) { isMoving = false; }

    }

    void New_Input()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0) || Input.GetMouseButton(0))
        {
            StatusC stat = GetComponent<StatusC>();

            MouseUpClickEffectTimer += Time.deltaTime;

            if (recover && !sprint && !dodging)
            {
                if (recoverStamina >= staminaRecover)
                {
                    StaminaRecovery();
                }
                else
                {
                    recoverStamina += Time.deltaTime;
                }
            }

            //TODO:
            //instead of detecting our keypress to start the sprint,  detect if our mouse is min x away from player..
            //
            if (Input.GetKeyDown(KeyCode.LeftShift) && !stat.freeze && !GlobalConditionC.freezeAll && !GlobalConditionC.freezePlayer && stat.canControl && canSprint)
            {
                sprint = true;
                if (sprint)
                {
                    pathFinderAgent.speed = sprintSpeed;
                }
                else pathFinderAgent.speed = walkSpeed;
            }
            //halt the sprint if needed
            if (stat.freeze || GlobalConditionC.freezeAll || GlobalConditionC.freezePlayer || !stat.canControl)
            {
                //inputMoveDirection = new Vector3(0, 0, 0);
                if (sprint)
                {
                    sprint = false;
                    recover = true;
                    pathFinderAgent.speed = walkSpeed;
                    recoverStamina = 0.0f;
                }
                return;
            }
            if (Time.timeScale == 0.0f)
            {
                return;
            }
            if (dodging && !unableToMove)
            {
                Vector3 fwd = transform.TransformDirection(dir);
                pathFinderAgent.Move(fwd * 8 * Time.deltaTime);
                return;
            }

            theRay = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(theRay, out rayCastInfo, 1000f))
            {
                target = rayCastInfo.collider.gameObject;

                if (target.tag == StaticTags.Enemy)
                {


                    enemyTargeted = true;

                    //if the enemy is our destination,  make sure we set the stopping distance to our offset
                    pathFinderAgent.stoppingDistance = GetComponent<AttackTriggerC>().requiredDistanceForAttack;
                }
                else
                {
                    if (target.tag != StaticTags.Enemy)
                    {
                        //if not,  go back to regular stopping distance
                        pathFinderAgent.stoppingDistance = 0;
                        isMoving = true;
                        NavMeshExtensions.GoToSetPosition(rayCastInfo.point, pathFinderAgent);
                        pathFinderAgent.destination = rayCastInfo.point;
                        LastMousePosition = rayCastInfo.point;

                        ClickEffect();// click particle system effect

                        if (GetComponent<AttackTriggerC>().queueLightAttack)
                            GetComponent<AttackTriggerC>().queueLightAttack = false;

                        if (dontRepeat)
                            dontRepeat = false;

                        if (enemyTargeted)
                            enemyTargeted = false;

                        if (closeToEnemy)
                            closeToEnemy = false;
                    }
                    else
                    {
                        isMoving = false;
                        NavMeshExtensions.StopAgent(pathFinderAgent);
                    }
                }
            }
            //TODO:
            // instead of detecting our keypress to start the sprint,  detect if our mouse is min x away from player..
            //Cancel Sprint
            if (sprint && Input.GetAxis("Vertical") < 0.02f || sprint && stamina <= 0 || sprint && Input.GetButtonDown("Fire1") || sprint && Input.GetKeyUp(KeyCode.LeftShift))
            {
                sprint = false;
                recover = true;
                pathFinderAgent.speed = walkSpeed;
                recoverStamina = 0.0f;
            }

        }

        if (enemyTargeted)
        {
            MoveToAttack();
        }

        if (pathFinderAgent.remainingDistance <= pathFinderAgent.stoppingDistance)
            NavMeshExtensions.StopAgent(pathFinderAgent);
        else
            NavMeshExtensions.ResumeAgent(pathFinderAgent);

        if (pathFinderAgent.remainingDistance <= pathFinderAgent.stoppingDistance) { isMoving = false; }

    }

    // click particle system effect
    void ClickEffect()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            ClickEffectUsed = false;
        }

        if (!ClickEffectUsed)
        {
            Instantiate(DestinationEffect, rayCastInfo.point + Vector3.up * 0.5f, Quaternion.identity);
            ClickEffectUsed = true;
        }
        




        /*
        //click effects
        if (!ClickEffectUsed && DestinationEffect != null)
        {
            Instantiate(DestinationEffect, rayCastInfo.point + Vector3.up * 0.5f, Quaternion.identity);
            ClickEffectUsed = true;
        }


        if (Input.GetKeyUp(KeyCode.Mouse0) || Input.GetMouseButtonUp(0))
        {
            Debug.Log("working in here");
            if (MouseUpClickEffectTimer > 1)
            {
                Instantiate(DestinationEffect, rayCastInfo.point + Vector3.up * 0.5f, Quaternion.identity);
            }

            ClickEffectUsed = false;
            MouseUpClickEffectTimer = 0;
        }
        */
    }

    void DodgeRoll_Input()
    {
        //dodgeRoll
        if (dodgeRollSetting.canDodgeRoll)
        {
            //Dodge Forward
            if (Input.GetButtonDown("Vertical") && Input.GetAxis("Vertical") > 0 && Input.GetAxis("Horizontal") == 0)
            {
                if (Input.GetButtonDown("Vertical") && (Time.time - lastTime) < 0.4f && Input.GetButtonDown("Vertical") && (Time.time - lastTime) > 0.1f && Input.GetAxis("Vertical") > 0.03f)
                {
                    lastTime = Time.time;
                    dir = Vector3.forward;
                    StartCoroutine(DodgeRoll(dodgeRollSetting.dodgeForward));
                }
                else
                    lastTime = Time.time;
            }
            //Dodge Backward
            if (Input.GetButtonDown("Vertical") && Input.GetAxis("Vertical") < 0 && Input.GetAxis("Horizontal") == 0)
            {
                if (Input.GetButtonDown("Vertical") && (Time.time - lastTime) < 0.4f && Input.GetButtonDown("Vertical") && (Time.time - lastTime) > 0.1f && Input.GetAxis("Vertical") < -0.03f)
                {
                    lastTime = Time.time;
                    dir = Vector3.back;
                    StartCoroutine(DodgeRoll(dodgeRollSetting.dodgeBack));
                }
                else
                    lastTime = Time.time;
            }
            //Dodge Left
            if (Input.GetButtonDown("Horizontal") && Input.GetAxis("Horizontal") < 0 && !Input.GetButton("Vertical"))
            {
                if (Input.GetButtonDown("Horizontal") && (Time.time - lastTime) < 0.3f && Input.GetButtonDown("Horizontal") && (Time.time - lastTime) > 0.15f && Input.GetAxis("Horizontal") < -0.03f)
                {
                    lastTime = Time.time;
                    dir = Vector3.left;
                    StartCoroutine(DodgeRoll(dodgeRollSetting.dodgeLeft));
                }
                else
                    lastTime = Time.time;
            }
            //Dodge Right
            if (Input.GetButtonDown("Horizontal") && Input.GetAxis("Horizontal") > 0 && !Input.GetButton("Vertical"))
            {
                if (Input.GetButtonDown("Horizontal") && (Time.time - lastTime) < 0.3f && Input.GetButtonDown("Horizontal") && (Time.time - lastTime) > 0.15f && Input.GetAxis("Horizontal") > 0.03f)
                {
                    lastTime = Time.time;
                    dir = Vector3.right;
                    StartCoroutine(DodgeRoll(dodgeRollSetting.dodgeRight));
                }
                else
                    lastTime = Time.time;
            }
        }
    }

    void MoveToAttack()
    {
        if(target == null)
        {
            if (enemyTargeted)
                enemyTargeted = false;

            if (closeToEnemy)
                closeToEnemy = false;

            return;
        }

        pathFinderAgent.destination = target.transform.position;

        if (pathFinderAgent.remainingDistance > GetComponent<AttackTriggerC>().requiredDistanceForAttack && !closeToEnemy)
        {
            isMoving = true;

            if (!dontRepeat)
            {
                GetComponent<AttackTriggerC>().queueLightAttack = true;
                dontRepeat = true;
            }

            NavMeshExtensions.ResumeAgent(pathFinderAgent);
        }
        else if (pathFinderAgent.remainingDistance <= GetComponent<AttackTriggerC>().requiredDistanceForAttack && closeToEnemy)
        {
            isMoving = false;
            transform.LookAt(target.transform);
        }
    }

    void MoveToAttackHeavyAttack()
    {
        if (target == null)
        {
            if (enemyTargeted)
                enemyTargeted = false;

            if (closeToEnemy)
                closeToEnemy = false;

            return;
        }

        pathFinderAgent.destination = target.transform.position;

        if (pathFinderAgent.remainingDistance > GetComponent<AttackTriggerC>().requiredDistanceForAttack && !closeToEnemy)
        {
            if (!dontRepeat)
            {
                GetComponent<AttackTriggerC>().queueHeavyAttack = true;
                dontRepeat = true;
            }

            isMoving = true;
            NavMeshExtensions.ResumeAgent(pathFinderAgent);
        }
        else if (pathFinderAgent.remainingDistance <= GetComponent<AttackTriggerC>().requiredDistanceForAttack && closeToEnemy)
        {
           // if(GetComponent<AttackTriggerC>().queueHeavyAttack)
             //   GetComponent<AttackTriggerC>().queueHeavyAttack = false;

            isMoving = false;
            transform.LookAt(target.transform);
        }
    }

    void CloseToEnemyCheck()
    {
        if (enemyTargeted)
        {
            if (pathFinderAgent.remainingDistance > GetComponent<AttackTriggerC>().requiredDistanceForAttack)
            {
                closeToEnemy = false;
            }
            else
            {
                closeToEnemy = true;
            }
        }
    }

    void StaminaRecovery()
    {
        stamina += 1;
        if (stamina >= maxStamina)
        {
            stamina = maxStamina;
            recoverStamina = 0.0f;
            recover = false;
        }
        else
        {
            recoverStamina = staminaRecover - 0.02f;
        }
    }

    IEnumerator DodgeRoll(AnimationClip anim)
    {
        if (stamina >= 25 && !dodging && canControl)
        {
            //For Mecanim Animation
            if (GetComponent<PlayerMecanimAnimationC>())
            {
                GetComponent<PlayerMecanimAnimationC>().AttackAnimation(anim.name);
            }

            dodging = true;
            stamina -= dodgeRollSetting.staminaUse;
            GetComponent<StatusC>().dodge = true;
            canControl = false;
            yield return new WaitForSeconds(0.5f);
            GetComponent<StatusC>().dodge = false;
            recover = true;
            canControl = true;
            dodging = false;
            recoverStamina = 0.0f;
        }
    }

    void OnGUI()
    {

            GUI.Box(new Rect((Screen.width / 2) - 140, 5, 280, 25), "Mouse Position = " + Input.mousePosition);
            GUI.Box(new Rect((Screen.width / 2) - 70, Screen.height - 30, 140, 25), "Mouse X = " + Input.mousePosition.x);
            GUI.Box(new Rect(5, (Screen.height / 2) - 12, 140, 25), "Mouse Y = " + Input.mousePosition.y);
        

        if (sprint || recover || dodging)
        {
            float staminaPercent = stamina * 100 / maxStamina * 3;
            //GUI.DrawTexture ( new Rect((Screen.width /2) -150,Screen.height - 120,stamina *3,10), staminaGauge);
            GUI.DrawTexture(new Rect((Screen.width / 2) - 150, Screen.height - 120, staminaPercent, 10), staminaGauge);
            GUI.DrawTexture(new Rect((Screen.width / 2) - 153, Screen.height - 123, 306, 16), staminaBorder);
        }

    }//stamina GUI

    void Old_Input()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0) || Input.GetMouseButton(0))
        {
            //Debug.Log("you clicked");

            theRay = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(theRay, out rayCastInfo))
            {
                //Debug.Log("ray was casted");

                pathFinderAgent.SetDestination(rayCastInfo.point);

                isMoving = true;
                //Debug.Log(pathFinderAgent.steeringTarget);
            }

        }
        if (pathFinderAgent.remainingDistance <= pathFinderAgent.stoppingDistance) { isMoving = false; }
    }
}

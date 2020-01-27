using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClickToMoveNavmesh : MonoBehaviour
{
    [System.Serializable]
    public class TheCursor
    {
        public string tag;
        public Texture2D cursorTexture;
    }

    [Header("Stats")]
    public float aimUpward = 0.8f;
    public float attackDistance;
    public float attackRate;
    private float nextAttack;
    private bool navmeshisDown = false;

    [Header("Cursor")]
    public Vector2 cursorPosition;
    public List <TheCursor> cursorList = new List<TheCursor>();
    public RaycastHit cursorHit;
    [HideInInspector]
    public Vector3 lookAtTargetPosition;
    [HideInInspector]
    Vector2 smoothDeltaPosition = Vector2.zero;
    [HideInInspector]
    Vector2 velocity = Vector2.zero;
    public bool enemyClicked = false;
    public bool hitEnemy = false;
    public bool shouldAttack = false;
    public Transform targetedEnemy;

    [Header("Navigation")]
        public float speed;
        private bool walking;
        public Vector3 position;
        private UnityEngine.AI.NavMeshAgent navMeshAgent;
        CharacterMotorC charactermotorc;
        public CharacterController controller;
        public Camera cam;

    [Header("Animation")]
    private Animator animator;
    public GameObject mainModel;
    
    //Animations
    public AnimationClip idle;
    public AnimationClip run;
    public AnimationClip attack;
    public AnimationClip hurt;
    public AnimationClip die;

    void Awake()
    {
        animator = GetComponent<Animator>();

        navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
    }

    void Start()
    {
        SetCursorTexture(cursorList[0].cursorTexture);

        attackDistance = attackDistance * navMeshAgent.stoppingDistance;

        enemyClicked = false;
        position = transform.position;
    }

    private float q = 0.0f;
    void Update()
    {

       // cursorPosition = 

        if(!cam)
        {
            cam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if(Physics.Raycast(ray, out cursorHit, 1000))
        {
         //   Color color = new Color(q, q, 1.0f);
        //Debug.DrawLine(Camera.main.transform.position, cursorHit.point, color);
        //q = q + 0.01f;

            if(cursorHit.transform.tag == "Terrain")
            {
                hitEnemy = false;
                targetedEnemy = null;

                if(Input.GetKey(KeyCode.Mouse0) && cursorHit.transform.tag != "Enemy")
                {
                    navMeshAgent.destination = cursorHit.point;
                    animator.SetBool("isWalking", true);
                    if(targetedEnemy != null && Input.GetKeyDown(KeyCode.Mouse0) && cursorHit.transform.tag == "Terrain")
                    {
                        hitEnemy = false;
                        targetedEnemy = null;
                    }
                }
            }

            if (cursorHit.transform.tag == "Enemy")
            {
                hitEnemy = true;
            }
            else
            {
               hitEnemy = false;
            }
            if (hitEnemy == true && Input.GetKeyDown(KeyCode.Mouse0))
            {
                enemyClicked = true;
            }
            else
            {
                enemyClicked = false;
            }
            if (hitEnemy == true && enemyClicked == true && cursorHit.transform.tag != "Terrain")
            {
                targetedEnemy = cursorHit.transform;
                //display selected enemies health canvas
                GetComponent<ShowEnemyHealthC>().GetHP(targetedEnemy.GetComponent<StatusC>().health, targetedEnemy.gameObject, targetedEnemy.name);
                MoveAndAttack();
            }


            //cursor icons
            if (Physics.Raycast(ray, out cursorHit, 1000))
            {
                for (int i = 0; i < cursorList.Count; i++)
                {
                    if (cursorHit.collider.tag == cursorList[i].tag)
                    {
                        SetCursorTexture(cursorList[i].cursorTexture);
                        return;
                    }
                }
                
            }
            SetCursorTexture(cursorList[0].cursorTexture);
        }





    }



    void SetCursorTexture(Texture2D tex)
    {
        Cursor.SetCursor(tex, Vector2.zero, CursorMode.Auto);
    }



    void MoveAndAttack()//navmesh based movement/engagement.
    {


        Vector3 futDir = targetedEnemy.transform.position; // WHAT IS ENEMY POSITION
        futDir = Vector3.RotateTowards(futDir, futDir, 6.28f * Time.deltaTime, float.PositiveInfinity); // ROTATE TOWARDS ENEMY POSITION


        if (Time.deltaTime > 1e-5f)
            velocity = smoothDeltaPosition / Time.deltaTime;
        bool shouldMove = velocity.magnitude > 0.5f && navMeshAgent.remainingDistance > navMeshAgent.radius;



        // Update animation parameters
        animator.SetBool("isAttacking", false);
       animator.SetBool("isWalking", true);


        navMeshAgent.destination = targetedEnemy.transform.position; // MOVE TOWARDS ENEMY POSITION

        if(navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            shouldMove = false;
            animator.SetBool("isWalking", false);
            shouldAttack = true;
        }
        else
        {
            shouldAttack = false;
            animator.SetBool("isWalking", true);
        }

    }

}
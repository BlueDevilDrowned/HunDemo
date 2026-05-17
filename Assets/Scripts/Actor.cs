using Animancer;
using Unity.Cinemachine;
using UnityEngine;

public class Actor : MonoBehaviour
{
    //组件信息
    public CinemachineCamera cinemachineCamera;
    public Movement movement;
    public Animator animator;
    public AnimancerComponent animancer;
    public LogicInput logicInput;
    public CharacterController characterController;
    public ActionSystem actionSystem;
    
    public AttackManager attackManager;
    //mapping
    public ActionTypeMap actionTypeMap;
    public ActionConfig actionConfig;
    public DefaultActionType defaultActionType;
    public DefenseState defenseState;
    public HitResolutionSystem hitResolutionSystem;
    public StanceSystem stanceSystem;
    void Start()
    {
        movement=GetComponentInChildren<Movement>();
        if(movement==null)Debug.LogError("Movement Cant Find In Children!");
        
        animator=GetComponentInChildren<Animator>();
        if(animator==null)Debug.LogError("Animator Cant Find In Children!");

        animancer=GetComponentInChildren<AnimancerComponent>();
        animancer.Animator=animator;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

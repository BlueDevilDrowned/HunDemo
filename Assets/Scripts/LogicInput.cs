using System;
using UnityEngine;

public class LogicInput : MonoBehaviour
{
    //组件信息
    public Actor actor;
    //按键逻辑属性
    public bool ShiftPressed;

    //游戏逻辑属性
    public Vector2 RawMove
    {
        get
        {
            if(LockRawMove)return Vector2.zero;
            return _RawMove*RawMoveChange;
        }
    }
    public Vector2 _RawMove;
    public float RawMoveChange=>ShiftPressed?1f:0.5f;
    public float Distance=>RawMove.magnitude;
    public bool LockRawMove=false;
    public Vector2 RawLook;
    public bool Jump;
    public bool attack;
    public bool Block;
    public bool TryMove=>Distance>0.001;
    public bool TryRun=>Distance>0.51;
    //功能性数值
    
    void Start()
    {
        if(actor==null)
        {
            actor=GetComponent<Actor>();
        }
    }
    void Update()
    {
        if(Jump)actor.actionSystem.AddAction(ActionType.jump);
        if(attack)actor.actionSystem.AddAction(ActionType.attack);
        if(Block)actor.actionSystem.AddAction(ActionType.Block);
        if(TryRun)actor.actionSystem.AddAction(ActionType.Run);
        else if(TryMove)actor.actionSystem.AddAction(ActionType.Walk);
        if(!actor.movement.isGrounded)actor.actionSystem.AddAction(ActionType.Air);
    }
    
    


}

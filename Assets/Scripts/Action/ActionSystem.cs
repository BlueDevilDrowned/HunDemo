using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
public class ActionSystem : MonoBehaviour
{
    public Actor actor;
    public List<ActionType>ThisFrameActionTypes=new();
    public ActionType BestActionType=ActionType.None;
    public string CurrentActionId="Idle";
    public ActionDefinition currentAction;
    public ActionDefinition targetAction;
    public float animatorTime=0;
    private List<RunTimeActionMoudle>Modules=new();
    public ActionDefinition None=>actor.actionConfig.TryGetAction("Idle");
    #region api
    public bool  CanInterrupt(int interruptLevel)
    {
        //print(interruptLevel+"+"+currentAction.interruptResistance);
        return interruptLevel>=currentAction.interruptResistance;
    }
    #endregion
    void Start()
    {
        currentAction=None;
        targetAction=None;
    }
    void Update()
    {
       ActionUpdate(Time.deltaTime);
    }
    void ActionUpdate(float time)
    {
        animatorTime+=time;
        UpdateAction();
    }
    public void AddAction(ActionType actionType)
    {
        ThisFrameActionTypes.Add(actionType);
    }
    #region action函数处理
    void UpdateAction()
    {
        //时间处理
        if(animatorTime>=currentAction.animationInfo.clip.length)
        {
            if(currentAction.Loop)
            {
                LoopExit();
            }
            else
            {
                ExitCurrentToNone();
            }
        }
        if(Modules==null)return;
        foreach(var module in Modules)
        {
            module.ActionUpdate(actor,currentAction,animatorTime);
        }
    }
    void EnterAction()
    {
        animatorTime=0f;
        BuildModules();
    }
    void BuildModules()
    {
        Modules.Clear();
        if(currentAction.modules==null)return;
        foreach(var module in currentAction.modules)
        {
            Modules.Add(new RunTimeActionMoudle(module));
        }
    }
    void ExitAction()
    {
        foreach(var module in Modules)
        {
            if(module.Started)
            {
                module.Exited=true;
                module.actionModule.Exit(actor,currentAction,animatorTime);
            }
        }
    }
    #endregion
    void LateUpdate()
    {
        RegisterUpdate();
    }
    void RegisterUpdate()
    {
        SelectBestActionType();
        if(targetAction!=None&&targetAction!=null)
        {
            
            //动画切换
            ChangeAnimation();
        }
    }
    void ChangeAnimation()
    {
        //提前exit
        ExitAction();
        //print("change"+targetAction.actionId);//
        CurrentActionId=targetAction.name;
        currentAction=targetAction;
        targetAction=None;
        actor.animancer.Play(currentAction.animationInfo.clip,0.1f);
        EnterAction();
    }
    void LoopExit()
    {
        //提前exit
        ExitAction();
        targetAction=currentAction;
        //print("change"+targetAction.actionId);//
        CurrentActionId=currentAction.name;
        actor.animancer.Play(currentAction.animationInfo.clip);
        EnterAction();
    }
    public void ExitCurrentToNone()
    {
        //提前exit
        ExitAction();
        targetAction=None;
        //print("change"+targetAction.actionId);//
        CurrentActionId=targetAction.name;
        currentAction=targetAction;
        targetAction=None;
        actor.animancer.Play(actor.actionConfig.DefaultAction.clip,0.1f);
        EnterAction();
    }
    void SelectBestActionType()
    {
        if(ThisFrameActionTypes!=null)
        {
            ActionType excActionType=ActionType.None;
            float priority=0;
            ActionDefinition bestAction=null;
            
           foreach(var actionType in ThisFrameActionTypes)
            {
                if(IfCandidate(actionType,out var action))
                {
                    if(actor.actionTypeMap.GetPriority(actionType)>=priority)
                    {
                        excActionType=actionType;
                        priority=actor.actionTypeMap.GetPriority(actionType);
                        bestAction=action;
                    }
                    
                }
            }
            BestActionType=excActionType;
            targetAction=bestAction;
        }
        ThisFrameActionTypes.Clear();
        if(currentAction.Loop&&currentAction.exitWhenHoldLost)
        {
            if(BestActionType==ActionType.None)
            {
                //无法维持loop状态，退出。
                ExitCurrentToNone();
            }
            else if(BestActionType==currentAction.HoldActionType)
            {
                BestActionType=ActionType.None;
                targetAction=None;
            }
        }
    }
    bool IfCandidate(ActionType actionType,out ActionDefinition targetAction)
    {
        //默认动作
        if(actor.defaultActionType.CheckAction(actionType,out var typeAndId)&&CanInterrupt(typeAndId.interruptLevel))//处理默认动作
        {
            print("DefaultAttack");
            targetAction=actor.actionConfig.TryGetAction(typeAndId.actionId);
            return true;
        }
        var definition=actor.actionConfig.TryGetAction(CurrentActionId);
        if(definition==null||definition.transitions==null)
        {
            targetAction=None;
            return false;
        }
        //loop是否需要持续的条件
        if(currentAction.Loop&&currentAction.exitWhenHoldLost)
        {
            if(actionType==currentAction.HoldActionType)
            {
                targetAction=currentAction;
                return true;
            }
        }
        //切换轨道
        foreach(var transition in definition.transitions)
        {
            if(transition.IsMatch(actionType,animatorTime))
            {
                targetAction=actor.actionConfig.TryGetAction(transition.actionId);
                if(targetAction==null)return false;
                return true;
            }
        }
        targetAction=null;
        return false;
    }
    
    

    //对于权能的判断
}

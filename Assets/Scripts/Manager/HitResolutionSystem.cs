using System;
using UnityEngine;

public class HitResolutionSystem : MonoBehaviour
{
    public Actor actor;
    public Action<AttackEvent>action;
    void Start()
    {
       action += (attackEvent) =>
        {
            if(actor!=attackEvent.hitBox.actor&&attackEvent.defender==actor)
            {
                print(actor.name+"受到了"+"来自"+attackEvent.hitBox.actor.name+attackEvent.hitBox.attackInfo.Damage.ToString());
            }
        };
        //增加逻辑
        action+=FindDefense;
        //
        EventManager.Subscribe<AttackEvent>(action);
    }
    public void FindDefense(AttackEvent attackEvent)
    {
        if(attackEvent.defender!=actor)return;
        if(attackEvent.hitBox.attackInfo.TryGetDefense(actor.defenseState.GetDefenseType(),out var defenseResponseRule))
        {
            //先判断角度
            if(actor.actionSystem.CanInterrupt(attackEvent.hitBox.attackInfo.interruptLevel)&&actor.defenseState.ifWithinDefenseAngle(attackEvent.hitBox.actor))
            {
                actor.actionSystem.AddAction(defenseResponseRule.defenserActionType);
                attackEvent.hitBox.actor.actionSystem.AddAction(defenseResponseRule.attackerActionType);
                //发布反制成功事件
                #region 反制成功事件
                DefenseSuccessEvent defense=new();
                defense.Defender=actor;
                defense.Attacker=attackEvent.hitBox.actor;
                defense.DefenseRule=defenseResponseRule;
                EventManager.Publish<DefenseSuccessEvent>(defense);
                #endregion

                
            }
            else
            {
                print("无法打断");
            }
            
        }
        else
        {
            //默认路线
            actor.actionSystem.AddAction(attackEvent.hitBox.attackInfo.DefaultResult.defenserActionType);
            attackEvent.hitBox.actor.actionSystem.AddAction(attackEvent.hitBox.attackInfo.DefaultResult.attackerActionType);

        }
    }
}
public struct DefenseSuccessEvent:GameEvent
{
    public Actor Defender;
    public Actor Attacker;
    public DefenseResponseRule DefenseRule;

}
using System;
using Unity.VisualScripting;
using UnityEngine;
[Serializable]
public abstract class ActionModule
{
    public bool DontHaveTime=>start==0&&end==0;
    public float start=0;
    public float end=0;


    
    public virtual void Enter(Actor actor,ActionDefinition action,float time)
    {
        
    }
    public virtual void Update(Actor actor,ActionDefinition action,float animatorTime)
    {
        
    }
    public virtual void Exit(Actor actor,ActionDefinition action,float time)
    {
        
    }

}
public class RunTimeActionMoudle
{
    public ActionModule actionModule;
    public bool Started=false;
    public bool Exited=false;
    public RunTimeActionMoudle(ActionModule actionModule)
    {
        this.actionModule=actionModule;
    }
    public void ActionUpdate(Actor actor,ActionDefinition action,float time)
    {
        //全轨道持续
        if(actionModule.DontHaveTime)
        {
            if(!Started)
            {
                Started=true;
                actionModule.Enter(actor,action,time);
            }
            return;
        }
        //正常更新
        if(!Started&&time>=actionModule.start)
        {
            Started=true;
            actionModule.Enter(actor,action,time);
            //Debug.Log("Enter"+actionModule.ToString());
        }
        if(Started&&!Exited)actionModule.Update(actor,action,time);
        if(Started&&!Exited&&time>=actionModule.end)
        {
            Exited=true;
            actionModule.Exit(actor,action,time);
        }
    }
}

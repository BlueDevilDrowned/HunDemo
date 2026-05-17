using System;
using System.Collections.Generic;
using UnityEngine;
public enum AttackType
{
    Normal,
    Grab,
    Kill,

}
[Serializable]
public class AttackCollisionModule : ActionModule
{
    
    public AttackInfo attackInfo;
    public List<Collision> collisions=new();
    public override void Enter(Actor actor, ActionDefinition action, float time)
    {
        string name =action.animationInfo.name+"/"+this.GetHashCode().ToString()+"/";

        actor.attackManager.EnableCollider(name,collisions,attackInfo);
    }
    public override void Exit(Actor actor, ActionDefinition action, float time)
    {
            string name =action.animationInfo.name+"/"+this.GetHashCode().ToString()+"/";
            actor.attackManager.DisableCollider(name,collisions);
    }
   
}
[Serializable]
public class Collision
{
    public string path;
    public Vector3 position;
    public Vector3 rotation;
    public float Radius;
    public float Height;
}
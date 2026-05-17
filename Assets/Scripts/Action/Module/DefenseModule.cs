using System;
using UnityEngine;
public enum DefenseType
{
    None,
    Block,
    Dodge,
}
[Serializable]
public class DefenseModule : ActionModule
{
    public DefenseInfo defenseInfo=new();
    public override void Enter(Actor actor, ActionDefinition action, float time)
    {
        actor.defenseState.AddDefenseInfo(defenseInfo);
    }
    public override void Exit(Actor actor, ActionDefinition action, float time)
    {
        actor.defenseState.RemoveDefenseInfo(defenseInfo);
    }
}
[Serializable]
public class DefenseInfo
{
    public float Angle=360;
    public DefenseType defenseType=DefenseType.Block;
}

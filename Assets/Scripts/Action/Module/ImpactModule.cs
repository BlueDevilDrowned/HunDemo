using System;
using UnityEngine;
[Serializable]
public class ImpactModule : ActionModule
{
    public Vector3 impact=Vector3.zero;
    public override void Enter(Actor actor, ActionDefinition action, float time)
    {
        actor.movement.AddImpact(impact);
    }
}

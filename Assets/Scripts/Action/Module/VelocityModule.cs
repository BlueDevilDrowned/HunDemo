using UnityEngine;
using System;
[Serializable]
public class VelocityModule : ActionModule
{
    public Vector3 velocity=Vector3.zero;
    public override void Enter(Actor actor, ActionDefinition action, float time)
    {
        actor.movement.AddVelocity(velocity);
    }

}

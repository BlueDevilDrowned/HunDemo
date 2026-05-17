using System;
using UnityEngine;
[Serializable]
public class LockRawMoveModule : ActionModule
{
    public override void Enter(Actor actor, ActionDefinition action, float time)
    {
        actor.logicInput.LockRawMove=true;
    }
    public override void Exit(Actor actor, ActionDefinition action, float time)
    {
        actor.logicInput.LockRawMove=false;
    }
}

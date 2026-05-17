using System.Collections.Generic;
using UnityEngine;

public class DefenseState : MonoBehaviour
{
    public Actor actor;
    public List<DefenseInfo> defenseInfos=new();
    public void AddDefenseInfo(DefenseInfo defenseInfo)
    {
        this.defenseInfos.Add(defenseInfo);
    }
    public void RemoveDefenseInfo(DefenseInfo defenseInfo)
    {
        this.defenseInfos.Remove(defenseInfo);
    }
    public bool ifWithinDefenseAngle(Actor attackActor)
    {
        if(defenseInfos.Count==0)return false;
        Vector3 dir=attackActor.transform.position-actor.transform.position;
        dir.y=0;
        float Angle=Vector3.Angle(actor.transform.forward,dir);
        return Angle<=defenseInfos[0].Angle/2;
    }
    public DefenseType GetDefenseType()
    {
        return defenseInfos.Count==0?DefenseType.None:defenseInfos[0].defenseType;
    }
}

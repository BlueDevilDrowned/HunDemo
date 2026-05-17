using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AttackInfo", menuName = "Scriptable Objects/AttackInfo")]
[Serializable]
public class AttackInfo : ScriptableObject
{
    public AttackType attackType;
    public int interruptLevel=0;
    public float Damage;
    public DefenseResponseRule DefaultResult=new();
    public List<DefenseResponseRule>DefenseResponseRules=new();
    public bool TryGetDefense(DefenseType defenseType,out DefenseResponseRule defenseResponseRule)
    {
        if(defenseType==DefenseType.None)
        {
            defenseResponseRule=null;
            return false;
        }
        foreach(var defense in DefenseResponseRules)
        {
            if(defenseType==defense.defenseType)
            {
                defenseResponseRule=defense;
                return true;
            }
        }
        defenseResponseRule=null;
        return false;
    }
}
[Serializable]
public class DefenseResponseRule
{
    public DefenseType defenseType;
    public bool ignoreHit=false;
    public bool enterInteraction=false;
    public float StanceValue=0;
    public ActionType attackerActionType;
    public ActionType defenserActionType;
}
using System;
using UnityEngine;
[Serializable]
public class Transition
{
    public string actionId;
    public ActionType type;
    public bool DontHaveTime=>duration==0;
    public float start=0;
    public float end=0;
    public float duration
    {
        get
        {
            return end-start;
        }
        set
        {
            end=start+value;
        }
    }

    
    public bool IsMatch(ActionType actionType,float Time)
    {
        if(actionType==type&&DontHaveTime)return true;
        return actionType==type&&Time>=start&&Time<=end;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DefaultActionType", menuName = "Scriptable Objects/DefaultActionType")]
public class DefaultActionType : ScriptableObject
{
    public ActionId actionId;
    public List<TypeAndId>DefaultActions=new();

    public bool CheckAction(ActionType actionType,out TypeAndId typeAndId)
    {
        foreach(var action in DefaultActions)
        {
            if(actionType==action.actionType)
            {
                typeAndId=action;
                
                return true;
            }
        }
        typeAndId=null;
        return false;
    }
}
[Serializable]
public class TypeAndId
{
    public ActionType actionType;
    public string actionId;
    public int interruptLevel;
}
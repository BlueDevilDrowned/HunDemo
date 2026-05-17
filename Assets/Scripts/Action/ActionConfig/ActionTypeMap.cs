using System;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "ActionType", menuName = "Scriptable Objects/ActionTypeMap")]
public class ActionTypeMap : ScriptableObject,ISerializationCallbackReceiver
{
    public List<ActionTypeEnter>ActionTypesList=new();
    private List<ActionTypeEnter>ActionTypesList2=new();
    private Dictionary<ActionType,ActionTypeEnter>ActionTypes=new();
    public int GetPriority(ActionType actionType)
    {
        return ActionTypes.TryGetValue(actionType,out ActionTypeEnter actionTypeEnter)?actionTypeEnter.priority:0;
    }
    #region inspector相关
    public void OnValidate()
    {
        //按priotity排序，如果改的是p，重排，如果改的是顺序，修改pri
        //个数一样
        if(ActionTypesList.Count==ActionTypesList2.Count)
        { 
            if(!CheckListByPriority())
            {
                SortByPriority(ActionTypesList);
            }
        }
        SmoothListPriority(ActionTypesList);
        ActionTypesList2=CopyList(ActionTypesList);
    }
    void CheckListCount(List<ActionTypeEnter>list)
    {
        int Count=(int)ActionType.Last;
        if(list.Count<Count)
        {
            for(int i=0;i<Count;i++)
            {
                if(!ActionTypes.TryGetValue((ActionType)i,out ActionTypeEnter value))
                {
                    ActionTypesList.Add(new ActionTypeEnter((ActionType)i,i));                 
                }
            }
        }
    }
    void SortByPriority(List<ActionTypeEnter>list)
    {
        list.Sort((a,b)=>
        {
            return a.priority.CompareTo(b.priority);
        });
    }
    List<ActionTypeEnter> CopyList(List<ActionTypeEnter>targetList)
    {
        List<ActionTypeEnter>list=new();
        foreach(var action in targetList)
        {
            list.Add(new ActionTypeEnter(action.actionType,action.priority));
        }
        return list;
    }
    void SmoothListPriority(List<ActionTypeEnter>list)
    {
        for(int i=0;i<list.Count;i++)
        {
            list[i].priority=i;
        }
    }
    bool CheckListByPriority()
    {
        for(int i=0;i<ActionTypesList.Count;i++)
        {
            if(ActionTypesList[i].priority!=ActionTypesList2[i].priority)
            {
                return false;
            }
        }
        return true;
    }
    public void OnBeforeSerialize()
    {
        CheckListCount(ActionTypesList);
    }

    public void OnAfterDeserialize()
    {
        ActionTypes.Clear();
        foreach(var action in ActionTypesList)
        {
            ActionTypes[action.actionType]=action;
        }
    }
#endregion
    //动作权职
}
[Serializable]
public class ActionTypeEnter
{
    public ActionType actionType;
    public int priority;
    public ActionTypeEnter(ActionType actionType,int priority)
    {
        this.actionType=actionType;
        this.priority=priority;
    }
}

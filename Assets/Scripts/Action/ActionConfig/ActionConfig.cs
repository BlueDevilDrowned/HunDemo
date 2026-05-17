using System;
using System.Collections.Generic;
using System.Linq;
using Animancer;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "ActionConfig", menuName = "Scriptable Objects/ActionConfig")]
public class ActionConfig : ScriptableObject,ISerializationCallbackReceiver
{
    [SerializeField]
    public RuntimeAnimatorController animatorController;
    public ActionId actionId;
    public AnimationInfo DefaultAction;
    public List<ActionDefinition>actions=new();
    Dictionary<ActionType,ActionDefinition>TypeToActions=new();
    Dictionary<string,ActionDefinition>IdToActions=new();
    HashSet<String> IdNames=new();
   
    public void RefreshActions()
    {
        //删掉没有的id
        for(int i=actions.Count()-1;i>0;i--)
        {
            if(!actionId.searchName(actions[i].animationInfo.name))
            {
                actions.RemoveAt(i);
            }
        }
        //补全缺失
        for(int i=actionId.ActionIds.Count()-1;i>0;i--)
        {
            AnimationInfo id=actionId.ActionIds[i];
            if(IdToActions.TryGetValue(id.name,out var action))
            {
                action.animationInfo=id;
            }
            else
            {
               actions.Add(new ActionDefinition(id));
            }
            
        }
        actions.Sort((a,b)=>
        {
            int an=actionId.GetPriority(a.animationInfo.name);
            int bn=actionId.GetPriority(b.animationInfo.name);
            if(an>bn)return 1;
            else if(an<bn)return -1;
            return 0;
        });
        
    }
    public  void OnAfterDeserialize()
    {
        BuildMap();
    }
    public void BuildMap()
    {
        TypeToActions.Clear();
        IdToActions.Clear();
        foreach(var action in actions)
        {
            if(action==null||string.IsNullOrEmpty(action.name))
            {
                continue;
            }
            IdToActions[action.animationInfo.name]=action;
        }
    }

    public void OnBeforeSerialize()
    {

    }
    public ActionDefinition TryGetAction(string id)
    {
        if(string.IsNullOrEmpty(id))
        {
            return null;
        }
        if(IdToActions.Count==0)BuildMap();
        return IdToActions.TryGetValue(id,out ActionDefinition actionDefinition)?actionDefinition:null;
    }

}
[Serializable]
public class ActionDefinition
{
    public AnimationInfo animationInfo;
    public string name=>animationInfo.name;
    public int interruptResistance=0;
    public bool Loop=false;
    public bool exitWhenHoldLost=false;
    public ActionType HoldActionType=ActionType.None;
    public List<Transition>transitions;
    [SerializeReference]
    public List<ActionModule>modules;
    public bool CanBlend;
    public List<ActionModule>BlendModules;
    public ActionDefinition(AnimationInfo id)
    {
        animationInfo=id;
    }
}

using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Timeline;

[CreateAssetMenu(fileName = "ActionId", menuName = "Scriptable Objects/ActionId")]
public class ActionId : ScriptableObject
{
   public List<AnimationInfo>ActionIds=new(); 
   public bool searchName(string name)
   {
      if(ActionIds.Count==0)return false;
      foreach(var action in ActionIds)
      {
         if(name==action.name)return true;
      }   
      return false;
   }
   public int GetPriority(string name)
   {
      for(int i=0;i<ActionIds.Count;i++)
      {
         if(name==ActionIds[i].name)
         return i;
      }
      return -1;
   }
}
[Serializable]
public class AnimationInfo
{
   public string name;
   public AnimationClip clip;
}
using System;
using System.Collections.Generic;
using UnityEngine;

public static class EventManager
{
   public static readonly Dictionary<Type,List<Delegate>>_handlers=new();
   public static void Subscribe<T>(Action<T> handler)where T:GameEvent
    {
        var type =typeof(T);
        if(!_handlers.TryGetValue(type,out var list))
        {
            list=new List<Delegate>();
            _handlers[type]=list;
        }
        list.Add(handler);
    }
    public static void Unsubscribr<T>(Action<T> hander)where T:GameEvent
    {
        var type=typeof(T);
        if(_handlers.TryGetValue(type,out var list))
        {
            list.Remove(hander);
        }
    }
    public static void Publish<T>(T eventArgs)where T:GameEvent
    {
        var type=typeof(T);
        if(_handlers.TryGetValue(type,out var list))
        {
            foreach(var handler in list)
            {
                ((Action<T>)handler)?.Invoke(eventArgs);
            }
        }
    }
}
public interface GameEvent
{
    
}
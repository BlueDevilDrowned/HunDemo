using System;
using UnityEngine;

public class StanceSystem : MonoBehaviour
{
    public Actor actor;
    public float StanceValue
    {
        get
        {
            return _StanceValue;
        }
        set
        {
            if(value<0)
            {
                _StanceValue=0;
            }
            else if(value>MaxStanceValue)
            {
                value=MaxStanceValue;
            }
            else
            {
                _StanceValue=value;
            }
        }
    }
    private float _StanceValue=0;
    public float MaxStanceValue;
    private Action<DefenseSuccessEvent> SuccessEvent;
    public void Start()
    {
        
        SuccessEvent+=CheckStacne;
        SuccessEvent+=(successEvent)=>
        {
            print(actor.name+"had get from"+successEvent.Attacker.name+"Now Value:"+StanceValue);
        };
        EventManager.Subscribe<DefenseSuccessEvent>(SuccessEvent);
    }
    private void CheckStacne(DefenseSuccessEvent successEvent)
    {
        if(successEvent.Defender!=actor)return;
        //
        StanceValue+=successEvent.DefenseRule.StanceValue;
    }
}

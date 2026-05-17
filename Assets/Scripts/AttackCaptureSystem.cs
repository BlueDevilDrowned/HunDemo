using System.Collections.Generic;
using UnityEngine;

public class AttackCaptureSystem : MonoBehaviour
{
    public Actor actor;
    void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent<AttackHitBox>(out AttackHitBox box))
        {
            //判断攻击是否已经触发
            if(box.actor.attackManager.defenders[box.attackName].Add(actor))
            {
                if(box.actor!=actor&&other.gameObject.layer==LayerMask.NameToLayer("Attack"))
                {
                    //Debug.Log("Publish");
                    AttackEvent attackEvent = new()
                    {
                        hitBox = box,
                        defender=actor
                    };
                    EventManager.Publish<AttackEvent>(attackEvent);
                }
            }
            
        }
    }
}
public struct AttackEvent:GameEvent
{
    public AttackHitBox hitBox;
    public Actor defender;
    
}

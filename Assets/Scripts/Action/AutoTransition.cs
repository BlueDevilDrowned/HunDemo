using Unity.VisualScripting;
using UnityEngine;

public class AutoTransition : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var actor=animator.GetComponentInParent<Actor>();
        animator.SetBool("Exit",true);
        if(actor.actionSystem.CurrentActionId!="None")
        {
            
            actor.actionSystem.ExitCurrentToNone();
        }   
    }
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool("Exit",false);
    }

}

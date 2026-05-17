using System;
using Unity.VisualScripting;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public Actor actor;
    //功能性数值
    public float TurnSpeed=360;
    public float MoveSpeed=>MoveMaxSpeed*actor.logicInput.Distance;
    public float MoveMaxSpeed=10;
    public float gravity=10;
    public Vector3 Velocity=Vector3.zero;
    public Vector3 ImplusVelocity=Vector3.zero;
    public bool isGrounded;
    //碰撞法线+阻尼系数
    private Vector3 lastWallNormal;
    private bool hitWall;
    public float ImplusFloat=0.1f;
    void Update()
    {
        //
        isGrounded=actor.characterController.isGrounded;
        UpdateUnLockedTurn();
        UpdateUnLockedMove();
        UpdateMove();

    }
    void UpdateUnLockedTurn()
    {
        if(actor.logicInput.TryMove)
        {
            Vector3 dir=new Vector3(actor.logicInput.RawMove.x,0,actor.logicInput.RawMove.y);
            dir=actor.cinemachineCamera.transform.TransformDirection(dir);
            dir.y=0;
            Quaternion q=Quaternion.LookRotation(dir,Vector3.up);
            
            actor.transform.rotation=Quaternion.RotateTowards(actor.transform.rotation,q,TurnSpeed*Time.deltaTime);
        }
    }
    void UpdateUnLockedMove()
    {
        UpdateVelocity();
    }
    public void UpdateVelocity()
    {
        Velocity=Vector3.up*Velocity.y+actor.transform.forward*MoveSpeed;
        UpdateGravitySpeed();
    }
    void UpdateGravitySpeed()
    {
        if(actor.characterController.isGrounded||(ImplusVelocity.y>0))
        {
            Velocity.y=-1;
        }
        else
        {
            Velocity-=gravity*Vector3.up*Time.deltaTime;
        }
    }
    #region move相关，阻尼速度处理
    void UpdateMove()
    {

        //先把三个速度叠加起来
        Vector3 SumVelocity=Velocity+ImplusVelocity;
        CollisionFlags flags=actor.characterController.Move(SumVelocity*Time.deltaTime);
        ImplusVelocityUpdate(flags);
    }
    void ImplusVelocityUpdate(CollisionFlags flags)
    {
        ImplusVelocityOnHit(flags);
        //阻尼
        ImplusVelocity=Vector3.MoveTowards(ImplusVelocity,Vector3.zero,ImplusFloat*Time.deltaTime);
        if(ImplusVelocity.magnitude<0.1)ImplusVelocity=Vector3.zero;

    }
    void ImplusVelocityOnHit(CollisionFlags flags)
    {
        if(hitWall)
        {
            if((flags&CollisionFlags.None)==0)
            {
                ImplusVelocity=Vector3.ProjectOnPlane(ImplusVelocity,lastWallNormal);
            }
        }
        
    }
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if(hit.normal.y<0.5)
        {
            hitWall=true;
            lastWallNormal=hit.normal;
        }
    }
    #endregion
    //Api提供
    public void AddVelocity(Vector3 velocity)
    {
        Velocity+=velocity;
    }
    public void AddImpact(Vector3 velocity)
    {
        ImplusVelocity+=velocity;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AttackManager : MonoBehaviour
{
    public Actor actor;
    public Dictionary<string, GameObject> Enable = new();
    public Dictionary<string, GameObject> Disable = new();
    public Dictionary<string,HashSet<Actor>>defenders=new();

    void Start()
    {
        Enable.Clear();
        Disable.Clear();
    }
    public void EnableCollider(string name, List<Collision> collisions,AttackInfo attackInfo)
    {
        //装进表
        defenders[name]=new();
        for(int i=0;i<collisions.Count;i++)
        {
            EnableOneCollider(name,name+i,collisions[i],attackInfo);
        }
    }
    public void EnableOneCollider(string attackName,string name,Collision collision,AttackInfo attackInfo)
    {
        Transform colliders = GetCollisions(collision);

            if (colliders == null)
                return;
        GameObject collisionObject;
        if (Disable.TryGetValue(name, out collisionObject))
        {     
        }
        else
        {
            collisionObject=new(name);
        }
        collisionObject.SetActive(true);
        collisionObject.transform.SetParent(colliders, false);
        CopyCollider(collisionObject, collision);
        Enable[name] = collisionObject;
        Disable.Remove(name);
        CreateHitBox(attackName,attackInfo,collisionObject);
        collisionObject.layer=LayerMask.NameToLayer("Attack");
    }
    void CreateHitBox(string attackName,AttackInfo attackInfo,GameObject obj)
    {
        AttackHitBox hitBox=obj.GetComponent<AttackHitBox>();
        if(hitBox==null)hitBox=obj.AddComponent<AttackHitBox>();
        
        hitBox.actor=actor;
        hitBox.attackInfo=attackInfo;   
        hitBox.attackName=attackName;  
    } 
    public void DisableCollider(string name,List<Collision>collisions)
    {
        defenders.Remove(name);
        for(int i=0;i<collisions.Count;i++)
        {
            DisableOneCollider(name+i,collisions[i]);
        }
    }
    public void DisableOneCollider(string name, Collision collision)
    {
        if (Enable.TryGetValue(name, out var obj))
        {
            obj.SetActive(false);
            Disable[name] = obj;
            Enable.Remove(name);
        }
    }

    public Transform GetCollisions(Collision collision)
    {
        Transform socket = actor.transform.Find(collision.path);

        if (socket == null)
        {
            Debug.LogError("Collision path is invalid: " + collision.path, actor);
            return null;
        }

        Transform colliders = socket.Find("Colliders");

        if (colliders == null)
        {
            GameObject obj = new GameObject("Colliders");
            obj.transform.SetParent(socket, false);
            colliders = obj.transform;
        }

        ConfigureCollisionRoot(colliders, socket);
        return colliders;
    }

    void ConfigureCollisionRoot(Transform collisionRoot, Transform socket)
    {
        collisionRoot.localPosition = Vector3.zero;
        collisionRoot.localRotation = Quaternion.identity;
        collisionRoot.localScale = InverseLossyScale(socket);
    }

    Vector3 InverseLossyScale(Transform target)
    {
        Vector3 scale = target.lossyScale;

        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : 1f / scale.z
        );
    }

    public void CopyCollider(GameObject obj, Collision collision)
    {
        CapsuleCollider collider = obj.GetComponent<CapsuleCollider>();

        if (collider == null)
            collider = obj.AddComponent<CapsuleCollider>();

        Transform objTransform = obj.transform;
        objTransform.localPosition = collision.position;
        objTransform.localRotation = Quaternion.Euler(collision.rotation);
        objTransform.localScale = Vector3.one;

        collider.isTrigger = true;
        collider.center = Vector3.zero;
        collider.radius = Mathf.Max(0.001f, collision.Radius);
        collider.height = Mathf.Max(collision.Height, collider.radius * 2f);
    }
}

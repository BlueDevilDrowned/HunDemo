using UnityEngine;
[System.Flags]
public enum ActionPermission
{
    None=0,
    Move=1<<0,
    Rotation=1<<1,
    Jump=1<<2,
    Attack=1<<3,
}
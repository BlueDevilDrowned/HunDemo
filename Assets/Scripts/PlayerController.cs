using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerContriller : MonoBehaviour,InputSystem_Actions.IPlayerActions
{
    private InputSystem_Actions inputActions;
    [SerializeField]
    private LogicInput logicInput;
    private void Awake()
    {
        inputActions=new InputSystem_Actions();
        inputActions.Player.SetCallbacks(this);
    }
    private void OnEnable()
    {
        inputActions.Player.Enable();
    }
    private void OnDisable()
    {
        inputActions.Player.Disable();        
    }
    public void OnAttack(InputAction.CallbackContext context)
    {
       if(context.started)
        {
            logicInput.attack=true;
        }
        else if(context.canceled)
        {
            logicInput.attack=false;
        }
    }

    public void OnBlock(InputAction.CallbackContext context)
    {
        if(context.started)
        {
            logicInput.Block=true;
        }
        else if(context.canceled)
        {
            logicInput.Block=false;
        }
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
       
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if(context.started)
        {
            logicInput.Jump=true;
        }
        else if(context.canceled)
        {
            logicInput.Jump=false;
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
       logicInput.RawLook=context.ReadValue<Vector2>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        //根据shift处理数值
        Vector2 vector=context.ReadValue<Vector2>();
        logicInput._RawMove=vector;
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if(context.started)
        {
            logicInput.ShiftPressed=true;
        }
        else if(context.canceled)
        {
            logicInput.ShiftPressed=false;
        }
    }
}

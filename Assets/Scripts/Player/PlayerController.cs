using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private PlayerInputActions player_input_actions;
    private InputAction movement;
    private InputAction aim;

    private Rigidbody rb;
    private PlayerMovement p_movement;
    private PlayerCamera p_cam;

    private void Awake()
    {
        player_input_actions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;

        movement = player_input_actions.Player.Movement;
        movement.Enable();

        aim = player_input_actions.Player.Aim;
        aim.Enable();

        player_input_actions.Player.Jump.performed += DoJump;
        player_input_actions.Player.Jump.Enable();
    }

    private void OnDisable()
    {
        movement.Disable();
        aim.Disable();
        player_input_actions.Player.Jump.Disable();
    }

    void Start()
    {
        rb = gameObject.GetComponent<Rigidbody>();
        p_movement = gameObject.GetComponent<PlayerMovement>();
        //p_cam = gameObject.GetComponent<PlayerCamera>();
    }

    void Update()
    {
        //Debug.Log(aim.ReadValue<Vector2>());
        //p_cam.AimCamera(aim.ReadValue<Vector2>());
    }

    private void FixedUpdate()
    {
        //Debug.Log("Movement Values: " + movement.ReadValue<Vector2>());
        p_movement.Move(rb, movement.ReadValue<Vector2>());
    }

    private void DoJump(InputAction.CallbackContext obj)
    {
        //Debug.Log("Jump!");
        p_movement.Jump(rb);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * 3f);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using System;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Camera p_cam;
    [SerializeField] private Transform ground_check;
    [SerializeField] private LayerMask walkable_mask;
    private float ground_distance = .3f;

    [Header("Movement Properties")]
    [SerializeField] private float movement_speed = 1f;
    [SerializeField] private float max_speed = 9f;
    private Vector3 force_direction = Vector3.zero;

    [Header("Jumping Properties")]
    [SerializeField] private float jump_force = 1f;
    private float jump_time = 0f;
    private bool is_grounded = false;

    [Header("Gravity Shifting Properties")]
    [SerializeField] private float directional_gravity_scale = 1f;
    private float zero_gravity = 0f;

    private void Update()
    {
        is_grounded = Physics.CheckSphere(ground_check.position, ground_distance, walkable_mask);
    }

    public void Move(Rigidbody rb, Vector2 vector)
    {
        //rb.velocity = new Vector3(vector.x * movement_speed, rb.velocity.y, vector.y * movement_speed);
        force_direction += vector.x * GetCameraRight(p_cam) * movement_speed;
        force_direction += vector.y * GetCameraForward(p_cam) * movement_speed;

        rb.AddForce(force_direction, ForceMode.Impulse);
        force_direction = Vector3.zero;

        Vector3 horizontal_velocity = rb.velocity;
        horizontal_velocity.y = 0;
        if(horizontal_velocity.sqrMagnitude > max_speed * max_speed)
        {
            rb.velocity = horizontal_velocity.normalized * max_speed + Vector3.up * rb.velocity.y;
        }

        #region Angular Velocity
        Vector3 dir = rb.velocity;
        dir.y = 0f;

        if (vector.sqrMagnitude > 0.1f && dir.sqrMagnitude > 0.1f)
        {
            rb.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
        else
        {
            rb.angularVelocity = Vector3.zero;
        }
        #endregion
    }

    private Vector3 GetCameraForward(Camera p_cam)
    {
        Vector3 forward = p_cam.transform.forward;
        forward.y = 0;
        return forward.normalized;
    }

    private Vector3 GetCameraRight(Camera p_cam)
    {
        Vector3 right = p_cam.transform.right;
        right.y = 0;
        return right.normalized;
    }

    public void Jump(Rigidbody rb)
    {
        if (is_grounded)
        {
            force_direction.y = jump_force;
            rb.AddForce(force_direction, ForceMode.Impulse);
        }
    }

    public void GravityShift(Rigidbody rb)
    {

    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(ground_check.position, ground_distance);
    }
}

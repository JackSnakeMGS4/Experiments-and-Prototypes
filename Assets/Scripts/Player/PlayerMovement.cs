using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using System;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Camera p_cam;
    [SerializeField] private PhysicMaterial slope_material;
    [SerializeField] private PhysicMaterial ground_material;
    [SerializeField] private Transform ground_check;
    [SerializeField] private LayerMask walkable_mask;
    private float ground_distance = .3f;

    [Header("Movement Properties")]
    [SerializeField] private float movement_speed = 1f;
    [SerializeField] private float air_movement_speed = 1f;
    [SerializeField] private float max_speed = 9f;
    [SerializeField, Range(0f, 90f)] private float max_ground_angle = 25f;
    private Vector3 force_direction = Vector3.zero;
    private float min_ground_dot_product;

    [Header("Jumping Properties")]
    [SerializeField] private float jump_height = 1f;
    [SerializeField] private int max_air_jumps = 0;
    private bool on_ground = false;
    private int jump_phase;

    [Header("Gravity Shifting Properties")]
    [SerializeField] private float directional_gravity_scale = 1f;
    private float zero_gravity = 0f;
    private Vector3 contact_normal;

    private void Awake()
    {
        OnValidate();
    }

    private void Update()
    {
    }

    private void UpdateState()
    {
        if (on_ground)
        {
            jump_phase = 0;
        }
        else
        {
            contact_normal = Vector3.up;
        }
    }

    public void Move(Rigidbody rb, Vector2 vector)
    {
        float speed = on_ground ? movement_speed : air_movement_speed;

        #region Adjust Velocity To Angle
        Vector3 x_axis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 z_axis = ProjectOnContactPlane(Vector3.forward).normalized;
        //Debug.Log(x_axis + " : " + z_axis);

        Vector3 input = new Vector3(vector.x, 0, vector.y);
        float current_x = Vector3.Dot(input, x_axis);
        float current_z = Vector3.Dot(input, z_axis);
        //Debug.Log(current_x + " : " + current_z);

        force_direction += current_x * GetCameraRight(p_cam) * speed;
        force_direction += current_z * GetCameraForward(p_cam) * speed;
        //Debug.Log("Force Direction X: " + force_direction.x + "; Force Direction Z: " + force_direction.z);

        #endregion

        // Following is here as a fallback option
        #region No Velocity to Angle Adjustment
        //force_direction += vector.x * GetCameraRight(p_cam) * speed;
        //force_direction += vector.y * GetCameraForward(p_cam) * speed;
        //Debug.Log(force_direction);
        #endregion

        rb.AddForce(force_direction, ForceMode.Impulse);
        force_direction = Vector3.zero;

        #region Limit Max Speed
        Vector3 horizontal_velocity = rb.velocity;
        horizontal_velocity.y = 0;
        if(horizontal_velocity.sqrMagnitude > max_speed * max_speed)
        {
            rb.velocity = horizontal_velocity.normalized * max_speed + Vector3.up * rb.velocity.y;
        }
        #endregion

        #region Jump Floatiness
        if (rb.velocity.y < 0f)
        {
            rb.velocity -= Vector3.down * Physics.gravity.y * Time.fixedDeltaTime;
        }
        #endregion

        #region Angular Velocity
        Vector3 dir = rb.velocity;
        dir.y = 0f;

        if (vector.sqrMagnitude > 0.1f && dir.sqrMagnitude > 0.1f)
        {
            rb.rotation = Quaternion.LookRotation(dir, Vector3.up);
            rb.GetComponent<Collider>().material = ground_material;
        }
        else
        {
            rb.angularVelocity = Vector3.zero;
            //rb.GetComponent<Collider>().material = slope_material;
        }
        #endregion

        UpdateState();
        on_ground = false;
    }

    private void AdjustVelocity()
    {
        Vector3 x_axis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 z_axis = ProjectOnContactPlane(Vector3.forward).normalized;
    }

    private Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        // Aligns to a direction/velocity synonymous with the angle of terrain in world space
        return vector - contact_normal * Vector3.Dot(vector, contact_normal);
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
        if (on_ground || jump_phase < max_air_jumps)
        {
            jump_phase += 1;
            Vector3 vel = rb.velocity;
            float jump_speed = Mathf.Sqrt(-2f * Physics.gravity.y * jump_height);

            #region Slope-based Jump
            float aligned_speed = Vector3.Dot(vel, contact_normal);
            //Debug.Log(contact_normal);
            if(aligned_speed > 0f)
            {
                jump_speed = Mathf.Max(jump_speed - aligned_speed, 0f);
            }
            vel += contact_normal * jump_speed;
            #endregion

            #region Slopeless-based Jump
            //if(vel.y > 0f)
            //{
            //    jump_speed = Mathf.Max(jump_speed - vel.y, 0f);
            //}
            //vel.y += jump_speed;
            #endregion

            rb.velocity = vel;
        }
    }

    public void GravityShift(Rigidbody rb)
    {

    }

    private void OnValidate()
    {
        // Determines the steepest walkable angle
        min_ground_dot_product = Mathf.Cos(max_ground_angle * Mathf.Deg2Rad);
    }

    private void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void EvaluateCollision(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            //on_ground |= normal.y >= min_ground_dot_product;
            if(normal.y >= min_ground_dot_product)
            {
                on_ground = true;
                contact_normal = normal;
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(ground_check.position, ground_distance);
    }
}

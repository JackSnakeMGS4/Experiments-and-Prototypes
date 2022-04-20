using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using System;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] 
    private Camera p_cam;
    [SerializeField]
    LayerMask probeMask = -1;

    [SerializeField] 
    private PhysicMaterial slope_material;
    [SerializeField] 
    private PhysicMaterial ground_material;
    [SerializeField] 
    private Material on_ground_material;
    [SerializeField] 
    private Material off_ground_material;

    private Rigidbody rb;

    [Header("Movement Properties")]
    [SerializeField] 
    private float movement_speed = 1f;
    [SerializeField] 
    private float air_movement_speed = 1f;
    [SerializeField] 
    private float max_speed = 9f;
    [SerializeField, Range(0f, 90f)] 
    private float max_ground_angle = 25f;
    [Tooltip("Determines fastest speed possible before ground snapping no longer activates.")]
    [SerializeField, Range(0f, 100f)] 
    private float max_snap_speed = 30f;
    [SerializeField, Min(0f)]
    private float probe_distance = 2f;

    private Vector3 velocity = Vector3.zero;
    private Vector3 desired_velocity = new Vector3();
    private float min_ground_dot_product;
    private int steps_since_last_grounded;
    private Vector3 contact_normal;

    [Header("Jumping Properties")]
    [SerializeField] 
    private float jump_height = 1f;
    [SerializeField] 
    private int max_air_jumps = 0;

    private bool on_ground = false;
    private int jump_phase;
    private int steps_since_last_jump;

    private void Awake()
    {
        OnValidate();
        rb = gameObject.GetComponent<Rigidbody>();
    }

    #region Helper Methods
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

    private void UpdateState()
    {
        steps_since_last_grounded += 1;
        steps_since_last_jump += 1;
        velocity = rb.velocity;
        if (on_ground || SnapToGround())
        {
            steps_since_last_grounded = 0;
            jump_phase = 0;
            contact_normal.Normalize();
        }
        else
        {
            contact_normal = Vector3.up;
        }
    }

    private void ClearState()
    {
        on_ground = false;
        contact_normal = Vector3.zero;
    }

    private bool SnapToGround()
    {
        if(steps_since_last_grounded > 1 || steps_since_last_jump <= 2)
        {
            return false;
        }
        float speed = velocity.magnitude;
        if(speed > max_snap_speed)
        {
            return false;
        }
        if (!Physics.Raycast(rb.position, Vector3.down, out RaycastHit hit, probe_distance, probeMask))
        {
            return false;
        }
        if(hit.normal.y < min_ground_dot_product)
        {
            return false;
        }

        contact_normal = hit.normal;
        float dot = Vector3.Dot(velocity, hit.normal);
        if(dot > 0f)
        {
            velocity = (velocity - hit.normal * dot).normalized * speed;
        }
        return true;
    }

    private void OnValidate()
    {
        // Determines the steepest walkable angle
        min_ground_dot_product = Mathf.Cos(max_ground_angle * Mathf.Deg2Rad);
    }

    private void EvaluateCollision(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            //on_ground |= normal.y >= min_ground_dot_product;
            if (normal.y >= min_ground_dot_product)
            {
                on_ground = true;
                contact_normal += normal;
            }
        }
    }
    #endregion

    private void Update()
    {
        gameObject.GetComponentInChildren<MeshRenderer>().material = on_ground ? on_ground_material : off_ground_material;
    }

    public void Move(Vector2 vector)
    {
        #region Grab Player Input
        float speed = on_ground ? movement_speed : air_movement_speed;
        //Vector3 desired_velocity = new Vector3(vector.x, 0f, vector.y) * speed;

        //Previous line doesn't account for camera-based direction/velocity
        desired_velocity = Vector3.zero;
        desired_velocity += vector.x * GetCameraRight(p_cam) * speed;
        desired_velocity += vector.y * GetCameraForward(p_cam) * speed;
        #endregion

        UpdateState();

        #region Set Velocity Based Off Terrain Slope
        Vector3 x_axis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 z_axis = ProjectOnContactPlane(Vector3.forward).normalized;

        float current_x = Vector3.Dot(velocity, x_axis);
        float current_z = Vector3.Dot(velocity, z_axis);

        float max_speed_change = max_speed * Time.deltaTime;

        float new_x = Mathf.MoveTowards(current_x, desired_velocity.x, max_speed_change);
        float new_z = Mathf.MoveTowards(current_z, desired_velocity.z, max_speed_change);
        velocity += x_axis * (new_x - current_x) + z_axis * (new_z - current_z);
        #endregion

        // Visualize force direction to applied velocity
        //Debug.DrawLine(transform.position, transform.position + velocity * 10f, Color.blue);
        rb.velocity = velocity;

        // Following is here as a fallback option
        #region No Velocity to Angle Adjustment
        //velocity += vector.x * GetCameraRight(p_cam) * speed;
        //velocity += vector.y * GetCameraForward(p_cam) * speed;
        //Debug.Log(force_direction);
        #endregion

        // Here as fallback option
        #region Limit Max Speed
        //Vector3 horizontal_velocity = rb.velocity;
        //horizontal_velocity.y = 0;
        //if(horizontal_velocity.sqrMagnitude > max_speed * max_speed)
        //{
        //    rb.velocity = horizontal_velocity.normalized * max_speed + Vector3.up * rb.velocity.y;
        //}
        #endregion

        #region Jump Floatiness
        if (rb.velocity.y < 0f)
        {
            rb.velocity -= Vector3.down * Physics.gravity.y * Time.deltaTime;
        }
        #endregion

        #region Prevent Angular Spin and Slope Slide
        Vector3 dir = rb.velocity;
        dir.y = 0f;

        if (vector.sqrMagnitude > 0.1f && dir.sqrMagnitude > 0.1f)
        {
            rb.rotation = Quaternion.LookRotation(dir, Vector3.up);
            //rb.GetComponent<Collider>().material = ground_material;
        }
        else
        {
            rb.angularVelocity = Vector3.zero;
            //rb.GetComponent<Collider>().material = slope_material;
        }
        #endregion
        ClearState();
    }

    public void Jump()
    {
        if (on_ground || jump_phase < max_air_jumps)
        {
            steps_since_last_jump = 0;
            jump_phase += 1;
            Vector3 vel = rb.velocity;
            float jump_speed = Mathf.Sqrt(-2f * Physics.gravity.y * jump_height);

            #region Slope-based Jump
            //float aligned_speed = Vector3.Dot(vel, contact_normal);
            ////Debug.Log(contact_normal);
            //if(aligned_speed > 0f)
            //{
            //    jump_speed = Mathf.Max(jump_speed - aligned_speed, 0f);
            //}
            //vel += contact_normal * jump_speed;
            #endregion

            #region Slopeless-based Jump
            if (vel.y > 0f)
            {
                jump_speed = Mathf.Max(jump_speed - vel.y, 0f);
            }
            vel.y += jump_speed;
            #endregion

            //rb.velocity = vel;
            rb.AddForce(vel, ForceMode.Impulse);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, probe_distance);
    }
}

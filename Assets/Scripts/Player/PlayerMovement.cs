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
    LayerMask probe_mask = -1, stair_mask = -1;

    [SerializeField] 
    private PhysicMaterial slope_material;
    [SerializeField] 
    private PhysicMaterial ground_material;

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
    [SerializeField, Range(0f, 90f)]
    private float max_stairs_angle = 65f;
    [Tooltip("Determines fastest speed possible before ground snapping no longer activates.")]
    [SerializeField, Range(0f, 100f)] 
    private float max_snap_speed = 30f;
    [SerializeField, Min(0f)]
    private float probe_distance = 2f;

    private Vector3 velocity = Vector3.zero;
    private Vector3 desired_velocity = new Vector3();
    private float min_ground_dot_product;
    private float min_stairs_dot_product;
    private int steps_since_last_grounded;
    private Vector3 contact_normal;
    private Vector3 steep_normal;
    private int ground_contact_count;
    private int steep_contact_count;
    private bool _On_Ground => ground_contact_count > 0;
    private bool _On_Steep => steep_contact_count > 0;


    [Header("Jumping Properties")]
    [SerializeField] 
    private float jump_height = 1f;
    [SerializeField] 
    private int max_air_jumps = 0;

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
        if (_On_Ground || SnapToGround() || CheckSteepContacts())
        {
            steps_since_last_grounded = 0;
            if(steps_since_last_jump > 1)
            {
                jump_phase = 0;
            }
            if(ground_contact_count > 1)
            {
                contact_normal.Normalize();
            }
        }
        else
        {
            contact_normal = Vector3.up;
        }
    }

    private void ClearState()
    {
        ground_contact_count = steep_contact_count = 0;
        contact_normal = steep_normal = Vector3.zero;
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
        if (!Physics.Raycast(rb.position, Vector3.down, out RaycastHit hit, probe_distance, probe_mask))
        {
            return false;
        }
        if(hit.normal.y < GetMinDot(hit.collider.gameObject.layer))
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
        // Determines the steepest walkable angle for stairs layer
        min_stairs_dot_product = Mathf.Cos(max_stairs_angle * Mathf.Deg2Rad);
    }

    private float GetMinDot(int layer)
    {
        return (stair_mask & (1 << layer)) == 0 ? min_ground_dot_product : min_stairs_dot_product;
    }

    private void EvaluateCollision(Collision collision)
    {
        float min_dot = GetMinDot(collision.gameObject.layer);
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            //on_ground |= normal.y >= min_ground_dot_product;
            if (normal.y >= min_dot)
            {
                ground_contact_count += 1;
                contact_normal += normal;
            }
            else if(normal.y > -0.01f)
            {
                steep_contact_count += 1;
                steep_normal = normal;
            }
        }
    }

    private bool CheckSteepContacts()
    {
        if (steep_contact_count > 1)
        {
            steep_normal.Normalize();
            if (steep_normal.y >= min_ground_dot_product)
            {
                ground_contact_count = 1;
                contact_normal = steep_normal;
                return true;
            }
        }
        return false;
    }
    #endregion

    private void Update()
    {
        //gameObject.GetComponentInChildren<MeshRenderer>().material = _On_Ground ? on_ground_material : off_ground_material;
        gameObject.GetComponentInChildren<MeshRenderer>().material.SetColor("_BaseColor", Color.white * (ground_contact_count * 0.25f));
    }

    public void Move(Vector2 vector)
    {
        #region Grab Player Input
        float speed = _On_Ground ? movement_speed : air_movement_speed;
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

        ClearState();
    }

    public void Jump()
    {
        Vector3 jump_direction;

        #region Check Jump Validity
        if (_On_Ground)
        {
            jump_direction = contact_normal;
        }
        else if (_On_Steep)
        {
            jump_direction = steep_normal;
            //Allow new jump sequence after wall touch
            jump_phase = 0;
        }
        else if (max_air_jumps > 0 && jump_phase <= max_air_jumps)
        {
            //Prevent Extra Air Jump Bug
            if(jump_phase == 0)
            {
                jump_phase = 1;
            }
            contact_normal = Vector3.up;
            jump_direction = contact_normal;
        }
        else
        {
            return;
        }
        #endregion

        steps_since_last_jump = 0;
        jump_phase += 1;
        float jump_speed = Mathf.Sqrt(-2f * Physics.gravity.y * jump_height);

        #region Slope-based Jump
        float aligned_speed = Vector3.Dot(velocity, jump_direction);
        //Debug.Log(contact_normal);
        if (aligned_speed > 0f)
        {
            jump_speed = Mathf.Max(jump_speed - aligned_speed, 0f);
        }
        velocity += jump_direction * jump_speed;
        #endregion

        #region Slopeless-based Jump
        //if (vel.y > 0f)
        //{
        //    jump_speed = Mathf.Max(jump_speed - vel.y, 0f);
        //}
        //vel.y += jump_speed;
        #endregion

        //rb.velocity = velocity;
        rb.AddForce(velocity, ForceMode.Impulse);
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

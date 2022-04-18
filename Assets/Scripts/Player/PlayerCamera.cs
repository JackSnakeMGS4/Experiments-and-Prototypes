using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class PlayerCamera : MonoBehaviour
{
    // Works well for over-the-shoulder cameras in Cinemachine
    [SerializeField] private Transform shoulder_point;

    [Header("Camera Properties")]
    [SerializeField] private float sensitivity = 1f;
    [SerializeField] private float max_x_orbit = 40f;
    [SerializeField] private float min_x_orbit = -30f;
    [SerializeField] private float lerp_rate = .5f;

    private Quaternion next_rotation;

    public void AimCamera(Vector2 vector)
    {
        #region Horizontal Rotation
        shoulder_point.rotation *= Quaternion.AngleAxis(vector.x * sensitivity, Vector3.up);
        #endregion

        #region Vertical Rotation
        shoulder_point.rotation *= Quaternion.AngleAxis(-vector.y * sensitivity, Vector3.right);

        //Debug.Log(shoulder_point.localEulerAngles);

        var angles = shoulder_point.localEulerAngles;
        angles.z = 0;

        var x_angle = shoulder_point.localEulerAngles.x;
        //Debug.Log(x_angle);

        if (x_angle > 180 && x_angle < max_x_orbit)
        {
            angles.x = max_x_orbit;
        }
        else if (x_angle < 180 && x_angle > min_x_orbit)
        {
            angles.x = min_x_orbit;
        }

        shoulder_point.localEulerAngles = angles;
        #endregion

        next_rotation = Quaternion.Lerp(shoulder_point.rotation, next_rotation, Time.deltaTime * lerp_rate);

        //Set the player rotation based on the look transform
        transform.rotation = Quaternion.Euler(0, shoulder_point.rotation.eulerAngles.y, 0);
        //reset the y rotation of the look transform
        shoulder_point.localEulerAngles = new Vector3(angles.x, 0, 0);
    }
}

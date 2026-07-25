using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

public class CameraZoom : MonoBehaviour
{
    public FirstPersonAIO FpsScript;
    public float Fov = 60;
    public float Zoom_Fov = 35;
    private bool is_clicking = false;


    void Update()
    {
        // -------------------Code for Zooming Out------------
        if (Input.GetAxis("Mouse ScrollWheel") < 0)
        {
            if (FpsScript.baseCamFOV <= Fov)
                FpsScript.baseCamFOV += 2;
        }
        // ---------------Code for Zooming In------------------------
        if (Input.GetAxis("Mouse ScrollWheel") > 0)
        {
            if (FpsScript.baseCamFOV > Zoom_Fov)
                FpsScript.baseCamFOV -= 2;
        }

        if (Input.GetMouseButton(1))
        {
            is_clicking = true;
        }
        if (Input.GetMouseButtonUp(1) && is_clicking)
        {
            is_clicking = false;
        }

        if (is_clicking)
        {
            FpsScript.baseCamFOV = Zoom_Fov;

        }
        else
        {
            FpsScript.baseCamFOV = Fov;

        }
    }
}
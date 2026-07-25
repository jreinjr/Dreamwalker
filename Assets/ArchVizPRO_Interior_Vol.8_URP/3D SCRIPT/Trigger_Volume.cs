using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Trigger_Volume : MonoBehaviour
{
    public GameObject Ceiling_Props_Livingroom;
    public GameObject Ceiling_Props_Bedroom;
    public GameObject Ceiling_Props_Guestroom;
    public GameObject Ceiling_Props_Bathroom_Main;
    public GameObject Ceiling_Props_Bathroom_Bedroom;

    public Camera MainCamera;

    private void Start()
    {
        MainCamera.clearFlags = CameraClearFlags.SolidColor;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody)
        {
            //other.attachedRigidbody.AddForce(Vector3.up * 10);
            Ceiling_Props_Livingroom.active = true;
            Ceiling_Props_Bedroom.active = true;
            Ceiling_Props_Guestroom.active = true;
            Ceiling_Props_Bathroom_Main.active = true;
            Ceiling_Props_Bathroom_Bedroom.active = true;

            Debug.Log("I am inside");

            MainCamera.clearFlags = CameraClearFlags.Skybox;
        }
    }

    private void OnTriggerExit(Collider other)
        {
            if (other.attachedRigidbody)
            {
            Ceiling_Props_Livingroom.active = false;
            Ceiling_Props_Bedroom.active = false;
            Ceiling_Props_Guestroom.active = false;
            Ceiling_Props_Bathroom_Main.active = false;
            Ceiling_Props_Bathroom_Bedroom.active = false;

            Debug.Log("I am otside");

            MainCamera.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    public void Show_Ceiling_Props()
    {
        Ceiling_Props_Livingroom.active = true;
        Ceiling_Props_Bedroom.active = true;
        Ceiling_Props_Guestroom.active = true;
        Ceiling_Props_Bathroom_Main.active = true;
        Ceiling_Props_Bathroom_Bedroom.active = true;
    }

    public void Hide_Ceiling_Props()
    {
        Ceiling_Props_Livingroom.active = false;
        Ceiling_Props_Bedroom.active = false;
        Ceiling_Props_Guestroom.active = false;
        Ceiling_Props_Bathroom_Main.active = false;
        Ceiling_Props_Bathroom_Bedroom.active = false;
    }
}

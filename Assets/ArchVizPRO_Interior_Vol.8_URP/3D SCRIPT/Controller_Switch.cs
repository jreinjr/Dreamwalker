using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Controller_Switch : MonoBehaviour
{
    public GameObject FIRST_PERSON_Controller;
    public GameObject ORBIT_Controller;
    public GameObject CINEMATIC_Controller;
    public GameObject CINEMATIC_FPS_Controller;
    public Trigger_Volume Trigger_Volume;

    public GameObject Menu;
    public GameObject Menu_FIRST_PERSON;
    public GameObject Menu_ORBIT;
    public GameObject Menu_CINEMATIC;
    public GameObject Menu_CINEMATIC_WALKTHROUGH;
    public FirstPersonAIO FPS_Script;
    public CapsuleCollider Character_Controller;

    void MENU_OPEN()
    {
        Menu.SetActive(true);

        FPS_Script.controllerPauseState = false;
        FPS_Script.ControllerPause();
    }
    void MENU_CLOSE()
    {
        Menu.SetActive(false);

        FPS_Script.controllerPauseState = true;
        FPS_Script.ControllerPause();
    }
    public void MENU_FIRST_PERSON() {
        MENU_INFO_HIDE();
        Menu_FIRST_PERSON.SetActive(true);
    }
    public void MENU_ORBIT()
    {
        MENU_INFO_HIDE();
        Menu_ORBIT.SetActive(true);
    }
    public void MENU_CINEMATIC()
    {
        MENU_INFO_HIDE();
        Menu_CINEMATIC.SetActive(true);
    }
    public void MENU_CINEMATIC_WALKTHROUGH()
    {
        MENU_INFO_HIDE();
        Menu_CINEMATIC_WALKTHROUGH.SetActive(true);
    }
    public void MENU_INFO_HIDE()
    {
        Menu_FIRST_PERSON.SetActive(false);
        Menu_ORBIT.SetActive(false);
        Menu_CINEMATIC.SetActive(false);
        Menu_CINEMATIC_WALKTHROUGH.SetActive(false);
    }


    void Update()
    {

        if (Input.GetKeyUp("1"))
        {
            FIRST_PERSON();
        }
        if (Input.GetKeyUp("2"))
        {
            ORBIT();
        }
        if (Input.GetKeyUp("3"))
        {
            CINEMATIC();
        }
        if (Input.GetKeyUp("4"))
        {
            CINEMATIC_WALKTHROUGH();
        }
        if (Input.GetKeyUp("escape"))
        {
            //Application.Quit();
            if (!Menu.activeInHierarchy)
            {
                MENU_OPEN();
            } else
            {
                MENU_CLOSE();
            }
        }
    }

    public void FIRST_PERSON()
    {
        FIRST_PERSON_Controller.SetActive(true);
        ORBIT_Controller.SetActive(false);
        CINEMATIC_Controller.SetActive(false);
        CINEMATIC_FPS_Controller.SetActive(false);

        Trigger_Volume.Show_Ceiling_Props();

        MENU_CLOSE();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    public void ORBIT()
    {
        FIRST_PERSON_Controller.SetActive(false);
        ORBIT_Controller.SetActive(true);
        CINEMATIC_Controller.SetActive(false);
        CINEMATIC_FPS_Controller.SetActive(false);

        Trigger_Volume.Hide_Ceiling_Props();

        MENU_CLOSE();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

    }
    public void CINEMATIC()
    {
        FIRST_PERSON_Controller.SetActive(false);
        ORBIT_Controller.SetActive(false);
        CINEMATIC_Controller.SetActive(true);
        CINEMATIC_FPS_Controller.SetActive(false);

        Trigger_Volume.Show_Ceiling_Props();

        MENU_CLOSE();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    public void CINEMATIC_WALKTHROUGH()
    {
        FIRST_PERSON_Controller.SetActive(false);
        ORBIT_Controller.SetActive(false);
        CINEMATIC_Controller.SetActive(false);
        CINEMATIC_FPS_Controller.SetActive(true);

        Trigger_Volume.Show_Ceiling_Props();

        MENU_CLOSE();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void EXIT()
    {
        Application.Quit();
    }

    public void Awake()
    {
        Character_Controller.radius = 0.1f;
    }

    public void Start()
    {
        Character_Controller.radius = 0.1f;
    }


}


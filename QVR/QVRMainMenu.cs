// Use the Unity new GUI with Unity 4.6 or above.
#if UNITY_4_6 || UNITY_5_0
#define USE_NEW_GUI
#endif

using System;
using System.Collections;
using UnityEngine;
#if USE_NEW_GUI
using UnityEngine.UI;
# endif

public class QVRMainMenu : MonoBehaviour
{
    public float FadeInTime = 2.0f;
    public UnityEngine.Texture FadeInTexture = null;
    public Font FontReplace = null;

    // Scenes to show onscreen
    public string[] Scenes;

    private bool ScenesVisible = false;

    // Handle to OVRCameraRig
    private OVRCameraRig CameraController = null;

    // Handle to OVRPlayerController
    private OVRPlayerController PlayerController = null;

    // Controller buttons
    private bool PrevStartDown;

    private bool ShowVRVars;

    // IPD shift from physical IPD
    public float IPDIncrement = 0.0025f;
    private string strIPD = "IPD: 0.000";

    // Prediction (in ms)
    public float PredictionIncrement = 0.001f; // 1 ms
    private string strPrediction = "Pred: OFF";

    // FOV Variables
    public float FOVIncrement = 0.2f;
    private string strFOV = "FOV: 0.0f";

    // Height adjustment 
    public float HeightIncrement = 0.01f;
    private string strHeight = "Height: 0.0f";

    // Speed and rotation adjustment
    public float SpeedRotationIncrement = 0.05f;
    private string strSpeedRotationMultipler = "Spd. X: 0.0f Rot. X: 0.0f";

    private bool LoadingLevel = false;
    //private float  AlphaFadeValue	= 1.0f;
    private int CurrentLevel = 0;

    // Rift detection
    //private bool HMDPresent = false;
    private float RiftPresentTimeout = 0.0f;
    private string strRiftPresent = "";

    // Replace the GUI with our own texture and 3D plane that
    // is attached to the rendder camera for true 3D placement
    private OVRGUI GuiHelper = new OVRGUI();
    private GameObject GUIRenderObject = null;
    private RenderTexture GUIRenderTexture = null;

    // We want to use new Unity GUI built in 4.6 for OVRMainMenu GUI
    // Enable the UsingNewGUI option in the editor, 
    // if you want to use new GUI and Unity version is higher than 4.6    
#if USE_NEW_GUI
    private GameObject NewGUIObject = null;
    private GameObject RiftPresentGUIObject = null;
#endif

    // We can set the layer to be anything we want to, this allows
    // a specific camera to render it
    public string LayerName = "Default";

    // Crosshair system, rendered onto 3D plane
    public UnityEngine.Texture CrosshairImage = null;
    private OVRCrosshair Crosshair = new OVRCrosshair();

    // Resolution Eye Texture
    private string strResolutionEyeTexture = "Resolution: 0 x 0";

    // Latency values
    private string strLatencies = "Ren: 0.0f TWrp: 0.0f PostPresent: 0.0f";

    // Vision mode on/off
    private bool VisionMode = true;
#if	SHOW_DK2_VARIABLES
	private string strVisionMode = "Vision Enabled: ON";
#endif

    // We want to hold onto GridCube, for potential sharing
    // of the menu RenderTarget
    OVRGridCube GridCube = null;

    // We want to hold onto the VisionGuide so we can share
    // the menu RenderTarget
    OVRVisionGuide VisionGuide = null;

    #region MonoBehaviour Message Handlers
    /// <summary>----------------------------------------------------------------------------------------------------
    /// Awake this instance.
    /// </summary>---------------------------------------------------------------------------------------------------
    void Awake()
    {
        // Find camera controller
        OVRCameraRig[] CameraControllers;
        CameraControllers = gameObject.GetComponentsInChildren<OVRCameraRig>();

        if (CameraControllers.Length == 0)
            Debug.LogWarning("OVRMainMenu: No OVRCameraRig attached.");
        else if (CameraControllers.Length > 1)
            Debug.LogWarning("OVRMainMenu: More then 1 OVRCameraRig attached.");
        else
        {
            CameraController = CameraControllers[0];
#if USE_NEW_GUI
            OVRUGUI.CameraController = CameraController;
#endif
        }

        // Find player controller
        OVRPlayerController[] PlayerControllers;
        PlayerControllers = gameObject.GetComponentsInChildren<OVRPlayerController>();

        if (PlayerControllers.Length == 0)
            Debug.LogWarning("OVRMainMenu: No OVRPlayerController attached.");
        else if (PlayerControllers.Length > 1)
            Debug.LogWarning("OVRMainMenu: More then 1 OVRPlayerController attached.");
        else
        {
            PlayerController = PlayerControllers[0];
#if USE_NEW_GUI
            OVRUGUI.PlayerController = PlayerController;
#endif
        }

#if USE_NEW_GUI
        // Create canvas for using new GUI
        NewGUIObject = new GameObject();
        NewGUIObject.name = "OVRGUIMain";
        NewGUIObject.transform.parent = GameObject.Find("LeftEyeAnchor").transform;
        RectTransform r = NewGUIObject.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(100f, 100f);
        r.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        r.localPosition = new Vector3(0.01f, 0.17f, 0.53f);
        r.localEulerAngles = Vector3.zero;

        Canvas c = NewGUIObject.AddComponent<Canvas>();
#if (UNITY_5_0)
        // TODO: Unity 5.0b11 has an older version of the new GUI being developed in Unity 4.6.
        // Remove this once Unity 5 has a more recent merge of Unity 4.6.
        c.renderMode = RenderMode.WorldSpace;
#else
	        c.renderMode = RenderMode.WorldSpace;
#endif
        c.pixelPerfect = false;
#endif
    }

    /// <summary>----------------------------------------------------------------------------------------------------
    /// Start this instance.
    /// </summary>---------------------------------------------------------------------------------------------------
    void Start() {
        CurrentLevel = 0;
        PrevStartDown = false;
        ShowVRVars = false;
        LoadingLevel = false;
        ScenesVisible = false;

        // Set the GUI target
        GUIRenderObject = GameObject.Instantiate(Resources.Load("OVRGUIObjectMain")) as GameObject;

        if (GUIRenderObject != null)
        {
            // Chnge the layer
            GUIRenderObject.layer = LayerMask.NameToLayer(LayerName);

            if (GUIRenderTexture == null)
            {
                int w = Screen.width;
                int h = Screen.height;

                // We don't need a depth buffer on this texture
                GUIRenderTexture = new RenderTexture(w, h, 0);
                GuiHelper.SetPixelResolution(w, h);
                // NOTE: All GUI elements are being written with pixel values based
                // from DK1 (1280x800). These should change to normalized locations so 
                // that we can scale more cleanly with varying resolutions
                GuiHelper.SetDisplayResolution(1280.0f, 800.0f);
            }
        }

        // Attach GUI texture to GUI object and GUI object to Camera
        if (GUIRenderTexture != null && GUIRenderObject != null)
        {
            GUIRenderObject.GetComponent<Renderer>().material.mainTexture = GUIRenderTexture;

            if (CameraController != null)
            {
                // Grab transform of GUI object
                Vector3 ls = GUIRenderObject.transform.localScale;
                Vector3 lp = GUIRenderObject.transform.localPosition;
                Quaternion lr = GUIRenderObject.transform.localRotation;

                // Attach the GUI object to the camera
                GUIRenderObject.transform.parent = CameraController.centerEyeAnchor;
                // Reset the transform values (we will be maintaining state of the GUI object
                // in local state)

                GUIRenderObject.transform.localScale = ls;
                GUIRenderObject.transform.localPosition = lp;
                GUIRenderObject.transform.localRotation = lr;

                // Deactivate object until we have completed the fade-in
                // Also, we may want to deactive the render object if there is nothing being rendered
                // into the UI
                GUIRenderObject.SetActive(false);
            }
        }

        // Make sure to hide cursor 
        if (Application.isEditor == false)
        {
#if UNITY_5_0
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
#else
			Cursor.visible = false; 
			Screen.lockCursor = true;
#endif
        }

        // CameraController updates
        if (CameraController != null)
        {
            // Add a GridCube component to this object
            GridCube = gameObject.AddComponent<OVRGridCube>();
            GridCube.SetOVRCameraController(ref CameraController);

            // Add a VisionGuide component to this object
            VisionGuide = gameObject.AddComponent<OVRVisionGuide>();
            VisionGuide.SetOVRCameraController(ref CameraController);
            VisionGuide.SetFadeTexture(ref FadeInTexture);
            VisionGuide.SetVisionGuideLayer(ref LayerName);
        }

        // Crosshair functionality
        Crosshair.Init();
        Crosshair.SetCrosshairTexture(ref CrosshairImage);
        Crosshair.SetOVRCameraController(ref CameraController);
        Crosshair.SetOVRPlayerController(ref PlayerController);

        // Check for HMD and sensor
        //CheckIfRiftPresent();

#if USE_NEW_GUI
        if (!string.IsNullOrEmpty(strRiftPresent))
        {
            ShowRiftPresentGUI();
        }
#endif
    }

    /// <summary>---------------------------------------------------------------------------------------------------
    /// Update this instance.
    /// </summary>--------------------------------------------------------------------------------------------------
    void Update()
    {
        if (LoadingLevel == true)
            return;

        // CameraController updates
        if (CameraController != null)
        {
            UpdateIPD();

            UpdateRecenterPose();
            UpdateVisionMode();
            UpdateFOV();
            UpdateResolutionEyeTexture();
            UpdateLatencyValues();
        }

        // PlayerController updates
        if (PlayerController != null)
        {
            UpdateSpeedAndRotationScaleMultiplier();
            UpdatePlayerControllerMovement();
        }

        // MainMenu updates
        UpdateSelectCurrentLevel();

        // Crosshair functionality
        Crosshair.UpdateCrosshair();

#if USE_NEW_GUI
        if (ShowVRVars && RiftPresentTimeout <= 0.0f)
        {
            NewGUIObject.SetActive(true);
            UpdateNewGUIVars();
            OVRUGUI.UpdateGUI();
        }
        else
        {
            NewGUIObject.SetActive(false);
        }
#endif
    }

    /// <summary>
    /// Updates Variables for new GUI.
    /// </summary>
#if USE_NEW_GUI
    void UpdateNewGUIVars()
    {
#if	SHOW_DK2_VARIABLES		
        // Print out Vision Mode
        OVRUGUI.strVisionMode = strVisionMode;		
#endif
        // Print out FPS
        //OVRUGUI.strFPS = strFPS;

        // Don't draw these vars if CameraController is not present
        if (CameraController != null)
        {
            OVRUGUI.strPrediction = strPrediction;
            OVRUGUI.strIPD = strIPD;
            OVRUGUI.strFOV = strFOV;
            OVRUGUI.strResolutionEyeTexture = strResolutionEyeTexture;
            OVRUGUI.strLatencies = strLatencies;
        }

        // Don't draw these vars if PlayerController is not present
        if (PlayerController != null)
        {
            OVRUGUI.strHeight = strHeight;
            OVRUGUI.strSpeedRotationMultipler = strSpeedRotationMultipler;
        }

        OVRUGUI.strRiftPresent = strRiftPresent;
    }
#endif
    #endregion


    #region Internal State Management Functions

    /// <summary>----------------------------------------------------------------------------------------------------
    /// Updates the IPD.
    /// </summary>---------------------------------------------------------------------------------------------------
    void UpdateIPD()
    {
        if (ShowVRVars == true) // limit gc
        {
            strIPD = System.String.Format("IPD (mm): {0:F4}", OVRManager.profile.ipd * 1000.0f);
        }
    }

    void UpdateRecenterPose()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            OVRManager.display.RecenterPose();
        }
    }

    /// <summary>---------------------------------------------------------------------------------------------------
    /// Updates the vision mode.
    /// </summary>---------------------------------------------------------------------------------------------------
    void UpdateVisionMode()
    {
        if (Input.GetKeyDown(KeyCode.F2))
        {
            VisionMode = !VisionMode;
            OVRManager.tracker.isEnabled = VisionMode;

#if SHOW_DK2_VARIABLES
			strVisionMode = VisionMode ? "Vision Enabled: ON" : "Vision Enabled: OFF";
#endif
        }
    }

    /// <summary>---------------------------------------------------------------------------------------------------
    /// Updates the FOV.
    /// </summary>---------------------------------------------------------------------------------------------------
    void UpdateFOV()
    {
        if (ShowVRVars == true)// limit gc
        {
            OVRDisplay.EyeRenderDesc eyeDesc = OVRManager.display.GetEyeRenderDesc(OVREye.Left);

            strFOV = System.String.Format("FOV (deg): {0:F3}", eyeDesc.fov.y);
        }
    }

    /// <summary>---------------------------------------------------------------------------------------------------
    /// Updates resolution of eye texture
    /// </summary>---------------------------------------------------------------------------------------------------
    void UpdateResolutionEyeTexture()
    {
        if (ShowVRVars == true) // limit gc
        {
            OVRDisplay.EyeRenderDesc leftEyeDesc = OVRManager.display.GetEyeRenderDesc(OVREye.Left);
            OVRDisplay.EyeRenderDesc rightEyeDesc = OVRManager.display.GetEyeRenderDesc(OVREye.Right);

            float scale = OVRManager.instance.nativeTextureScale * OVRManager.instance.virtualTextureScale;
            float w = (int)(scale * (float)(leftEyeDesc.resolution.x + rightEyeDesc.resolution.x));
            float h = (int)(scale * (float)Mathf.Max(leftEyeDesc.resolution.y, rightEyeDesc.resolution.y));

            strResolutionEyeTexture = System.String.Format("Resolution : {0} x {1}", w, h);
        }
    }

    /// <summary>---------------------------------------------------------------------------------------------------
    /// Updates latency values
    /// </summary>---------------------------------------------------------------------------------------------------
    void UpdateLatencyValues()
    {
#if !UNITY_ANDROID || UNITY_EDITOR
        if (ShowVRVars == true) // limit gc
        {
            OVRDisplay.LatencyData latency = OVRManager.display.latency;
            if (latency.render < 0.000001f && latency.timeWarp < 0.000001f && latency.postPresent < 0.000001f)
                strLatencies = System.String.Format("Ren : N/A TWrp: N/A PostPresent: N/A");
            else
                strLatencies = System.String.Format("Ren : {0:F3} TWrp: {1:F3} PostPresent: {2:F3}",
                    latency.render,
                    latency.timeWarp,
                    latency.postPresent);
        }
#endif
    }

    /// <summary>---------------------------------------------------------------------------------------------------
    /// Updates the speed and rotation scale multiplier.
    /// </summary>---------------------------------------------------------------------------------------------------
    void UpdateSpeedAndRotationScaleMultiplier()
    {
        float moveScaleMultiplier = 0.0f;
        PlayerController.GetMoveScaleMultiplier(ref moveScaleMultiplier);
        if (Input.GetKeyDown(KeyCode.Alpha7))
            moveScaleMultiplier -= SpeedRotationIncrement;
        else if (Input.GetKeyDown(KeyCode.Alpha8))
            moveScaleMultiplier += SpeedRotationIncrement;
        PlayerController.SetMoveScaleMultiplier(moveScaleMultiplier);

        float rotationScaleMultiplier = 0.0f;
        PlayerController.GetRotationScaleMultiplier(ref rotationScaleMultiplier);
        if (Input.GetKeyDown(KeyCode.Alpha9))
            rotationScaleMultiplier -= SpeedRotationIncrement;
        else if (Input.GetKeyDown(KeyCode.Alpha0))
            rotationScaleMultiplier += SpeedRotationIncrement;
        PlayerController.SetRotationScaleMultiplier(rotationScaleMultiplier);

        if (ShowVRVars == true)// limit gc
            strSpeedRotationMultipler = System.String.Format("Spd.X: {0:F2} Rot.X: {1:F2}",
                                    moveScaleMultiplier,
                                    rotationScaleMultiplier);
    }

    /// <summary>---------------------------------------------------------------------------------------------------
    /// Updates the player controller movement.
    /// </summary>---------------------------------------------------------------------------------------------------
    void UpdatePlayerControllerMovement()
    {
        if (PlayerController != null)
            PlayerController.SetHaltUpdateMovement(ScenesVisible);
    }

    /// <summary>---------------------------------------------------------------------------------------------------
    /// Updates the select current level.
    /// </summary>---------------------------------------------------------------------------------------------------
    void UpdateSelectCurrentLevel()
    {
        ShowLevels();

        if (!ScenesVisible)
            return;

        CurrentLevel = Application.loadedLevel;

        if (Scenes.Length != 0
            && (OVRGamepadController.GPC_GetButton(OVRGamepadController.Button.A)
                || Input.GetKeyDown(KeyCode.Return)))
        {
            LoadingLevel = true;
            Application.LoadLevelAsync(Scenes[CurrentLevel]);
        }
    }

    /// <summary>---------------------------------------------------------------------------------------------------
    /// Shows the levels.
    /// </summary>---------------------------------------------------------------------------------------------------
    /// <returns><c>true</c>, if levels was shown, <c>false</c> otherwise.</returns>
    bool ShowLevels()
    {
        if (Scenes.Length == 0)
        {
            ScenesVisible = false;
            return ScenesVisible;
        }

        bool curStartDown = OVRGamepadController.GPC_GetButton(OVRGamepadController.Button.Start);
        bool startPressed = (curStartDown && !PrevStartDown) || Input.GetKeyDown(KeyCode.RightShift);
        PrevStartDown = curStartDown;

        if (startPressed)
        {
            ScenesVisible = !ScenesVisible;
        }

        return ScenesVisible;
    }

    /// <summary>---------------------------------------------------------------------------------------------------
    /// Show if Rift is detected.
    /// </summary>---------------------------------------------------------------------------------------------------
    /// <returns><c>true</c>, if show rift detected was GUIed, <c>false</c> otherwise.</returns>
    bool GUIShowRiftDetected()
    {
#if !USE_NEW_GUI
		if(RiftPresentTimeout > 0.0f)
		{
			GuiHelper.StereoBox (StartX, StartY, WidthX, WidthY, 
								 ref strRiftPresent, Color.white);
		
			return true;
        }
#else
        if (RiftPresentTimeout < 0.0f)
            DestroyImmediate(RiftPresentGUIObject);
#endif
        return false;
    }

    /// <summary>---------------------------------------------------------------------------------------------------
    /// Show rift present GUI with new GUI
    /// </summary>---------------------------------------------------------------------------------------------------
    void ShowRiftPresentGUI()
    {
#if USE_NEW_GUI
        RiftPresentGUIObject = new GameObject();
        RiftPresentGUIObject.name = "RiftPresentGUIMain";
        RiftPresentGUIObject.transform.parent = GameObject.Find("LeftEyeAnchor").transform;

        RectTransform r = RiftPresentGUIObject.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(100f, 100f);
        r.localPosition = new Vector3(0.01f, 0.17f, 0.53f);
        r.localEulerAngles = Vector3.zero;
        r.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        Canvas c = RiftPresentGUIObject.AddComponent<Canvas>();
#if UNITY_5_0
        // TODO: Unity 5.0b11 has an older version of the new GUI being developed in Unity 4.6.
        // Remove this once Unity 5 has a more recent merge of Unity 4.6.
        c.renderMode = RenderMode.WorldSpace;
#else
		c.renderMode = RenderMode.WorldSpace;
#endif
        c.pixelPerfect = false;
        OVRUGUI.RiftPresentGUI(RiftPresentGUIObject);
#endif
    }
    #endregion
}

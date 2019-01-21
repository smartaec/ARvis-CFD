using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tango;
using TangoCFD;
using UnityEngine.Rendering;

/// <summary>
/// GUI manager that merge the UI and functions of ARGUIController of TangoSDK and CfdVrVtkLoader
/// </summary>
public class ArGuiManager :MonoBehaviour, ITangoLifecycle, ITangoDepth
{
    // Constant value for controlling the position and size of debug overlay.
    public const float UI_LABEL_START_X = 15.0f;
    public const float UI_LABEL_START_Y = 15.0f;
    public const float UI_LABEL_SIZE_X = 1920.0f;
    public const float UI_LABEL_SIZE_Y = 35.0f;
    public const float UI_LABEL_GAP_X = 5.0f;
    public const float UI_LABEL_GAP_Y = 3.0f;
    public const float UI_BUTTON_SIZE_X = 210.0f;
    public const float UI_BUTTON_SIZE_Y = 90.0f;
    public const float UI_TOGGLE_SIZE_X = 210.0f;
    public const float UI_TOGGLE_SIZE_Y = 35.0f;
    public const float UI_BUTTON_GAP_X = 5.0f;
    public const float UI_BUTTON_GAP_Y = 3.0f;
    public const float UI_CAMERA_BUTTON_OFFSET = UI_BUTTON_SIZE_X + UI_BUTTON_GAP_X;
    public const float UI_LABEL_OFFSET = UI_LABEL_GAP_Y + UI_LABEL_SIZE_Y;
    public const float UI_FPS_LABEL_START_Y = UI_LABEL_START_Y + UI_LABEL_OFFSET;
    public const float UI_EVENT_LABEL_START_Y = UI_FPS_LABEL_START_Y + UI_LABEL_OFFSET;
    public const float UI_POSE_LABEL_START_Y = UI_EVENT_LABEL_START_Y + UI_LABEL_OFFSET;
    public const float UI_DEPTH_LABLE_START_Y = UI_POSE_LABEL_START_Y + UI_LABEL_OFFSET;
    public const string UI_FLOAT_FORMAT = "F3";
    public const string UI_FONT_SIZE = "<size=20>";

    public const float UI_TANGO_VERSION_X = UI_LABEL_START_X;
    public const float UI_TANGO_VERSION_Y = UI_LABEL_START_Y;
    public const float UI_TANGO_APP_SPECIFIC_START_X = UI_TANGO_VERSION_X;
    public const float UI_TANGO_APP_SPECIFIC_START_Y = UI_TANGO_VERSION_Y + (UI_LABEL_OFFSET * 2);

    public const string UX_SERVICE_VERSION = "Service version: {0}";
    public const string UX_TANGO_SERVICE_VERSION = "Tango service version: {0}";
    public const string UX_TANGO_SYSTEM_EVENT = "Tango system event: {0}";
    public const string UX_TARGET_TO_BASE_FRAME = "Target->{0}, Base->{1}:";
    public const string UX_STATUS = "\tstatus: {0}, count: {1}, position (m): [{2}], orientation: [{3}]";
    public const float SECOND_TO_MILLISECOND = 1000.0f;

    /// <summary>
    /// The marker prefab to place on taps.
    /// </summary>
    public GameObject m_prefabMarker;

    /// <summary>
    /// The touch effect to place on taps.
    /// </summary>
    public RectTransform m_prefabTouchEffect;

    /// <summary>
    /// The canvas to place 2D game objects under.
    /// </summary>
    public Canvas m_canvas;

    /// <summary>
    /// The point cloud object in the scene.
    /// </summary>
    public TangoPointCloud m_pointCloud;

    #region Cfd Config

    enum FileLoadMode
    {
        CFD,
        Scan,
        BIM,
        Tube,
    }
    public Texture2D fileIcon,folderIcon,backIcon,driveIcon;
    public GUISkin[] skins;
    public Material defaultMaterial;
    public Material vertexColorMaterial;
    public Shader shader;

    private FileBrowser _fileBrowser;
    private bool _errorCalcXform=false,_xformCalced = false,showMarker=true,_posingSlice=false,_dataFromServer=false,_loadSliceByTouch=false,_uiElementTriggered=false;
    private List<Vector3> _markerPositions=new List<Vector3>();
    private List<string> _svrFileList=new List<string>();

    private FileLoadMode _loadMode = FileLoadMode.CFD;
    private ICfdSceneManager _cfdManager;
    #endregion

    private const float FPS_UPDATE_FREQUENCY = 1.0f;
    private string m_fpsText;
    private int m_currentFPS;
    private int m_framesSinceUpdate;
    private float m_accumulation;
    private float m_currentTime;

    private TangoApplication m_tangoApplication;
    private TangoARPoseController m_tangoPose;
    private string m_tangoServiceVersion;
    private ARCameraPostProcess m_arCameraPostProcess;

    /// <summary>
    /// If set, then the depth camera is on and we are waiting for the next depth update.
    /// </summary>
    private bool m_findPlaneWaitingForDepth;

    /// <summary>
    /// If set, this is the selected marker.
    /// </summary>
    private ARMarker m_selectedMarker;

    /// <summary>
    /// If set, this is the rectangle bounding the selected marker.
    /// </summary>
    private Rect m_selectedRect;

    /// <summary>
    /// If set, this is the rectangle for the Hide All button.
    /// </summary>
    private Rect m_hideAllRect;

    /// <summary>
    /// If set, show debug text.
    /// </summary>
    private bool m_showDebug = false;

    /// <summary>
    /// Unity Start() callback, we set up some initial values here.
    /// </summary>
    public void Start()
    {
        m_currentFPS=0;
        m_framesSinceUpdate=0;
        m_currentTime=0.0f;
        m_fpsText="FPS = Calculating";
        m_tangoApplication=FindObjectOfType<TangoApplication>();
        m_tangoPose=FindObjectOfType<TangoARPoseController>();
        m_arCameraPostProcess=FindObjectOfType<ARCameraPostProcess>();
        m_tangoServiceVersion=TangoApplication.GetTangoServiceVersion();

        m_tangoApplication.Register(this);

        #region Cfd
        _cfdManager=new CfdSceneManagerV2();
        _cfdManager.DefaultMaterial=defaultMaterial;
        _cfdManager.VertexColorMaterial=vertexColorMaterial;
        _cfdManager.CfdShader=shader;
        _cfdManager.Start();
        #endregion
    }

    /// <summary>
    /// Unity destroy function.
    /// </summary>
    public void OnDestroy()
    {
        m_tangoApplication.Unregister(this);
    }

    /// <summary>
    /// Updates UI and handles player input.
    /// </summary>
    public void Update()
    {
        m_currentTime+=Time.deltaTime;
        ++m_framesSinceUpdate;
        m_accumulation+=Time.timeScale/Time.deltaTime;
        if(m_currentTime>=FPS_UPDATE_FREQUENCY) {
            m_currentFPS=(int)(m_accumulation/m_framesSinceUpdate);
            m_currentTime=0.0f;
            m_framesSinceUpdate=0;
            m_accumulation=0.0f;
            m_fpsText="FPS: "+m_currentFPS;
        }

        //_UpdateLocationMarker();
        #region Cfd
        if(_markerPositions.Count<4) {
            _UpdateLocationMarker();
        }
        if(_cfdManager.BaseFileLoaded&&_posingSlice&&!_uiElementTriggered) {
            UpdateSliceLocation();
        }
        #endregion

        if(Input.GetKey(KeyCode.Escape)) {
            Application.Quit();
        }
    }

    /// <summary>
    /// Display simple GUI.
    /// </summary>
    public void OnGUI()
    {
        Rect distortionButtonRec = new Rect(UI_BUTTON_GAP_X,
                                            Screen.height-UI_BUTTON_SIZE_Y-UI_BUTTON_GAP_X,
                                            UI_BUTTON_SIZE_X,
                                            UI_BUTTON_SIZE_Y);
        string isOn = m_arCameraPostProcess.enabled ? "Off" : "On";
        if(GUI.Button(distortionButtonRec,
                       UI_FONT_SIZE+"Turn Distortion "+isOn+"</size>")) {
            m_arCameraPostProcess.enabled=!m_arCameraPostProcess.enabled;
        }

        if(m_showDebug&&m_tangoApplication.HasRequestedPermissions()) {
            Color oldColor = GUI.color;
            GUI.color=Color.white;

            GUI.color=Color.black;
            GUI.Label(new Rect(UI_LABEL_START_X,
                               UI_LABEL_START_Y,
                               UI_LABEL_SIZE_X,
                               UI_LABEL_SIZE_Y),
                      UI_FONT_SIZE+String.Format(UX_TANGO_SERVICE_VERSION, m_tangoServiceVersion)+"</size>");

            GUI.Label(new Rect(UI_LABEL_START_X,
                               UI_FPS_LABEL_START_Y,
                               UI_LABEL_SIZE_X,
                               UI_LABEL_SIZE_Y),
                      UI_FONT_SIZE+m_fpsText+"</size>");

            // MOTION TRACKING
            GUI.Label(new Rect(UI_LABEL_START_X,
                               UI_POSE_LABEL_START_Y-UI_LABEL_OFFSET,
                               UI_LABEL_SIZE_X,
                               UI_LABEL_SIZE_Y),
                      UI_FONT_SIZE+String.Format(UX_TARGET_TO_BASE_FRAME, "Device", "Start")+"</size>");

            Vector3 pos = m_tangoPose.transform.position;
            Quaternion quat = m_tangoPose.transform.rotation;
            string positionString = pos.x.ToString(UI_FLOAT_FORMAT)+", "+
                pos.y.ToString(UI_FLOAT_FORMAT)+", "+
                    pos.z.ToString(UI_FLOAT_FORMAT);
            string rotationString = quat.x.ToString(UI_FLOAT_FORMAT)+", "+
                quat.y.ToString(UI_FLOAT_FORMAT)+", "+
                    quat.z.ToString(UI_FLOAT_FORMAT)+", "+
                    quat.w.ToString(UI_FLOAT_FORMAT);
            string statusString = String.Format(UX_STATUS,
                                                _GetLoggingStringFromPoseStatus(m_tangoPose.m_poseStatus),
                                                _GetLoggingStringFromFrameCount(m_tangoPose.m_poseCount),
                                                positionString, rotationString);
            GUI.Label(new Rect(UI_LABEL_START_X,
                               UI_POSE_LABEL_START_Y,
                               UI_LABEL_SIZE_X,
                               UI_LABEL_SIZE_Y),
                      UI_FONT_SIZE+statusString+"</size>");
            GUI.color=oldColor;
        }

        if(m_selectedMarker!=null) {
            Renderer selectedRenderer = m_selectedMarker.GetComponent<Renderer>();

            // GUI's Y is flipped from the mouse's Y
            Rect screenRect = WorldBoundsToScreen(Camera.main, selectedRenderer.bounds);
            float yMin = Screen.height-screenRect.yMin;
            float yMax = Screen.height-screenRect.yMax;
            screenRect.yMin=Mathf.Min(yMin, yMax);
            screenRect.yMax=Mathf.Max(yMin, yMax);

            if(_fileBrowser==null&&GUI.Button(screenRect, "<size=20>Delete</size>")) {
                m_selectedMarker.SendMessage("Hide");
                m_selectedMarker=null;
                m_selectedRect=new Rect();
            } else {
                m_selectedRect=screenRect;
            }
        } else {
            m_selectedRect=new Rect();
        }

        if(GameObject.FindObjectOfType<ARMarker>()!=null) {
            m_hideAllRect=new Rect(Screen.width-UI_BUTTON_SIZE_X*0.75f-UI_BUTTON_GAP_X,
                                     Screen.height-UI_BUTTON_SIZE_Y-UI_BUTTON_GAP_X,
                                     UI_BUTTON_SIZE_X*0.75f,
                                     UI_BUTTON_SIZE_Y);
            if(_fileBrowser==null&&GUI.Button(m_hideAllRect, "<size=20>Reset</size>")) {
                foreach(ARMarker marker in GameObject.FindObjectsOfType<ARMarker>()) {
                    marker.SendMessage("Hide");
                }
                #region Cfd
                _uiElementTriggered=true;
                _markerPositions.Clear();
                _errorCalcXform=false;
                _xformCalced=false;
                showMarker=true;
                return;
                #endregion
            }
            #region Cfd

            if(_markerPositions.Count>=4) {
                var rect = new Rect(Screen.width-UI_BUTTON_SIZE_X*1.5f-UI_BUTTON_GAP_X,
                                                    Screen.height-UI_BUTTON_SIZE_Y-UI_BUTTON_GAP_X,
                                                    UI_BUTTON_SIZE_X*0.75f,
                                                    UI_BUTTON_SIZE_Y);
                var txt = showMarker ? "Hide Marker" : "Show Marker";
                if(GUI.Button(rect, "<size=20>"+txt+"</size>")) {
                    _uiElementTriggered=true;
                    showMarker=!showMarker;
                    foreach(ARMarker marker in GameObject.FindObjectsOfType<ARMarker>()) {
                        marker.GetComponent<Renderer>().enabled=showMarker;
                    }
                    return;
                }
            }

            #endregion
        } else {
            m_hideAllRect=new Rect(0, 0, 0, 0);
        }

        #region Cfd
        var oColor = GUI.color;

        GUI.color=Color.black;
        var msg = "Add 4 markers before loading and positioning CFD data.";
        if((_markerPositions.Count>=4||!Application.isMobilePlatform)&&_errorCalcXform) {
            msg="Error encountered, please clear all markers and try again.";
        }
        GUI.Label(new Rect(UI_LABEL_GAP_X,
                           UI_LABEL_GAP_Y,
                           UI_LABEL_SIZE_X,
                           UI_LABEL_SIZE_Y),
                  UI_FONT_SIZE+msg+"</size>");
        GUI.color=oColor;

        if(_fileBrowser==null&&(_markerPositions.Count>=4||!Application.isMobilePlatform)) {
            Rect toggleRect = new Rect(Screen.width-UI_BUTTON_GAP_X-UI_BUTTON_SIZE_X,
                                                      UI_BUTTON_GAP_Y,
                                                      UI_TOGGLE_SIZE_X,
                                                      UI_TOGGLE_SIZE_Y);
            float vmove = UI_TOGGLE_SIZE_Y+UI_BUTTON_GAP_Y;
            //data source toggle
            _dataFromServer=GUI.Toggle(toggleRect, _dataFromServer, "<size=20>Load Server Data</size>");
            if(_dataFromServer) {
                toggleRect=new Rect(Screen.width-UI_BUTTON_GAP_X-UI_BUTTON_SIZE_X,
                                                      vmove+UI_BUTTON_GAP_Y,
                                                      UI_TOGGLE_SIZE_X,
                                                      UI_TOGGLE_SIZE_Y);
                _loadSliceByTouch=GUI.Toggle(toggleRect, _loadSliceByTouch, "<size=20>Load slice by touch</size>");
                vmove+=UI_TOGGLE_SIZE_Y+UI_BUTTON_GAP_Y;
            }
            int it = 0;
            Rect buttonRect = new Rect(Screen.width-UI_BUTTON_GAP_X-UI_BUTTON_SIZE_X,
                                                       vmove+((UI_BUTTON_GAP_Y+UI_BUTTON_SIZE_Y)*it),
                                                       UI_BUTTON_SIZE_X,
                                                       UI_BUTTON_SIZE_Y);
            //load file button
            if(GUI.Button(buttonRect, "<size=20>Load BIM/Scan</size>")) {
                _uiElementTriggered=true;
                _loadMode=FileLoadMode.Scan;
                OnLoadFile();
                return;
            }
            if(_cfdManager.BaseFileLoaded) {
                it++;
                buttonRect=new Rect(Screen.width-UI_BUTTON_GAP_X-UI_BUTTON_SIZE_X,
                    vmove+UI_BUTTON_GAP_Y+((UI_BUTTON_GAP_Y+UI_BUTTON_SIZE_Y)*it),
                    UI_BUTTON_SIZE_X,
                    UI_BUTTON_SIZE_Y);
                string show = _cfdManager.ScannedActive ? "Hide" : "Show";
                if(GUI.Button(buttonRect, "<size=20>"+show+" BIM/Scan</size>")) {
                    _uiElementTriggered=true;
                    _cfdManager.SetScannedActive(!_cfdManager.ScannedActive);
                    return;
                }

                it++;
                buttonRect=new Rect(Screen.width-UI_BUTTON_GAP_X-UI_BUTTON_SIZE_X,
                    vmove+UI_BUTTON_GAP_Y+((UI_BUTTON_GAP_Y+UI_BUTTON_SIZE_Y)*it),
                    UI_BUTTON_SIZE_X,
                    UI_BUTTON_SIZE_Y);
                //load file button
                if(GUI.Button(buttonRect, "<size=20>Load CFD Slice</size>")) {
                    _uiElementTriggered=true;
                    _loadMode=FileLoadMode.CFD;
                    if(_loadSliceByTouch) {
                        _posingSlice=true;
                    } else {
                        OnLoadFile();
                    }
                    return;
                }

                it++;
                buttonRect=new Rect(Screen.width-UI_BUTTON_GAP_X-UI_BUTTON_SIZE_X,
                    vmove+UI_BUTTON_GAP_Y+((UI_BUTTON_GAP_Y+UI_BUTTON_SIZE_Y)*it),
                    UI_BUTTON_SIZE_X,
                    UI_BUTTON_SIZE_Y);
                //load file button
                if(GUI.Button(buttonRect, "<size=20>Load Tube</size>")) {
                    _uiElementTriggered=true;
                    _loadMode=FileLoadMode.Tube;
                    OnLoadFile();
                    return;
                }
            }

            if(_cfdManager.TimeStepCount>1) {
                it++;
                buttonRect=new Rect(Screen.width-UI_BUTTON_GAP_X-UI_BUTTON_SIZE_X,
                                                       vmove+UI_BUTTON_GAP_Y+((UI_BUTTON_GAP_Y+UI_BUTTON_SIZE_Y)*it),
                                                       UI_BUTTON_SIZE_X,
                                                       UI_BUTTON_SIZE_Y);
                //step switch button
                if(GUI.Button(buttonRect, "<size=20>Clear Slices/Tubes</size>")) {
                    _uiElementTriggered=true;
                    OnClearSliceTube();
                    return;
                }

                it++;
                buttonRect=new Rect(Screen.width-UI_BUTTON_GAP_X-UI_BUTTON_SIZE_X,
                                                       vmove+UI_BUTTON_GAP_Y+((UI_BUTTON_GAP_Y+UI_BUTTON_SIZE_Y)*it),
                                                       UI_BUTTON_SIZE_X,
                                                       UI_BUTTON_SIZE_Y);
                //step switch button
                if(GUI.Button(buttonRect, "<size=20>Next Step</size>")) {
                    _uiElementTriggered=true;
                    OnNextStep();
                    return;
                }

                foreach(var cfdAttribute in _cfdManager.CfdAttributes) {
                    it++;
                    buttonRect=new Rect(Screen.width-UI_BUTTON_GAP_X-UI_BUTTON_SIZE_X,
                                                           vmove+UI_BUTTON_GAP_Y+((UI_BUTTON_GAP_Y+UI_BUTTON_SIZE_Y)*it),
                                                           UI_BUTTON_SIZE_X,
                                                           UI_BUTTON_SIZE_Y);
                    //step switch button
                    if(GUI.Button(buttonRect, "<size=20>"+cfdAttribute+"</size>")) {
                        _uiElementTriggered=true;
                        OnSwitchToAttrib(cfdAttribute);
                        return;
                    }
                }
            }
        }

        //draw and display output
        if(_fileBrowser!=null&&_fileBrowser.draw()) { //true is returned when a file has been selected
                                                      //the output file is a member if the FileInfo class, if cancel was selected the value is null
            if(_fileBrowser.outputFile!=null) {
                _uiElementTriggered=true;

                var output = _fileBrowser.outputFile;
                _fileBrowser=null;
                bool fromSvr = false;
                if(_svrFileList.Count>0) {
                    _svrFileList.Clear();
                    fromSvr=true;
                }
                switch(_loadMode) {
                    case FileLoadMode.Scan:
                        if(fromSvr) {
                            _cfdManager.LoadDataFromServer("scan/"+output.Name, output.Name, p => _cfdManager.LoadScanned(p));
                        } else {
                            _cfdManager.LoadScanned(output.FullName);
                        }
                        break;
                    case FileLoadMode.CFD:
                        if(fromSvr) {
                            _cfdManager.LoadDataFromServer("slices/"+output.Name, output.Name,
                                p => _cfdManager.LoadCfd(p));
                        } else {
                            _cfdManager.LoadCfd(output.FullName);
                        }
                        _cfdManager.ResetTimeStep();
                        break;
                    case FileLoadMode.Tube:
                        if(fromSvr) {
                            _cfdManager.LoadDataFromServer("tubes/"+output.Name, output.Name,
                                p => _cfdManager.LoadTube(p));
                        } else {
                            _cfdManager.LoadTube(output.FullName);
                        }
                        _cfdManager.ResetTimeStep();
                        break;
                }
            }
            _fileBrowser=null;
            return;
        }

        _uiElementTriggered=false;
        #endregion
    }

    #region Cfd

    void OnLoadFile()
    {
        if(_dataFromServer) {
            switch(_loadMode) {
                case FileLoadMode.Scan:
                    _svrFileList.AddRange(_cfdManager.ListServerFiles("scan"));
                    break;
                case FileLoadMode.CFD:
                    _svrFileList.AddRange(_cfdManager.ListServerFiles("slices"));
                    break;
                case FileLoadMode.Tube:
                    _svrFileList.AddRange(_cfdManager.ListServerFiles("tubes"));
                    break;
            }
        }

        _fileBrowser=new FileBrowser(1);
        var dir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        if(!Directory.Exists(dir)) {
            dir=System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        }
        if(!Directory.Exists(dir)) {
            dir=Directory.GetCurrentDirectory();
        }

        //setup file browser style
        _fileBrowser.guiSkin=skins[0]; //set the starting skin
                                       //set the various textures
        _fileBrowser.fileTexture=fileIcon;
        _fileBrowser.directoryTexture=folderIcon;
        _fileBrowser.backTexture=backIcon;
        _fileBrowser.driveTexture=driveIcon;

        _fileBrowser.selectedColor=Color.yellow;

        if(_svrFileList.Count>0) {
            _fileBrowser.useServerList=true;
            _fileBrowser.files=_svrFileList.Select(f => new FileInformation(new FileInfo(f))).ToArray();
        } else {
            _fileBrowser.setDirectory(dir);
            _fileBrowser.searchPattern="*.c4a";
        }
    }

    void OnNextStep()
    {
        _cfdManager.NextTimeStep();
    }

    void OnClearSliceTube()
    {
        _cfdManager.ClearSliceTube();
    }

    void OnSwitchToAttrib(string attrib)
    {
        _cfdManager.SwitchToAttrib(attrib);
    }

    void CalculateTransform()
    {
        if(_xformCalced) { return; }//do not need this actually, cause this will be called once when we have 4 markers
        var center = 0.5f*(_markerPositions[3]+_markerPositions[0]);
        var dimZ = _markerPositions[1]-_markerPositions[0];
        var dimY = _markerPositions[2]-_markerPositions[1];
        var dimX = _markerPositions[3]-_markerPositions[2];
        if(dimZ.magnitude==0||dimY.magnitude==0||dimX.magnitude==0) {
            _errorCalcXform=true;
            return;
        }

        var scale = Vector3.one;
        scale.x=scale.y=scale.z=dimZ.magnitude;

        dimZ.Normalize();
        dimY.Normalize();
        _cfdManager.UpdateTransform(center, scale, Quaternion.LookRotation(dimZ, dimY));
        _xformCalced=true;
    }

    void UpdateSliceLocation()
    {
        Vector3? pos = null;
        if(Input.touchCount==1) {
            // Single tap -- place new location or select existing location.
            Touch t = Input.GetTouch(0);
            pos=t.position;

            if(t.phase!=TouchPhase.Began) {
                return;
            }
        } else if(Input.GetMouseButtonDown(0)) {
            pos=Input.mousePosition;
        }

        if(pos.HasValue) {
            Vector2 guiPosition = new Vector2(pos.Value.x, Screen.height-pos.Value.y);
            var ray = Camera.main.ScreenPointToRay(guiPosition);

            _posingSlice=!_cfdManager.LoadCfdIfHitted(ray);
        }
    }
    #endregion

    /// <summary>
    /// This is called when the permission granting process is finished.
    /// </summary>
    /// <param name="permissionsGranted"><c>true</c> if permissions were granted, otherwise <c>false</c>.</param>
    public void OnTangoPermissions(bool permissionsGranted)
    {
    }

    /// <summary>
    /// This is called when successfully connected to the Tango service.
    /// </summary>
    public void OnTangoServiceConnected()
    {
        m_tangoApplication.SetDepthCameraRate(TangoEnums.TangoDepthCameraRate.DISABLED);
    }

    /// <summary>
    /// This is called when disconnected from the Tango service.
    /// </summary>
    public void OnTangoServiceDisconnected()
    {
    }

    /// <summary>
    /// This is called each time new depth data is available.
    /// 
    /// On the Tango tablet, the depth callback occurs at 5 Hz.
    /// </summary>
    /// <param name="tangoDepth">Tango depth.</param>
    public void OnTangoDepthAvailable(TangoUnityDepth tangoDepth)
    {
        // Don't handle depth here because the PointCloud may not have been updated yet.  Just
        // tell the coroutine it can continue.
        m_findPlaneWaitingForDepth=false;
    }

    /// <summary>
    /// Convert a 3D bounding box into a 2D rectangle.
    /// </summary>
    /// <returns>The 2D rectangle in Screen coordinates.</returns>
    /// <param name="cam">Camera to use.</param>
    /// <param name="bounds">3D bounding box.</param>
    private Rect WorldBoundsToScreen(Camera cam, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Bounds screenBounds = new Bounds(cam.WorldToScreenPoint(center), Vector3.zero);

        screenBounds.Encapsulate(cam.WorldToScreenPoint(center+new Vector3(+extents.x, +extents.y, +extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center+new Vector3(+extents.x, +extents.y, -extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center+new Vector3(+extents.x, -extents.y, +extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center+new Vector3(+extents.x, -extents.y, -extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center+new Vector3(-extents.x, +extents.y, +extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center+new Vector3(-extents.x, +extents.y, -extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center+new Vector3(-extents.x, -extents.y, +extents.z)));
        screenBounds.Encapsulate(cam.WorldToScreenPoint(center+new Vector3(-extents.x, -extents.y, -extents.z)));
        return Rect.MinMaxRect(screenBounds.min.x, screenBounds.min.y, screenBounds.max.x, screenBounds.max.y);
    }

    /// <summary>
    /// Construct readable string from TangoPoseStatusType.
    /// </summary>
    /// <param name="status">Pose status from Tango.</param>
    /// <returns>Readable string corresponding to status.</returns>
    private string _GetLoggingStringFromPoseStatus(TangoEnums.TangoPoseStatusType status)
    {
        string statusString;
        switch(status) {
            case TangoEnums.TangoPoseStatusType.TANGO_POSE_INITIALIZING:
                statusString="initializing";
                break;
            case TangoEnums.TangoPoseStatusType.TANGO_POSE_INVALID:
                statusString="invalid";
                break;
            case TangoEnums.TangoPoseStatusType.TANGO_POSE_UNKNOWN:
                statusString="unknown";
                break;
            case TangoEnums.TangoPoseStatusType.TANGO_POSE_VALID:
                statusString="valid";
                break;
            default:
                statusString="N/A";
                break;
        }

        return statusString;
    }

    /// <summary>
    /// Reformat string from vector3 type for data logging.
    /// </summary>
    /// <param name="vec">Position to display.</param>
    /// <returns>Readable string corresponding to <c>vec</c>.</returns>
    private string _GetLoggingStringFromVec3(Vector3 vec)
    {
        if(vec==Vector3.zero) {
            return "N/A";
        } else {
            return string.Format("{0}, {1}, {2}",
                                 vec.x.ToString(UI_FLOAT_FORMAT),
                                 vec.y.ToString(UI_FLOAT_FORMAT),
                                 vec.z.ToString(UI_FLOAT_FORMAT));
        }
    }

    /// <summary>
    /// Reformat string from quaternion type for data logging.
    /// </summary>
    /// <param name="quat">Quaternion to display.</param>
    /// <returns>Readable string corresponding to <c>quat</c>.</returns>
    private string _GetLoggingStringFromQuaternion(Quaternion quat)
    {
        if(quat==Quaternion.identity) {
            return "N/A";
        } else {
            return string.Format("{0}, {1}, {2}, {3}",
                                 quat.x.ToString(UI_FLOAT_FORMAT),
                                 quat.y.ToString(UI_FLOAT_FORMAT),
                                 quat.z.ToString(UI_FLOAT_FORMAT),
                                 quat.w.ToString(UI_FLOAT_FORMAT));
        }
    }

    /// <summary>
    /// Return a string to the get logging from frame count.
    /// </summary>
    /// <returns>The get logging string from frame count.</returns>
    /// <param name="frameCount">Frame count.</param>
    private string _GetLoggingStringFromFrameCount(int frameCount)
    {
        if(frameCount==-1.0) {
            return "N/A";
        } else {
            return frameCount.ToString();
        }
    }

    /// <summary>
    /// Return a string to get logging of FrameDeltaTime.
    /// </summary>
    /// <returns>The logging string for frame delta time.</returns>
    /// <param name="frameDeltaTime">Frame delta time.</param>
    private string _GetLoggingStringFromFrameDeltaTime(float frameDeltaTime)
    {
        if(frameDeltaTime==-1.0) {
            return "N/A";
        } else {
            return (frameDeltaTime*SECOND_TO_MILLISECOND).ToString(UI_FLOAT_FORMAT);
        }
    }

    /// <summary>
    /// Update location marker state.
    /// </summary>
    private void _UpdateLocationMarker()
    {
        if(Input.touchCount==1) {
            // Single tap -- place new location or select existing location.
            Touch t = Input.GetTouch(0);
            Vector2 guiPosition = new Vector2(t.position.x, Screen.height-t.position.y);
            Camera cam = Camera.main;
            RaycastHit hitInfo;

            if(t.phase!=TouchPhase.Began) {
                return;
            }

            if(m_selectedRect.Contains(guiPosition)||m_hideAllRect.Contains(guiPosition)) {
                // do nothing, the button will handle it
            } else if(Physics.Raycast(cam.ScreenPointToRay(t.position), out hitInfo)) {
                // Found a marker, select it (so long as it isn't disappearing)!
                GameObject tapped = hitInfo.collider.gameObject;
                if(!tapped.GetComponent<Animation>().isPlaying) {
                    m_selectedMarker=tapped.GetComponent<ARMarker>();
                }
            } else {
                // Place a new point at that location, clear selection
                m_selectedMarker=null;
                StartCoroutine(_WaitForDepthAndFindPlane(t.position));

                // Because we may wait a small amount of time, this is a good place to play a small
                // animation so the user knows that their input was received.
                RectTransform touchEffectRectTransform = (RectTransform)Instantiate(m_prefabTouchEffect);
                touchEffectRectTransform.transform.SetParent(m_canvas.transform, false);
                Vector2 normalizedPosition = t.position;
                normalizedPosition.x/=Screen.width;
                normalizedPosition.y/=Screen.height;
                touchEffectRectTransform.anchorMin=touchEffectRectTransform.anchorMax=normalizedPosition;
            }
        }

        if(Input.touchCount==2) {
            // Two taps -- toggle debug text
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            if(t0.phase!=TouchPhase.Began&&t1.phase!=TouchPhase.Began) {
                return;
            }

            m_showDebug=!m_showDebug;
            return;
        }

        if(Input.touchCount!=1) {
            return;
        }
    }

    /// <summary>
    /// Wait for the next depth update, then find the plane at the touch position.
    /// </summary>
    /// <returns>Coroutine IEnumerator.</returns>
    /// <param name="touchPosition">Touch position to find a plane at.</param>
    private IEnumerator _WaitForDepthAndFindPlane(Vector2 touchPosition)
    {
        m_findPlaneWaitingForDepth=true;

        // Turn on the camera and wait for a single depth update.
        m_tangoApplication.SetDepthCameraRate(TangoEnums.TangoDepthCameraRate.MAXIMUM);
        while(m_findPlaneWaitingForDepth) {
            yield return null;
        }

        m_tangoApplication.SetDepthCameraRate(TangoEnums.TangoDepthCameraRate.DISABLED);

        // Find the plane.
        Camera cam = Camera.main;
        Vector3 planeCenter;
        Plane plane;
        if(!m_pointCloud.FindPlane(cam, touchPosition, out planeCenter, out plane)) {
            yield break;
        }

        // Ensure the location is always facing the camera.  This is like a LookRotation, but for the Y axis.
        Vector3 up = plane.normal;
        Vector3 forward;
        if(Vector3.Angle(plane.normal, cam.transform.forward)<175) {
            Vector3 right = Vector3.Cross(up, cam.transform.forward).normalized;
            forward=Vector3.Cross(right, up).normalized;
        } else {
            // Normal is nearly parallel to camera look direction, the cross product would have too much
            // floating point error in it.
            forward=Vector3.Cross(up, cam.transform.right);
        }

        #region Cfd
        _markerPositions.Add(planeCenter);
        if(_markerPositions.Count>=4) { CalculateTransform(); }
        #endregion
        Instantiate(m_prefabMarker, planeCenter, Quaternion.LookRotation(forward, up));
        m_selectedMarker=null;
    }
}

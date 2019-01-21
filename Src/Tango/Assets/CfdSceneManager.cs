using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace TangoCFD
{
    public interface ICfdSceneManager
    {
        /// <summary>
        /// whether the BIM or point cloud was loaded
        /// </summary>
        bool BaseFileLoaded { get; }
        /// <summary>
        /// material for visualize BIM or point cloud
        /// </summary>
        Material DefaultMaterial { get; set; }
        /// <summary>
        /// material for visualization of slices or tubes created from CFD raw data
        /// </summary>
        Material VertexColorMaterial { get; set; }
        /// <summary>
        /// if <see cref="VertexColorMaterial"/> is null, just use this shader for visualizzation of slices or tubes
        /// </summary>
        Shader CfdShader { get; set; }
        /// <summary>
        /// different attributes defined in CFD data, like pressure, velocity,etc. not implemented now
        /// </summary>
        List<string> CfdAttributes { get; }
        /// <summary>
        /// total number of time steps
        /// </summary>
        int TimeStepCount { get; }
        /// <summary>
        /// is BIM or point cloud visible or not
        /// </summary>
        bool ScannedActive { get; }
        /// <summary>
        /// for data initialization, will be called in Start() of <see cref="ArGuiManager"/>
        /// </summary>
        void Start();
        /// <summary>
        /// switch to next time step
        /// </summary>
        void NextTimeStep();
        /// <summary>
        /// clear all loaded data for slices and tubes
        /// </summary>
        void ClearSliceTube();
        /// <summary>
        /// swith to provided attrib, not implemented now
        /// </summary>
        /// <param name="attrib"></param>
        void SwitchToAttrib(string attrib);
        /// <summary>
        /// update transformation of all virtual data
        /// </summary>
        /// <param name="position"></param>
        /// <param name="scale"></param>
        /// <param name="rotation"></param>
        void UpdateTransform(Vector3 position, Vector3 scale, Quaternion rotation);
        /// <summary>
        /// load CFD slices
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        bool LoadCfd(string path);
        /// <summary>
        /// load CFD slices when the ray instersects with the bounding box of the scene
        /// </summary>
        /// <param name="ray"></param>
        /// <returns></returns>
        bool LoadCfdIfHitted(Ray ray);
        /// <summary>
        /// load tubes
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        bool LoadTube(string path);
        /// <summary>
        /// load BIM or point cloud
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        bool LoadScanned(string path);
        /// <summary>
        /// reset current time step to zero, and update scene
        /// </summary>
        void ResetTimeStep();
        /// <summary>
        /// set active property of BIM or point cloud, control the visibility of the data
        /// </summary>
        /// <param name="active"></param>
        void SetScannedActive(bool active);

        /// <summary>
        /// load data from the server side
        /// </summary>
        /// <param name="url">relative route</param>
        /// <param name="cachename">file name of cache data</param>
        /// <param name="callback">callback when the file is downloaded from the server or found in cached files</param>
        /// <param name="compressData">get compressed data from server side</param>
        void LoadDataFromServer(string url, string cachename, Action<string> callback, bool compressData = true);
        /// <summary>
        /// get all files at the server
        /// </summary>
        /// <param name="url">relative route</param>
        /// <returns></returns>
        List<string> ListServerFiles(string url);
    }
    public class CfdSceneManagerV2 :ICfdSceneManager
    {
        class Timestep
        {
            public int Step;
            public List<GameObject> Objects=new List<GameObject>();
            public List<Color[]> Attribs=new List<Color[]>();
        }
        public bool BaseFileLoaded { get { return _basefielLoaded; } }
        public string HostUrl="http://cfd.bimer.cn";
        public float MaxHitDistance = 100f;
        public Material DefaultMaterial { get; set; }
        public Material VertexColorMaterial { get; set; }
        public Shader CfdShader { get; set; }
        public List<string> CfdAttributes { get; set; }
        public int TimeStepCount { get { return _stepsDic.Count==0 ? 0 : _stepsDic.Values.Max(i => i.Count); } }
        public bool ScannedActive { get { return _scannedActive; } }

        private bool _scannedActive = true,_basefielLoaded=false;
        Dictionary<string,List<Timestep>> _stepsDic=new Dictionary<string, List<Timestep>>();
        private int _curStep = 0;
        private List<GameObject> _scanSubs=new List<GameObject>();
        private GameObject _cfdObject, _cfdSubTemplate, _cfdBoundWireframe,_cfdBoundSurface;
        private BoxCollider _boxCollider;
        private Plane[] _boxPlanes=null;
        public void Start()
        {
            CfdAttributes=new List<string>();
            _cfdObject=new GameObject("Cfd Object");
            //Add Components
            _cfdObject.AddComponent<MeshFilter>();
            _cfdObject.AddComponent<MeshRenderer>();

            var renderer = _cfdObject.GetComponent<MeshRenderer>();
            renderer.enabled=true;//default is true
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            if(CfdShader!=null) {
                var mat = new Material(CfdShader);
                renderer.material=mat;
                renderer.materials[0]=mat;
            } else if(DefaultMaterial!=null) {
                renderer.material=DefaultMaterial;
                renderer.materials[0]=DefaultMaterial;
            } else {
                var sh = Shader.Find("Material");
                if(sh!=null) {
                    var mat = new Material(sh);
                    renderer.material=mat;
                    renderer.materials[0]=mat;
                }
            }

            _cfdSubTemplate=GameObject.Instantiate(_cfdObject);
            _cfdSubTemplate.name="CFD Submesh Template";

            _cfdBoundWireframe=GameObject.Instantiate(_cfdObject);
            _cfdBoundWireframe.name="CFD Bound Wireframe";
            _cfdBoundWireframe.transform.SetParent(_cfdObject.transform, false);

            _cfdBoundSurface=new GameObject("CFD Bound Surface");
            _boxCollider=_cfdBoundSurface.AddComponent<BoxCollider>();
            var body = _cfdBoundSurface.AddComponent<Rigidbody>();
            body.isKinematic=true;
            _cfdBoundSurface.AddComponent<MeshFilter>();
            _cfdBoundSurface.transform.SetParent(_cfdObject.transform, false);
            _boxCollider.transform.SetParent(_cfdObject.transform, false);

            _cfdObject.transform.localPosition=Vector3.forward*0.5f;
        }

        public void ResetTimeStep()
        {
            _curStep=-1;
            NextTimeStep();
        }
        public void NextTimeStep()
        {
            _curStep++;
            var large = _stepsDic.Values.Max(i => i.Count);
            _curStep%=large;

            foreach(var kv in _stepsDic) {
                var steps = kv.Value;
                if(steps.Count>1) {
                    var stepIdx = _curStep%steps.Count;
                    for(int i = 0, len = steps.Count; i<len; i++) {
                        if(i!=stepIdx) {
                            foreach(var o in steps[i].Objects) {
                                o.SetActive(false);
                            }
                        }
                    }
                    var step = steps[stepIdx];
                    for(int i = 0, len = step.Objects.Count; i<len; i++) {
                        var o = step.Objects[i];
                        o.SetActive(true);
                        o.GetComponent<MeshFilter>().mesh.colors=step.Attribs[i];
                    }
                }
            }
        }

        public void ClearSliceTube()
        {
            foreach(var kv in _stepsDic) {
                var steps = kv.Value;
                if(steps.Count>0) {
                    foreach(var timestep in steps) {
                        foreach(var o in timestep.Objects) {
                            UnityEngine.Object.Destroy(o);
                        }
                    }
                    steps.Clear();
                }
            }

            _stepsDic.Clear();
        }

        public void SetScannedActive(bool active)
        {
            foreach(var scanSub in _scanSubs) {
                scanSub.SetActive(active);
            }
            _scannedActive=active;
        }
        public void SwitchToAttrib(string attrib)
        {
            //TODO
        }

        public void UpdateTransform(Vector3 position, Vector3 scale, Quaternion rotation)
        {
            _cfdObject.transform.localPosition=position;
            _cfdObject.transform.localScale=scale;
            _cfdObject.transform.localRotation=rotation;
        }

        public bool LoadCfdIfHitted(Ray ray)
        {
            if(_boxPlanes!=null) {
                var min = Vector3.Scale(baseMin, scaleToParent);
                var max = Vector3.Scale(baseMax, scaleToParent);

                var trans = _cfdObject.transform;
                var ry = new Ray(trans.InverseTransformPoint(ray.origin), trans.InverseTransformVector(ray.direction));
                float enter;
                if(_boxPlanes[0].Raycast(ry, out enter)||_boxPlanes[1].Raycast(ry, out enter)) {
                    var pos = ry.origin+enter*ry.direction;
                    var distZ = pos.z-min.z/(max.z-min.z);

                    if(!float.IsInfinity(distZ)&&distZ>0&&distZ<1) {
                        Debug.Log(string.Format("hit at ({0},{1},{2}) with normal in X direction.", pos.x, pos.y, pos.z));
                        //scale distZ to [-100,100]
                        var dz = (int)((distZ-0.5f)*200);
                        dz-=dz%10;
                        Debug.Log(string.Format("try load slice data at y{0}.", dz));
                        try {
                            LoadDataFromServer("slices/y"+dz, "y"+dz, f => {
                                if(LoadCfd(f)) {
                                    ResetTimeStep();
                                }
                            });
                            return true;
                        } catch(Exception ex) {
                            Debug.LogError(ex.Message);
                        }

                    }

                }

                if(_boxPlanes[4].Raycast(ry, out enter)||_boxPlanes[5].Raycast(ry, out enter)) {
                    var pos = ry.origin+enter*ry.direction;
                    var distX = pos.x-min.x/(max.x-min.x);

                    if(!float.IsInfinity(distX)&&distX>0&&distX<1) {
                        Debug.Log(string.Format("hit at ({0},{1},{2}) with normal in Z direction.", pos.x, pos.y, pos.z));
                        //scale distX to [-100,100]
                        var dx = (int)((distX-0.5f)*200);
                        dx-=dx%10;
                        Debug.Log(string.Format("try load slice data at x{0}.", dx));
                        try {
                            LoadDataFromServer("slices/x"+dx, "x"+dx, f => {
                                if(LoadCfd(f)) {
                                    ResetTimeStep();
                                }
                            });
                            return true;
                        } catch(Exception ex) {
                            Debug.LogError(ex.Message);
                        }

                    }
                }
            }
            return false;
        }
        public bool LoadCfd(string path)
        {
            PerfTimer.Start(string.Format("cfd-->{0}", Path.GetFileName(path)));

            var cfdSteps = new List<Timestep>();

            using(var reader = new BinaryReader(new FileStream(path, FileMode.Open), Encoding.ASCII)) {
                var version = reader.ReadInt32();
                var name = reader.ReadString();
                reader.BaseStream.Position+=sizeof(float)*6;//skip min and max corner

                var totalTimeStepCount = reader.ReadInt32();
                var submeshCount = reader.ReadInt32();
                var submeshStart = reader.ReadInt32();

                var attribName = "veloc";
                var attribCount = reader.ReadInt32();
                int attribStart = -1;
                for(int i = 0; i<attribCount; i++) {
                    var str = reader.ReadString();
                    if(str.Contains(attribName)) {
                        reader.ReadInt32();//attrib type
                        attribStart=reader.ReadInt32();
                        break;
                    } else {
                        reader.BaseStream.Position+=2*sizeof(int);
                    }
                }


                var legendColors = new Color[2]
                {
                    Color.red, Color.green
                };
                List<int> relatedSteps = new List<int>();
                var objects = LoadSubmeshes(reader, submeshStart, submeshCount, baseCenter, scaleToParent, _cfdSubTemplate, _cfdObject, relatedSteps, doubleSide: true);
                var attribs = LoadAttributes(reader, attribStart, totalTimeStepCount, submeshCount, legendColors);

                ConstructTimeSteps(cfdSteps, totalTimeStepCount, objects, relatedSteps, attribs);
                _stepsDic[Path.GetFileName(path)]=cfdSteps;
            }

            PerfTimer.Stop();
            return true;
        }

        public bool LoadTube(string path)
        {
            PerfTimer.Start(string.Format("tube-->{0}",Path.GetFileName(path)));

            var tubSteps = new List<Timestep>();

            using(var reader = new BinaryReader(new FileStream(path, FileMode.Open), Encoding.ASCII)) {
                var version = reader.ReadInt32();
                var name = reader.ReadString();
                reader.BaseStream.Position+=sizeof(float)*6;//skip min and max corner

                var totalTimeStepCount = reader.ReadInt32();
                var submeshCount = reader.ReadInt32();
                var submeshStart = reader.ReadInt32();

                var attribName = "veloc";
                var attribCount = reader.ReadInt32();
                int attribStart = -1;
                for(int i = 0; i<attribCount; i++) {
                    var str = reader.ReadString();
                    if(str.Contains(attribName)) {
                        reader.ReadInt32();//attrib type
                        attribStart=reader.ReadInt32();
                        break;
                    } else {
                        reader.BaseStream.Position+=2*sizeof(int);
                    }
                }


                var legendColors = new Color[2]
                {
                    new Color(1, 1, 0), Color.blue
                };
                List<int> relatedSteps = new List<int>();
                var objects = LoadSubmeshes(reader, submeshStart, submeshCount, baseCenter, scaleToParent, _cfdSubTemplate, _cfdObject, relatedSteps, calcNormals: true);
                var attribs = LoadAttributes(reader, attribStart, totalTimeStepCount, submeshCount, legendColors);
                foreach(var tubeSub in objects) {
                    if(VertexColorMaterial!=null) {
                        tubeSub.GetComponent<MeshRenderer>().material=VertexColorMaterial;
                    } else if(DefaultMaterial!=null) {
                        tubeSub.GetComponent<MeshRenderer>().material=DefaultMaterial;
                    }
                }

                ConstructTimeSteps(tubSteps, totalTimeStepCount, objects, relatedSteps, attribs);
                _stepsDic[Path.GetFileName(path)]=tubSteps;
            }

            PerfTimer.Stop();
            return true;
        }

        internal Vector3 baseMin,baseMax, baseCenter,scaleToParent=Vector3.one;
        public bool LoadScanned(string path)
        {
            if(_scanSubs.Count>0) {
                foreach(var sub in _scanSubs) {
                    UnityEngine.Object.Destroy(sub);
                }
                _scanSubs.Clear();
            }

            PerfTimer.Start(string.Format("scan-->{0}", Path.GetFileName(path)));

            using (var reader = new BinaryReader(new FileStream(path, FileMode.Open), Encoding.ASCII)) {
                var version = reader.ReadInt32();
                var name = reader.ReadString();

                baseMin.x=reader.ReadSingle();
                baseMin.z=reader.ReadSingle();
                baseMin.y=reader.ReadSingle();
                baseMax.x=reader.ReadSingle();
                baseMax.z=reader.ReadSingle();
                baseMax.y=reader.ReadSingle();
                baseCenter=0.5f*(baseMin+baseMax);

                var diag = baseMax-baseMin;
                if(diag.z!=0) {
                    scaleToParent.x=1f/diag.z;
                    scaleToParent.y=1f/diag.z;
                    scaleToParent.z=1f/diag.z;
                } else if(diag.y!=0) {
                    scaleToParent.x=1f/diag.y;
                    scaleToParent.y=1f/diag.y;
                    scaleToParent.z=1f/diag.y;
                } else if(diag.x!=0) {
                    scaleToParent.x=1f/diag.x;
                    scaleToParent.y=1f/diag.x;
                    scaleToParent.z=1f/diag.x;
                } else {
                    throw new Exception("invalid bounding box");
                }

                baseMin-=baseCenter;
                baseMax-=baseCenter;
                var min = Vector3.Scale(baseMin, scaleToParent);
                var max = Vector3.Scale(baseMax, scaleToParent);

                Debug.Log(string.Format("bouding box size: <{0},{1},{2}>", diag.x, diag.y, diag.z));
                Debug.Log(string.Format("bouding box center: <{0},{1},{2}>", baseCenter.x, baseCenter.y, baseCenter.z));

                #region bound
                var bwmesh = new Mesh();
                _cfdBoundWireframe.GetComponent<MeshFilter>().mesh=bwmesh;
                var corners = new Vector3[8];

                corners[0]=new Vector3(min.x, min.y, min.z);
                corners[1]=new Vector3(max.x, min.y, min.z);
                corners[2]=new Vector3(max.x, max.y, min.z);
                corners[3]=new Vector3(min.x, max.y, min.z);

                corners[4]=new Vector3(min.x, min.y, max.z);
                corners[5]=new Vector3(max.x, min.y, max.z);
                corners[6]=new Vector3(max.x, max.y, max.z);
                corners[7]=new Vector3(min.x, max.y, max.z);
                bwmesh.vertices=corners;
                var bwidx = new int[] {
                0,1,1,2,2,3,3,0,
                4,5,5,6,6,7,7,4,
                0,4,1,5,2,6,3,7
            };
                bwmesh.SetIndices(bwidx, MeshTopology.Lines, 0);

                var bsmesh = new Mesh();
                _cfdBoundSurface.GetComponent<MeshFilter>().mesh=bsmesh;
                bsmesh.vertices=corners;
                var bsidx = new int[]
                {
                    3,2,1,0,
                    0,1,5,4,
                    1,2,6,5,
                    2,3,7,6,
                    3,0,4,7,
                    4,5,6,7,
                };
                bsmesh.SetIndices(bsidx.Reverse().ToArray(), MeshTopology.Quads, 0);
                _boxCollider.center=baseCenter;
                _boxCollider.size=max-min;

                _boxPlanes=new Plane[6];
                _boxPlanes[0]=new Plane(new Vector3(1, 0, 0), min);
                _boxPlanes[1]=new Plane(new Vector3(-1, 0, 0), max);
                _boxPlanes[2]=new Plane(new Vector3(0, 1, 0), min);
                _boxPlanes[3]=new Plane(new Vector3(0, -1, 0), max);
                _boxPlanes[4]=new Plane(new Vector3(0, 0, 1), min);
                _boxPlanes[5]=new Plane(new Vector3(0, 0, -1), max);
                #endregion


                var totalTimeStepCount = reader.ReadInt32();
                var submeshCount = reader.ReadInt32();
                var submeshStart = reader.ReadInt32();

                var objects = LoadSubmeshes(reader, submeshStart, submeshCount, baseCenter, scaleToParent, _cfdSubTemplate, _cfdObject, calcNormals: true);

                foreach(var gameObject in objects) {
                    if(DefaultMaterial!=null) {
                        gameObject.GetComponent<MeshRenderer>().material=DefaultMaterial;
                    }

                    _scanSubs.Add(gameObject);
                }

                _basefielLoaded=true;
            }

            PerfTimer.Stop();

            return true;
        }

        private static List<GameObject> LoadSubmeshes(BinaryReader reader, int submeshStart, int submeshCount,
            Vector3 moveCenterTo, Vector3 scaleToParent,
            GameObject objTemplate, GameObject parentObject, List<int> relatedSteps = null, bool calcNormals = false, bool doubleSide = false)
        {
            var objects = new List<GameObject>();
            reader.BaseStream.Seek(submeshStart, SeekOrigin.Begin);
            for(int sb = 0; sb<submeshCount; sb++) {
                Mesh mesh = new Mesh();
                var subName = reader.ReadString();
                var timestepIndex = reader.ReadInt32();
                mesh.name=subName;
                Vector3 smin, smax;
                smin.x=reader.ReadSingle();
                smin.z=reader.ReadSingle();
                smin.y=reader.ReadSingle();
                smax.x=reader.ReadSingle();
                smax.z=reader.ReadSingle();
                smax.y=reader.ReadSingle();

                var count = reader.ReadInt32();
                Vector3[] vertices = new Vector3[count];
                for(int i = 0; i<count; i++) {
                    Vector3 v;
                    v.x=reader.ReadSingle();
                    v.z=reader.ReadSingle();
                    v.y=reader.ReadSingle();
                    vertices[i]=v;
                }

                for(int i = 0; i<count; i++) {
                    vertices[i]-=moveCenterTo;
                    vertices[i]=Vector3.Scale(vertices[i], scaleToParent);
                }

                mesh.vertices=vertices;
                var nCount = reader.ReadInt32();
                if(nCount>0) {
                    Debug.LogError(string.Format("count of normals({0}) should be equal to count of vertices({1}).", nCount, count));
                    var normals = new Vector3[nCount];
                    for(int i = 0; i<nCount; i++) {
                        var n = new Vector3();
                        n.x=reader.ReadSingle();
                        n.z=reader.ReadSingle();
                        n.y=reader.ReadSingle();
                        normals[i]=n;
                    }
                    mesh.normals=normals;
                }
                var tCount = reader.ReadInt32();
                if(tCount>0) {
                    reader.BaseStream.Position+=tCount*4*sizeof(float);
                }
                var cCount = reader.ReadInt32();
                if(cCount>0) {
                    reader.BaseStream.Position+=cCount*4*sizeof(float);
                }
                var indexType = reader.ReadInt32();
                var iCount = reader.ReadInt32();
                if(iCount>0) {
                    int[] indices = new int[iCount];
                    for(int i = 0; i<iCount; i++) {
                        indices[i]=reader.ReadInt32();
                    }

                    if(indexType==0) {
                        mesh.SetIndices(indices, MeshTopology.Points, 0);
                    } else {
                        if(doubleSide) {
                            indices=indices.Concat(indices.Reverse()).ToArray();
                        } else {
                            indices=indices.Reverse().ToArray();
                        }
                        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
                        if(calcNormals) {
                            mesh.RecalculateNormals();
                        }
                    }
                }

                var sub = GameObject.Instantiate(objTemplate);
                sub.name=mesh.name+"_ts"+timestepIndex;
                sub.transform.SetParent(parentObject.transform, false);
                sub.GetComponent<MeshFilter>().mesh=mesh;

                objects.Add(sub);
                if(relatedSteps!=null) {
                    relatedSteps.Add(timestepIndex);
                }
            }
            return objects;
        }

        private static Dictionary<int, List<Color[]>> LoadAttributes(BinaryReader reader, int attribStart, int totalTimestepCount, int totalSubmeshCount,
            Color[] legendColors)
        {
            Dictionary<int, List<Color[]>> attribs = new Dictionary<int, List<Color[]>>();
            reader.BaseStream.Seek(attribStart, SeekOrigin.Begin);
            for(int sk = 0; sk<totalSubmeshCount; sk++) {
                var submeshIndex = reader.ReadInt32();
                var timestepCount = reader.ReadInt32();
                var attribItemCount = reader.ReadInt32();
                var compCount = reader.ReadInt32();
                var dtType = reader.ReadInt32();//0 is float, 1 is int
                attribs[submeshIndex]=new List<Color[]>();

                List<float>[] series = new List<float>[timestepCount];
                float maxLen = float.MinValue, minLen = float.MaxValue;
                for(int j = 0; j<timestepCount; j++) {
                    List<float> vLens = new List<float>();
                    for(int k = 0; k<attribItemCount; k++) {
                        float[] comps = new float[compCount];
                        var sqr_sum = 0f;
                        for(int l = 0; l<compCount; l++) {
                            comps[l]=reader.ReadSingle();
                            sqr_sum+=comps[l]*comps[l];
                        }
                        var vlen = Mathf.Sqrt(sqr_sum);
                        vLens.Add(vlen);
                        minLen=Math.Min(minLen, vlen);
                        maxLen=Math.Max(maxLen, vlen);
                    }
                    series[j]=vLens;

                }
                for(int j = 0; j<timestepCount; j++) {
                    var s = series[j];
                    var len = s.Count;
                    var colors = new Color[len];
                    for(int k = 0; k<len; k++) {
                        colors[k]=Color.Lerp(legendColors[0], legendColors[1], (s[k]-minLen)/(maxLen-minLen));
                    }
                    attribs[submeshIndex].Add(colors);
                }

            }

            return attribs;
        }

        private static void ConstructTimeSteps(List<Timestep> steps, int totalTimestepCount, List<GameObject> objects, List<int> relatedSteps, Dictionary<int, List<Color[]>> attribs)
        {
            for(int i = 0; i<totalTimestepCount; i++) {
                steps.Add(new Timestep() { Step=i });
            }
            for(int i = 0, len = objects.Count; i<len; i++) {
                var tsIdx = relatedSteps[i];
                var obj = objects[i];
                var atts = attribs[i];
                for(int j = 0, c = atts.Count; j<c; j++) {
                    var timeStep = steps[tsIdx+j];
                    timeStep.Objects.Add(obj);
                    timeStep.Attribs.Add(atts[j]);
                }
            }
        }

        public void LoadDataFromServer(string url, string filename, Action<string> callback, bool compressData = true)
        {
            var root = Application.persistentDataPath.TrimEnd(Path.DirectorySeparatorChar);

            var fullpath = root+Path.DirectorySeparatorChar+filename+(compressData ? ".zipc4a" : ".c4a");
            if(!File.Exists(fullpath)) {
                var baseUrl = HostUrl;
                if(!Application.isMobilePlatform) {
                    baseUrl="http://localhost:1234";
                }

                HttpWebRequest wr = new HttpWebRequest(new Uri(baseUrl+"/"+url+(compressData ? "?compress=true" : "")));
                wr.Method="GET";
                wr.ContentLength=0;
                wr.ContentType="text/html";
                using(var stream = wr.GetResponse().GetResponseStream()) {
                    using(var save = new FileStream(fullpath, FileMode.Create)) {
                        byte[] buffer = new byte[1024];
                        while(true) {
                            int n = stream.Read(buffer, 0, buffer.Length);
                            if(n==0) {
                                break;
                            }
                            save.Write(buffer, 0, n);
                        }
                        save.Flush();
                    }
                }
            }

            var path = root+Path.DirectorySeparatorChar+filename+".c4a";
            //decompress data
            if(File.Exists(fullpath)&&compressData&&!File.Exists(path)) {
                using(var save = new FileStream(path, FileMode.Create)) {
                    using(var stream = new GZipStream(new FileStream(fullpath, FileMode.Open), CompressionMode.Decompress)) {
                        byte[] buffer = new byte[1024];
                        while(true) {
                            int n = stream.Read(buffer, 0, buffer.Length);
                            if(n==0) {
                                break;
                            }
                            save.Write(buffer, 0, n);
                        }
                        save.Flush();
                    }
                }
            }

            if(File.Exists(path)&&callback!=null) {
                callback(path);
            }

        }

        public List<string> ListServerFiles(string url)
        {
            var baseUrl = HostUrl;
            if(!Application.isMobilePlatform) {
                baseUrl="http://localhost:1234";
            }

            HttpWebRequest wr = new HttpWebRequest(new Uri(baseUrl+"/"+url));
            wr.Method="GET";
            wr.ContentLength=0;
            wr.ContentType="text/html";
            using(var stream = wr.GetResponse().GetResponseStream()) {
                using(var reader = new StreamReader(stream)) {
                    var data = reader.ReadToEnd().Trim();
                    return data.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
            }
        }

    }

    internal static class PerfTimer
    {
        private static Stopwatch watch=new Stopwatch();
        private static string file="";

        public static void Start(string msg)
        {
            file = msg;
            watch.Start();
        }

        public static void Stop()
        {
            watch.Stop();
            var time = watch.ElapsedMilliseconds;
            UnityEngine.Debug.Log(string.Format("msg:{0}>>>time:{1}ms",file,time));
            watch.Reset();
        }
    }
}

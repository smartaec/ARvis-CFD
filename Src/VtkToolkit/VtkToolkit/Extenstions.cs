using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace VtkToolkit
{
    public class Submesh
    {
        public string Name { get; set; }
        public int TimeStep { get; set; }
        public Vector3 Min;
        public Vector3 Max;
        public List<Vector3> Vertices { get; set; }
        public List<Vector3> Normals { get; set; }
        public List<Vector4> Texcoords { get; set; }
        public List<Vector4> Colors { get; set; }
        /// <summary>
        /// type of the index, should be 0, or 1, 0 is points, 1 is triangles
        /// </summary>
        public int IndexType { get; set; }
        public List<int> Indices { get; set; }

        /// <summary>
        /// vertex attributes, key-name of the attribute, value-time series of the attributes
        /// </summary>
        public Dictionary<string, List<List<float[]>>> ScalarAttribs { get; set; }
        /// <summary>
        /// vertex attributes, key-name of the attribute, value-time series of the attributes
        /// </summary>
        public Dictionary<string, List<List<float[]>>> VectorAttribs { get; set; }

        public Submesh()
        {
            IndexType=0;
            TimeStep=0;
            Vertices=new List<Vector3>();
            Normals=new List<Vector3>();
            Texcoords=new List<Vector4>();
            Colors=new List<Vector4>();
            Indices=new List<int>();
            ScalarAttribs=new Dictionary<string, List<List<float[]>>>();
            VectorAttribs=new Dictionary<string, List<List<float[]>>>();
        }
    }

    public class SimpleCfdMesh
    {
        /// <summary>
        /// version, currently must be 1 or 2
        /// </summary>
        public int Version { get; set; }
        /// <summary>
        /// name, description, or sth like that
        /// </summary>
        public string Name { get; set; }
        public Vector3 Min;
        public Vector3 Max;
        public int TimeStepCount { get; set; }
        /// <summary>
        /// submeshes
        /// </summary>
        public Dictionary<int, Submesh> Submeshes { get; set; }
        public SimpleCfdMesh()
        {
            Version=2;
            Name="default";
            TimeStepCount=1;
            Submeshes=new Dictionary<int, Submesh>();
        }
        public bool Save(string path, int version = 2)
        {
            //using(var writer = new BinaryWriter(new GZipStream(new FileStream(path, FileMode.Create), CompressionMode.Compress), Encoding.ASCII)) {
            using(var writer = new BinaryWriter(new FileStream(path, FileMode.Create), Encoding.ASCII)) {
                Version=version;
                switch(Version) {
                    case 1:
                        SaveV1(writer);
                        break;
                    case 2:
                        SaveV2(writer);
                        break;
                    default:
                        throw new NotImplementedException("unsupport file format...");
                        break;
                }
            }

            return true;
        }

        private void SaveV1(BinaryWriter writer)
        { //version
            writer.Write(1);
            writer.Write(Name);
            //min point
            writer.Write(Min.X);
            writer.Write(Min.Y);
            writer.Write(Min.Z);
            //max piont
            writer.Write(Max.X);
            writer.Write(Max.Y);
            writer.Write(Max.Z);

            writer.Write(Submeshes.Count);
            foreach(var smPair in Submeshes) {
                var sm = smPair.Value;
                writer.Write(sm.Name);
                //min point
                writer.Write(sm.Min.X);
                writer.Write(sm.Min.Y);
                writer.Write(sm.Min.Z);
                //max piont
                writer.Write(sm.Max.X);
                writer.Write(sm.Max.Y);
                writer.Write(sm.Max.Z);

                int vCount = sm.Vertices.Count, nCount = sm.Normals.Count, tCount = sm.Texcoords.Count, cCount = sm.Colors.Count;
                //vertices
                writer.Write(vCount);
                for(int i = 0; i<vCount; i++) {
                    var v = sm.Vertices[i];
                    writer.Write(v.X);
                    writer.Write(v.Y);
                    writer.Write(v.Z);
                }
                //normals
                writer.Write(nCount);
                for(int i = 0; i<nCount; i++) {
                    var n = sm.Normals[i];
                    writer.Write(n.X);
                    writer.Write(n.Y);
                    writer.Write(n.Z);
                }
                //tex coords
                writer.Write(tCount);
                for(int i = 0; i<tCount; i++) {
                    var t = sm.Texcoords[i];
                    writer.Write(t.X);
                    writer.Write(t.Y);
                    writer.Write(t.Z);
                    writer.Write(t.W);
                }
                //colors
                writer.Write(cCount);
                for(int i = 0; i<cCount; i++) {
                    var c = sm.Colors[i];
                    writer.Write(c.X);
                    writer.Write(c.Y);
                    writer.Write(c.Z);
                    writer.Write(c.W);
                }

                //index type
                writer.Write(sm.IndexType);

                //indices
                writer.Write(sm.Indices.Count);
                for(int i = 0, len = sm.Indices.Count; i<len; i++) {
                    Debug.Assert(sm.Indices[i]<vCount);
                    writer.Write(sm.Indices[i]);
                }

                //scalars
                writer.Write(sm.ScalarAttribs.Count);
                foreach(var kv in sm.ScalarAttribs) {
                    writer.Write(kv.Key);//attribute name
                    writer.Write(kv.Value==null ? 0 : kv.Value.Count);//count of time series
                    if(kv.Value!=null) {
                        foreach(var ps in kv.Value) {
                            writer.Write(ps==null ? 0 : ps.Count);//item count of each series
                            writer.Write(ps==null||ps.Count==0 ? 0 : ps[0].Length);//component count of each item
                            foreach(var i in ps) {
                                foreach(var i1 in i) {
                                    writer.Write(i1);
                                }
                            }
                        }
                    }
                }

                //vectors
                writer.Write(sm.VectorAttribs.Count);
                foreach(var kv in sm.VectorAttribs) {
                    writer.Write(kv.Key);//attribute name
                    writer.Write(kv.Value==null ? 0 : kv.Value.Count);//count of time series
                    if(kv.Value!=null) {
                        foreach(var ps in kv.Value) {
                            writer.Write(ps==null ? 0 : ps.Count);//item count of each series
                            writer.Write(ps==null||ps.Count==0 ? 0 : ps[0].Length);//component count of each item
                            foreach(var i in ps) {
                                foreach(var i1 in i) {
                                    writer.Write(i1);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void SaveV2(BinaryWriter writer)
        {
            writer.Write(2);
            writer.Write(Name);
            //min point
            writer.Write(Min.X);
            writer.Write(Min.Y);
            writer.Write(Min.Z);
            //max piont
            writer.Write(Max.X);
            writer.Write(Max.Y);
            writer.Write(Max.Z);
            //time step count
            writer.Write(TimeStepCount);
            writer.Write(Submeshes.Count);
            var smStartPos = (int)writer.BaseStream.Position;
            writer.Write(-1);

            var fsb = Submeshes.Values.First();
            var attribCount = fsb.ScalarAttribs.Count+fsb.VectorAttribs.Count;
            writer.Write(attribCount);
            Dictionary<string, int> attribStartPos = new Dictionary<string, int>();
            foreach(var attrib in fsb.ScalarAttribs) {
                writer.Write(attrib.Key);
                writer.Write(0);//scalar is 0
                attribStartPos[attrib.Key]=(int)writer.BaseStream.Position;
                writer.Write(-1);
            }
            foreach(var attrib in fsb.VectorAttribs) {
                writer.Write(attrib.Key);
                writer.Write(0);//vector is 1
                attribStartPos[attrib.Key]=(int)writer.BaseStream.Position;
                writer.Write(-1);
            }

            int curpos = (int)writer.BaseStream.Position;
            writer.Seek(smStartPos, SeekOrigin.Begin);
            writer.Write(curpos);
            writer.Seek(curpos, SeekOrigin.Begin);
            foreach(var kv in Submeshes) {
                var sm = kv.Value;
                writer.Write(sm.Name);
                writer.Write(sm.TimeStep);
                //min point
                writer.Write(sm.Min.X);
                writer.Write(sm.Min.Y);
                writer.Write(sm.Min.Z);
                //max piont
                writer.Write(sm.Max.X);
                writer.Write(sm.Max.Y);
                writer.Write(sm.Max.Z);

                int vCount = sm.Vertices.Count, nCount = sm.Normals.Count, tCount = sm.Texcoords.Count, cCount = sm.Colors.Count;
                //vertices
                writer.Write(vCount);
                for(int i = 0; i<vCount; i++) {
                    var v = sm.Vertices[i];
                    writer.Write(v.X);
                    writer.Write(v.Y);
                    writer.Write(v.Z);
                }
                //normals
                writer.Write(nCount);
                for(int i = 0; i<nCount; i++) {
                    var n = sm.Normals[i];
                    writer.Write(n.X);
                    writer.Write(n.Y);
                    writer.Write(n.Z);
                }
                //tex coords
                writer.Write(tCount);
                for(int i = 0; i<tCount; i++) {
                    var t = sm.Texcoords[i];
                    writer.Write(t.X);
                    writer.Write(t.Y);
                    writer.Write(t.Z);
                    writer.Write(t.W);
                }
                //colors
                writer.Write(cCount);
                for(int i = 0; i<cCount; i++) {
                    var c = sm.Colors[i];
                    writer.Write(c.X);
                    writer.Write(c.Y);
                    writer.Write(c.Z);
                    writer.Write(c.W);
                }

                //index type
                writer.Write(sm.IndexType);

                //indices
                writer.Write(sm.Indices.Count);
                for(int i = 0, len = sm.Indices.Count; i<len; i++) {
                    Debug.Assert(sm.Indices[i]<vCount);
                    writer.Write(sm.Indices[i]);
                }
            }

            //scalars
            foreach(var attrib in fsb.ScalarAttribs) {
                curpos=(int)writer.BaseStream.Position;
                writer.Seek(attribStartPos[attrib.Key], SeekOrigin.Begin);
                writer.Write(curpos);
                writer.Seek(curpos, SeekOrigin.Begin);

                foreach(var kv in Submeshes) {
                    var sm = kv.Value;
                    writer.Write(kv.Key);//submesh index
                    var series = sm.ScalarAttribs[attrib.Key];
                    writer.Write(series.Count);//how many time steps are there for this submesh
                    writer.Write(series[0].Count);//item count of each series
                    writer.Write(series[0].Count==0 ? 0 : series[0][0].Length);//component count of each item
                    writer.Write((int)0);//data type: 0 is float, 1 is int
                    foreach(var ps in series) {
                        foreach(var i in ps) {
                            foreach(var i1 in i) {
                                writer.Write(i1);
                            }
                        }
                    }

                }
            }

            //vectors
            foreach(var attrib in fsb.VectorAttribs) {
                curpos=(int)writer.BaseStream.Position;
                writer.Seek(attribStartPos[attrib.Key], SeekOrigin.Begin);
                writer.Write(curpos);
                writer.Seek(curpos, SeekOrigin.Begin);

                foreach(var kv in Submeshes) {
                    var sm = kv.Value;
                    writer.Write(kv.Key);//submesh index
                    var series = sm.VectorAttribs[attrib.Key];
                    writer.Write(series.Count);//how many time steps are there for this submesh
                    writer.Write(series[0].Count);//item count of each series
                    writer.Write(series[0].Count==0 ? 0 : series[0][0].Length);//component count of each item
                    writer.Write((int)0);//data type: 0 is float, 1 is int
                    foreach(var ps in series) {
                        foreach(var i in ps) {
                            foreach(var i1 in i) {
                                writer.Write(i1);
                            }
                        }
                    }

                }
            }
//
//#if DEBUG
//            foreach(var kv in Submeshes) {
//                using(var sr = new StreamWriter(kv.Key+".obj")) {
//                    var val = kv.Value;
//                    foreach(var vec in val.Vertices) {
//                        sr.WriteLine(string.Format("v {0} {1} {2}", vec.X, vec.Y, vec.Z));
//                    }
//                    var normals = new Vector3[val.Vertices.Count];
//                    var indices = val.Indices;
//                    var vertices = val.Vertices;
//                    for(int i = 0, ic = indices.Count; i<ic; i+=3) {
//                        var idx0 = indices[i];
//                        var idx1 = indices[i+1];
//                        var idx2 = indices[i+2];
//                        var p0 = vertices[idx0];
//                        var p1 = vertices[idx1];
//                        var p2 = vertices[idx2];
//                        var normal = Vector3.Cross(p1-p0, p2-p0);
//                        normal=Vector3.Normalize(normal);
//                        normals[idx0]=normal;
//                        normals[idx1]=normal;
//                        normals[idx2]=normal;
//                    }
//                    foreach(var vec in normals) {
//                        sr.WriteLine(string.Format("vn {0} {1} {2}", vec.X, vec.Y, vec.Z));
//                    }
//                    for(int i = 0, len = val.Indices.Count; i<len; i+=3) {
//                        sr.WriteLine(string.Format("f {0}//{0} {1}//{1} {2}//{2}", val.Indices[i]+1, val.Indices[i+1]+1, val.Indices[i+2]+1));
//                    }
//                }
//            }
//#endif
        }

        public bool Load(string path)
        {
            if(!File.Exists(path)) {
                return false;
            }
            using(var reader = new BinaryReader(File.Open(path, FileMode.Open), Encoding.ASCII)) {
                //version
                Version=reader.ReadInt32();
                switch(Version) {
                    case 1:
                        LoadV1(reader);
                        break;
                    case 2:
                        LoadV2(reader);
                        break;
                    default:
                        throw new NotImplementedException("unsupport file version...");
                        break;
                }

            }
            return true;
        }

        private void LoadV1(BinaryReader reader)
        {
            //name
            Name=reader.ReadString();
            //min
            Min.X=reader.ReadSingle();
            Min.Y=reader.ReadSingle();
            Min.Z=reader.ReadSingle();
            //max
            Max.X=reader.ReadSingle();
            Max.Y=reader.ReadSingle();
            Max.Z=reader.ReadSingle();

            //submesh
            var smCount = reader.ReadInt32();
            for(int smi = 0; smi<smCount; smi++) {
                var sm = new Submesh();
                Submeshes[smi]=sm;
                sm.Name=reader.ReadString();
                //min
                sm.Min.X=reader.ReadSingle();
                sm.Min.Y=reader.ReadSingle();
                sm.Min.Z=reader.ReadSingle();
                //max
                sm.Max.X=reader.ReadSingle();
                sm.Max.Y=reader.ReadSingle();
                sm.Max.Z=reader.ReadSingle();
                //vertices
                var vCount = reader.ReadInt32();
                if(vCount>0) {
                    for(int i = 0; i<vCount; i++) {
                        var v = new Vector3();
                        v.X=reader.ReadSingle();
                        v.Y=reader.ReadSingle();
                        v.Z=reader.ReadSingle();
                        sm.Vertices.Add(v);
                    }
                }
                //normals
                var nCount = reader.ReadInt32();
                if(nCount>0) {
                    for(int i = 0; i<nCount; i++) {
                        var n = new Vector3();
                        n.X=reader.ReadSingle();
                        n.Y=reader.ReadSingle();
                        n.Z=reader.ReadSingle();
                        sm.Normals.Add(n);
                    }
                }
                //tex coords
                var tCount = reader.ReadInt32();
                if(tCount>0) {
                    for(int i = 0; i<tCount; i++) {
                        var t = new Vector4();
                        t.X=reader.ReadSingle();
                        t.Y=reader.ReadSingle();
                        t.Z=reader.ReadSingle();
                        t.W=reader.ReadSingle();
                        sm.Texcoords.Add(t);
                    }
                }
                //colors
                var cCount = reader.ReadInt32();
                if(cCount>0) {
                    for(int i = 0; i<cCount; i++) {
                        var t = new Vector4();
                        t.X=reader.ReadSingle();
                        t.Y=reader.ReadSingle();
                        t.Z=reader.ReadSingle();
                        t.W=reader.ReadSingle();
                        sm.Colors.Add(t);
                    }
                }

                //index type
                sm.IndexType=reader.ReadInt32();
                //indices
                var iCount = reader.ReadInt32();
                if(iCount>0) {
                    for(int i = 0; i<iCount; i++) {
                        sm.Indices.Add(reader.ReadInt32());
                    }
                }
                //scalars
                var rsCount = reader.ReadInt32();
                if(rsCount>0) {
                    for(int i = 0; i<rsCount; i++) {
                        var key = reader.ReadString();
                        var timeSeriesCount = reader.ReadInt32();
                        if(timeSeriesCount>0) {
                            var val = new List<List<float[]>>(timeSeriesCount);
                            for(int j = 0; j<timeSeriesCount; j++) {
                                var attribCount = reader.ReadInt32();
                                var attribCompCount = reader.ReadInt32();
                                if(attribCount>0&&attribCompCount>0) {
                                    var attribList = new List<float[]>(attribCount);

                                    for(int k = 0; k<attribCount; k++) {
                                        var comps = new float[attribCompCount];
                                        for(int l = 0; l<attribCompCount; l++) {
                                            comps[l]=reader.ReadSingle();
                                        }
                                    }
                                    val.Add(attribList);
                                }
                            }
                            sm.ScalarAttribs[key]=val;
                        }
                    }
                }
                //vectors
                var rvCount = reader.ReadInt32();
                if(rvCount>0) {
                    for(int i = 0; i<rvCount; i++) {
                        var key = reader.ReadString();
                        var timeSeriesCount = reader.ReadInt32();
                        if(timeSeriesCount>0) {
                            var val = new List<List<float[]>>(timeSeriesCount);
                            for(int j = 0; j<timeSeriesCount; j++) {
                                var attribCount = reader.ReadInt32();
                                var attribCompCount = reader.ReadInt32();
                                if(attribCount>0&&attribCompCount>0) {
                                    var attribList = new List<float[]>(attribCount);

                                    for(int k = 0; k<attribCount; k++) {
                                        var comps = new float[attribCompCount];
                                        for(int l = 0; l<attribCompCount; l++) {
                                            comps[l]=reader.ReadSingle();
                                        }
                                    }
                                    val.Add(attribList);
                                }
                            }
                            sm.VectorAttribs[key]=val;
                        }
                    }
                }
            }
        }

        private void LoadV2(BinaryReader reader) { }
    }
}

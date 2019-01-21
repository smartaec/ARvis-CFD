using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace VtkToolkit
{
    public class VtkTransfer
    {
        public bool Convert(List<string> fileList, string savePath, bool sliceMesh = false)
        {
            if(fileList.Count==0) {
                return false;
            }

            PerfTimer.Start("load");
            var vtkModels = fileList.Select(f => VtkLoader.Load(f)).ToList();
            PerfTimer.Stop();
            PerfTimer.Start("convert");
            var meshes = vtkModels.Select(v => VtkToCfdMesh(v)).ToArray();
            PerfTimer.Stop();

            PerfTimer.Start("merge");
            var mesh = CombineTimeSeriesSteps(meshes);
            if(sliceMesh) {
                SliceMesh(mesh, 60000);
            }
            PerfTimer.Stop();

            return mesh.Save(savePath);
        }

        public SimpleCfdMesh VtkToCfdMesh(VtkModel model)
        {
            var mesh = new SimpleCfdMesh();
            mesh.Name=model.Description;

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            var vCount = model.DataSet.Points.Count;
            var subMesh = new Submesh();
            subMesh.Name=mesh.Name;
            mesh.Submeshes[0]=subMesh;

            subMesh.Vertices.AddRange(model.DataSet.Points);
            for(int i = 0; i<vCount; i++) {
                var v = model.DataSet.Points[i];
                min=Vector3.Min(min, v);
                max=Vector3.Max(max, v);
            }
            mesh.Max=max;
            mesh.Min=min;

            subMesh.Max=max;
            subMesh.Min=min;

            #region Point Data
            if(model.PointDatas!=null&&model.PointDatas.Count>0) {
                foreach(var kv in model.PointDatas) {
                    var key = kv.Key;
                    var val = kv.Value;
                    if(val is Scalars) {
                        var cs = val as Scalars;
                        List<float[]> realScalar = new List<float[]>(cs.Values.Count);
                        for(int i = 0, len = cs.Values.Count; i<len; i++) {
                            realScalar.Add(cs.Values[i]);
                        }
                        if(!subMesh.ScalarAttribs.ContainsKey(key)) {
                            subMesh.ScalarAttribs[key]=new List<List<float[]>>();
                        }
                        subMesh.ScalarAttribs[key].Add(realScalar);
                    } else if(val is VectorsOrNormals) {
                        var vn = val as VectorsOrNormals;
                        List<float[]> realVector = new List<float[]>(vn.Values.Count);
                        for(int i = 0, len = vn.Values.Count; i<len; i++) {
                            var item = vn.Values[i];
                            realVector.Add(new float[] { item.X, item.Y, item.Z });
                        }
                        if(!subMesh.VectorAttribs.ContainsKey(key)) {
                            subMesh.VectorAttribs[key]=new List<List<float[]>>();
                        }
                        subMesh.VectorAttribs[key].Add(realVector);
                    } else if(val is TextureCoordinates) {
                        //TODO
                    } else if(val is FieldData) {
                        var fieldData = val as FieldData;
                        if(fieldData.Arrays!=null&&fieldData.Arrays.Count>0) {
                            foreach(var pair in fieldData.Arrays) {
                                var pk = pair.Key;
                                var pv = pair.Value;
                                //taken field data array as scalars
                                if(pv.Tuples!=null&&pv.Tuples.Count>0) {
                                    var scalars = new List<float[]>();
                                    for(int i = 0, len = pv.Tuples.Count; i<len; i++) {
                                        scalars.Add(pv.Tuples[i]);
                                    }
                                    var k = key+"_"+pk;
                                    if(!subMesh.ScalarAttribs.ContainsKey(k)) {
                                        subMesh.ScalarAttribs[k]=new List<List<float[]>>();
                                    }
                                    subMesh.ScalarAttribs[k].Add(scalars);
                                }
                            }
                        }
                    } else {
                        throw new NotImplementedException("not implemented now for "+val.GetType());
                    }
                }
            }
            #endregion

            #region PolyData
            if(model.DataSet is VtkPolyData) {
                var items = (model.DataSet as VtkPolyData).Items;
                var cellAttrib2Point = new Dictionary<int, int>();//key-vertex idx, val-cell idx
                foreach(var kv in items) {
                    switch(kv.Key) {
                        case "Polygons":
                        case "POLYGONS":
                            subMesh.IndexType=1;
                            var polygons = kv.Value;
                            for(int k = 0, kc = polygons.Indices.Count; k<kc; k++) {
                                var idxs = polygons.Indices[k];
                                cellAttrib2Point[idxs[0]]=k;
                                cellAttrib2Point[idxs[1]]=k;
                                for(int i = 1, idLen = idxs.Length-1; i<idLen; i++) {
                                    subMesh.Indices.Add(idxs[0]);
                                    subMesh.Indices.Add(idxs[i]);
                                    subMesh.Indices.Add(idxs[i+1]);

                                    cellAttrib2Point[idxs[i+1]]=k;
                                }
                            }
                            break;
                        case "TriangleStrips":
                        case "TRIANGLE_STRIPS":
                            subMesh.IndexType=1;
                            var triStrips = kv.Value;
                            for(int k = 0, kc = triStrips.Indices.Count; k<kc; k++) {
                                var idxs = triStrips.Indices[k];
                                if(idxs.Length<3) { continue; }
                                cellAttrib2Point[idxs[0]]=k;
                                cellAttrib2Point[idxs[1]]=k;
                                for(int i = 1, j = 0, end = idxs.Length-1; i<end; i++, j++) {
                                    if(j%2==0) {
                                        subMesh.Indices.Add(idxs[i-1]);
                                        subMesh.Indices.Add(idxs[i]);
                                        subMesh.Indices.Add(idxs[i+1]);
                                    } else {
                                        subMesh.Indices.Add(idxs[i-1]);
                                        subMesh.Indices.Add(idxs[i+1]);
                                        subMesh.Indices.Add(idxs[i]);
                                    }

                                    cellAttrib2Point[idxs[i+1]]=k;
                                }
                            }
                            break;
                        default:
                            throw new NotImplementedException("implement process for VERTICES, LINES, NORMALS, later...");
                            break;
                    }
                }

                //deal with cell data
                #region Cell Data
                if(model.CellDatas!=null&&model.CellDatas.Count>0) {
                    var vtxCount = subMesh.Vertices.Count;
                    foreach(var kv in model.CellDatas) {
                        var key = kv.Key;
                        var val = kv.Value;
                        if(val is Scalars) {
                            var cs = val as Scalars;
                            List<float[]> realScalar = new List<float[]>(vtxCount);
                            for(int i = 0; i<vtxCount; i++) {
                                realScalar.Add(cs.Values[cellAttrib2Point[i]]);
                            }
                            if(!subMesh.ScalarAttribs.ContainsKey(key)) {
                                subMesh.ScalarAttribs[key]=new List<List<float[]>>();
                            }
                            subMesh.ScalarAttribs[key].Add(realScalar);
                        } else if(val is VectorsOrNormals) {
                            var vn = val as VectorsOrNormals;
                            List<float[]> realVector = new List<float[]>(vtxCount);
                            for(int i = 0; i<vtxCount; i++) {
                                var item = vn.Values[cellAttrib2Point[i]];
                                realVector.Add(new float[] { item.X, item.Y, item.Z });
                            }
                            if(!subMesh.VectorAttribs.ContainsKey(key)) {
                                subMesh.VectorAttribs[key]=new List<List<float[]>>();
                            }
                            subMesh.VectorAttribs[key].Add(realVector);
                        } else if(val is FieldData) {
                            var fieldData = val as FieldData;
                            if(fieldData.Arrays!=null&&fieldData.Arrays.Count>0) {
                                foreach(var pair in fieldData.Arrays) {
                                    var pk = pair.Key;
                                    var pv = pair.Value;
                                    //taken field data array as scalars
                                    if(pv.Tuples!=null&&pv.Tuples.Count>0) {
                                        var scalars = new List<float[]>(vtxCount);
                                        for(int i = 0; i<vtxCount; i++) {
                                            scalars.Add(pv.Tuples[cellAttrib2Point[i]]);
                                        }
                                        var k = key+"_"+pk;
                                        if(!subMesh.ScalarAttribs.ContainsKey(k)) {
                                            subMesh.ScalarAttribs[k]=new List<List<float[]>>();
                                        }
                                        subMesh.ScalarAttribs[k].Add(scalars);
                                    }
                                }
                            }
                        } else {
                            throw new NotImplementedException("not implemented now for "+val.GetType());
                        }
                    }
                }
                #endregion
            }
            #endregion

            #region Cells
            if(model.DataSet.Cells!=null&&model.DataSet.Cells.Count>0) {
                subMesh.IndexType=1;
                var cellAttrib2Point = new Dictionary<int, int>();//key-vertex idx, val-cell idx

                for(int i = 0, cl = model.DataSet.Cells.Count; i<cl; i++) {
                    var cell = model.DataSet.Cells[i];
                    foreach(var index in cell.Indices) {
                        cellAttrib2Point[index]=i;
                    }

                    if(cell.Type==VtkCellType.Hexahedron) {
                        subMesh.Indices.Add(cell.Indices[0]);
                        subMesh.Indices.Add(cell.Indices[1]);
                        subMesh.Indices.Add(cell.Indices[2]);
                        subMesh.Indices.Add(cell.Indices[2]);
                        subMesh.Indices.Add(cell.Indices[3]);
                        subMesh.Indices.Add(cell.Indices[0]);

                        subMesh.Indices.Add(cell.Indices[0+4]);
                        subMesh.Indices.Add(cell.Indices[1+4]);
                        subMesh.Indices.Add(cell.Indices[2+4]);
                        subMesh.Indices.Add(cell.Indices[2+4]);
                        subMesh.Indices.Add(cell.Indices[3+4]);
                        subMesh.Indices.Add(cell.Indices[0+4]);

                        subMesh.Indices.Add(cell.Indices[0]);
                        subMesh.Indices.Add(cell.Indices[1]);
                        subMesh.Indices.Add(cell.Indices[5]);
                        subMesh.Indices.Add(cell.Indices[5]);
                        subMesh.Indices.Add(cell.Indices[4]);
                        subMesh.Indices.Add(cell.Indices[0]);

                        subMesh.Indices.Add(cell.Indices[1]);
                        subMesh.Indices.Add(cell.Indices[2]);
                        subMesh.Indices.Add(cell.Indices[6]);
                        subMesh.Indices.Add(cell.Indices[6]);
                        subMesh.Indices.Add(cell.Indices[5]);
                        subMesh.Indices.Add(cell.Indices[1]);

                        subMesh.Indices.Add(cell.Indices[2]);
                        subMesh.Indices.Add(cell.Indices[3]);
                        subMesh.Indices.Add(cell.Indices[7]);
                        subMesh.Indices.Add(cell.Indices[7]);
                        subMesh.Indices.Add(cell.Indices[6]);
                        subMesh.Indices.Add(cell.Indices[2]);

                        subMesh.Indices.Add(cell.Indices[3]);
                        subMesh.Indices.Add(cell.Indices[0]);
                        subMesh.Indices.Add(cell.Indices[4]);
                        subMesh.Indices.Add(cell.Indices[4]);
                        subMesh.Indices.Add(cell.Indices[7]);
                        subMesh.Indices.Add(cell.Indices[3]);
                    } else if(cell.Type==VtkCellType.Voxel) {
                        subMesh.Indices.Add(cell.Indices[0]);
                        subMesh.Indices.Add(cell.Indices[1]);
                        subMesh.Indices.Add(cell.Indices[3]);
                        subMesh.Indices.Add(cell.Indices[3]);
                        subMesh.Indices.Add(cell.Indices[2]);
                        subMesh.Indices.Add(cell.Indices[0]);

                        subMesh.Indices.Add(cell.Indices[0+4]);
                        subMesh.Indices.Add(cell.Indices[1+4]);
                        subMesh.Indices.Add(cell.Indices[3+4]);
                        subMesh.Indices.Add(cell.Indices[3+4]);
                        subMesh.Indices.Add(cell.Indices[2+4]);
                        subMesh.Indices.Add(cell.Indices[0+4]);

                        subMesh.Indices.Add(cell.Indices[0]);
                        subMesh.Indices.Add(cell.Indices[1]);
                        subMesh.Indices.Add(cell.Indices[5]);
                        subMesh.Indices.Add(cell.Indices[5]);
                        subMesh.Indices.Add(cell.Indices[4]);
                        subMesh.Indices.Add(cell.Indices[0]);

                        subMesh.Indices.Add(cell.Indices[1]);
                        subMesh.Indices.Add(cell.Indices[3]);
                        subMesh.Indices.Add(cell.Indices[7]);
                        subMesh.Indices.Add(cell.Indices[7]);
                        subMesh.Indices.Add(cell.Indices[5]);
                        subMesh.Indices.Add(cell.Indices[1]);

                        subMesh.Indices.Add(cell.Indices[3]);
                        subMesh.Indices.Add(cell.Indices[2]);
                        subMesh.Indices.Add(cell.Indices[6]);
                        subMesh.Indices.Add(cell.Indices[6]);
                        subMesh.Indices.Add(cell.Indices[7]);
                        subMesh.Indices.Add(cell.Indices[3]);

                        subMesh.Indices.Add(cell.Indices[2]);
                        subMesh.Indices.Add(cell.Indices[0]);
                        subMesh.Indices.Add(cell.Indices[4]);
                        subMesh.Indices.Add(cell.Indices[4]);
                        subMesh.Indices.Add(cell.Indices[6]);
                        subMesh.Indices.Add(cell.Indices[2]);
                    } else {
                        throw new NotImplementedException("implement process for "+cell.Type);
                    }
                }

                //deal with cell data
                #region Cell Data
                if(model.CellDatas!=null&&model.CellDatas.Count>0) {
                    var vtxCount = subMesh.Vertices.Count;
                    foreach(var kv in model.CellDatas) {
                        var key = kv.Key;
                        var val = kv.Value;
                        if(val is Scalars) {
                            var cs = val as Scalars;
                            List<float[]> realScalar = new List<float[]>(vtxCount);
                            for(int i = 0; i<vtxCount; i++) {
                                realScalar.Add(cs.Values[cellAttrib2Point[i]]);
                            }
                            if(!subMesh.ScalarAttribs.ContainsKey(key)) {
                                subMesh.ScalarAttribs[key]=new List<List<float[]>>();
                            }
                            subMesh.ScalarAttribs[key].Add(realScalar);
                        } else if(val is VectorsOrNormals) {
                            var vn = val as VectorsOrNormals;
                            List<float[]> realVector = new List<float[]>(vtxCount);
                            for(int i = 0; i<vtxCount; i++) {
                                var item = vn.Values[cellAttrib2Point[i]];
                                realVector.Add(new float[] { item.X, item.Y, item.Z });
                            }
                            if(!subMesh.VectorAttribs.ContainsKey(key)) {
                                subMesh.VectorAttribs[key]=new List<List<float[]>>();
                            }
                            subMesh.VectorAttribs[key].Add(realVector);
                        } else if(val is FieldData) {
                            var fieldData = val as FieldData;
                            if(fieldData.Arrays!=null&&fieldData.Arrays.Count>0) {
                                foreach(var pair in fieldData.Arrays) {
                                    var pk = pair.Key;
                                    var pv = pair.Value;
                                    //taken field data array as scalars
                                    if(pv.Tuples!=null&&pv.Tuples.Count>0) {
                                        var scalars = new List<float[]>(vtxCount);
                                        for(int i = 0; i<vtxCount; i++) {
                                            scalars.Add(pv.Tuples[cellAttrib2Point[i]]);
                                        }
                                        var k = key+"_"+pk;
                                        if(!subMesh.ScalarAttribs.ContainsKey(k)) {
                                            subMesh.ScalarAttribs[k]=new List<List<float[]>>();
                                        }
                                        subMesh.ScalarAttribs[k].Add(scalars);
                                    }
                                }
                            }
                        } else {
                            throw new NotImplementedException("not implemented now for "+val.GetType());
                        }
                    }
                }
                #endregion

            }
            #endregion

            return mesh;
        }

        public SimpleCfdMesh CombineTimeSeriesSteps(SimpleCfdMesh[] meshes)
        {
            if(meshes.Length==1) {
                return meshes[0];
            }
            //todo do real mesh combine later
            bool sameGeometry = true;
            #region simple data validation
            var random = new Random();
            var idx = random.Next(1, meshes.Length);

            var first = meshes[0];
            var second = meshes[idx];
            if(first.Submeshes.Count!=second.Submeshes.Count) {
                sameGeometry=false;
            }


            var fsb = first.Submeshes[0];
            var ssb = second.Submeshes[0];
            if(sameGeometry) {
                if(fsb.Name!=ssb.Name) {
                    sameGeometry=false;
                }
                if(sameGeometry&&fsb.Vertices.Count!=ssb.Vertices.Count) {
                    sameGeometry=false;
                }

                if(sameGeometry&&fsb.IndexType!=ssb.IndexType) {
                    sameGeometry=false;
                }
                if(sameGeometry&&fsb.Indices.Count!=ssb.Indices.Count) {
                    sameGeometry=false;
                }
                if(sameGeometry) {
                    for(int i = 0, len = fsb.Vertices.Count; i<len; i++) {
                        if(fsb.Vertices[i]!=ssb.Vertices[i]) {
                            sameGeometry=false;
                            break;
                        }
                    }
                }
                if(sameGeometry) {
                    for(int i = 0, len = fsb.Indices.Count; i<len; i++) {
                        if(fsb.Indices[i]!=ssb.Indices[i]) {
                            sameGeometry=false;
                            break;
                        }
                    }
                }
            }
            #endregion

            first.TimeStepCount=meshes.Length;

            if(sameGeometry) {//just merge attributes
                foreach(var skv in first.Submeshes) {
                    fsb=skv.Value;
                    for(int i = 1, len = meshes.Length; i<len; i++) {
                        second=meshes[i];
                        if(!second.Submeshes.ContainsKey(skv.Key)) {
                            return null;
                        }
                        ssb=second.Submeshes[skv.Key];
                        foreach(var kv in ssb.ScalarAttribs) {
                            var key = kv.Key;
                            var value = kv.Value;
                            if(!fsb.ScalarAttribs.ContainsKey(key)) {
                                return null;
                            }
                            fsb.ScalarAttribs[key].AddRange(value);
                        }
                        foreach(var kv in ssb.VectorAttribs) {
                            var key = kv.Key;
                            var value = kv.Value;
                            if(!fsb.VectorAttribs.ContainsKey(key)) {
                                return null;
                            }
                            fsb.VectorAttribs[key].AddRange(value);
                        }
                    }
                }
            } else {
                var count = first.Submeshes.Count;
                for(int i = 1, len = meshes.Length; i<len; i++) {
                    second=meshes[i];
                    foreach(var kv in second.Submeshes) {
                        var sm = kv.Value;
                        sm.TimeStep=i;
                        first.Submeshes[count++]=sm;
                    }
                }
            }


            return first;
        }

        public void SliceMesh(SimpleCfdMesh mesh, int vertexAmountThreshold = 65000)
        {
            Dictionary<int, Submesh> newSubmeshes = new Dictionary<int, Submesh>();
            int subKey = 0;
            foreach(var kv in mesh.Submeshes) {
                if(kv.Value.Vertices.Count<=vertexAmountThreshold) {
                    newSubmeshes.Add(subKey++, kv.Value);
                    continue;
                }

                var submesh = kv.Value;
                var slice = (int)Math.Ceiling(submesh.Vertices.Count/(float)vertexAmountThreshold);
                var bestVertexCountThreshold = (int)(submesh.Vertices.Count/(float)slice);
                int sliceKey = 0;

                Dictionary<int, int> old2New = new Dictionary<int, int>();
                List<int> indices = new List<int>();
                for(int i = 0, ilen = submesh.Indices.Count; i<ilen; i++) {
                    var idx = submesh.Indices[i];
                    if(!old2New.ContainsKey(idx)) {
                        old2New[idx]=old2New.Count;
                    }
                    indices.Add(old2New[idx]);

                    if(indices.Count%3==0) {//trick for triangles 
                        if(old2New.Count>=bestVertexCountThreshold) {
                            if(sliceKey<slice-1||old2New.Count>=vertexAmountThreshold) {
                                var newSubmesh = CreateSubmeshSlice(submesh, old2New.Keys.ToList(), indices);
                                newSubmesh.Name=submesh.Name+"_"+sliceKey;
                                newSubmesh.TimeStep=submesh.TimeStep;
                                newSubmeshes[subKey++]=newSubmesh;

                                old2New.Clear();
                                indices.Clear();
                                sliceKey++;
                            }
                        }
                    }
                }
                if(old2New.Count>0) {
                    var newSubmesh = CreateSubmeshSlice(submesh, old2New.Keys.ToList(), indices);
                    newSubmesh.Name=submesh.Name+"_"+sliceKey;
                    newSubmesh.TimeStep=submesh.TimeStep;
                    newSubmeshes[subKey++]=newSubmesh;

                    old2New.Clear();
                    indices.Clear();
                }
            }
            mesh.Submeshes=newSubmeshes;
        }

        public Submesh CreateSubmeshSlice(Submesh submesh, List<int> vertexIndices, List<int> indices)
        {
            var newsb = new Submesh();
            newsb.Name=submesh.Name;
            newsb.IndexType=submesh.IndexType;
            newsb.Indices.AddRange(indices);

            //vertices
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach(var k in vertexIndices) {
                var v = submesh.Vertices[k];
                newsb.Vertices.Add(v);
                min=Vector3.Min(min, v);
                max=Vector3.Max(max, v);
            }
            newsb.Max=max;
            newsb.Min=min;

            //normals
            if(submesh.Normals!=null&&submesh.Normals.Count>0) {
                foreach(var k in vertexIndices) {
                    newsb.Normals.Add(submesh.Normals[k]);
                }
            }
            //texture coords
            if(submesh.Texcoords!=null&&submesh.Texcoords.Count>0) {
                foreach(var k in vertexIndices) {
                    newsb.Texcoords.Add(submesh.Texcoords[k]);
                }
            }
            //colors
            if(submesh.Colors!=null&&submesh.Colors.Count>0) {
                foreach(var k in vertexIndices) {
                    newsb.Colors.Add(submesh.Colors[k]);
                }
            }
            //scalar attribs
            if(submesh.ScalarAttribs!=null&&submesh.ScalarAttribs.Count>0) {
                foreach(var kv in submesh.ScalarAttribs) {
                    List<List<float[]>> attribs = new List<List<float[]>>();
                    foreach(var timeStep in kv.Value) {
                        var newts = new List<float[]>();
                        foreach(var k in vertexIndices) {
                            newts.Add(timeStep[k]);
                        }
                        attribs.Add(newts);
                    }
                    newsb.ScalarAttribs[kv.Key]=attribs;
                }
            }
            //vector attribs
            if(submesh.VectorAttribs!=null&&submesh.VectorAttribs.Count>0) {
                foreach(var kv in submesh.VectorAttribs) {
                    List<List<float[]>> attribs = new List<List<float[]>>();
                    foreach(var timeStep in kv.Value) {
                        var newts = new List<float[]>();
                        foreach(var k in vertexIndices) {
                            newts.Add(timeStep[k]);
                        }
                        attribs.Add(newts);
                    }
                    newsb.VectorAttribs[kv.Key]=attribs;
                }
            }

            return newsb;
        }
    }

    internal static class PerfTimer
    {
        private static Stopwatch watch = new Stopwatch();
        private static string file = "";

        public static void Start(string msg)
        {
            file = msg;
            watch.Start();
        }

        public static void Stop()
        {
            watch.Stop();
            var time = watch.ElapsedMilliseconds;
            Trace.WriteLine(string.Format("msg:{0}>>>time:{1}ms", file, time));
            watch.Reset();
        }
    }
}

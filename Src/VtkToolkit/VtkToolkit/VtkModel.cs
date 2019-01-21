using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace VtkToolkit
{
    public class VtkModel
    {
        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }
        public string Description { get; set; }
        public VtkDataSet DataSet { get; set; }
        public Dictionary<string, VtkDataAttribute> PointDatas { get; set; }
        public Dictionary<string, VtkDataAttribute> CellDatas { get; set; }
    }

    #region Data Set
    public abstract class VtkDataSet
    {
        public Dimensions Dimensions { get; set; }
        public List<Vector3> Points { get; set; }
        public VtkDataType PointDataType { get; set; }
        public List<Cell> Cells { get; set; }
    }

    public class VtkUnstructuredGrid :VtkDataSet
    {

    }

    public class VtkPolyData :VtkDataSet
    {
        public Dictionary<string, PolyDataItem> Items { get; set; }
    }

    public class PolyDataItem
    {
        public string Name { get; set; }
        public List<int[]> Indices { get; set; }
    }

    #endregion

    #region Data Attribute

    public abstract class VtkDataAttribute
    {
        public string Name { get; set; }
    }

    public class Scalars :VtkDataAttribute
    {
        public int ComponentCount { get; set; }
        public List<float[]> Values { get; set; }
        public string LookupTable { get; set; }
    }
    public class ColorScalars :Scalars { }

    public class VectorsOrNormals :VtkDataAttribute
    {
        public List<Vector3> Values { get; set; }
    }

    public class Vectors :VectorsOrNormals
    {
    }
    public class Normals :VectorsOrNormals
    {
    }
    public class TextureCoordinates :VtkDataAttribute
    {
        public int Dim { get; set; }
        public List<float[]> Values { get; set; }
    }

    public class Tensors :VtkDataAttribute
    {
        public List<Vector3[]> Values { get; set; }//each item should contains 3 vector
    }

    public class FieldData :VtkDataAttribute
    {
        public Dictionary<string, FieldDataItem> Arrays { get; set; }
    }

    public class FieldDataItem
    {
        public string Name { get; set; }
        public int ComponentCount { get; set; }
        public List<float[]> Tuples { get; set; }
    }

    public class LookupTable :VtkDataAttribute
    {
        public List<Vector4> Rgbas { get; set; }
    }
    #endregion

    public class Cell
    {
        public VtkCellType Type { get; set; }
        public List<int> Indices { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Dimensions
    {
        public int X;
        public int Y;
        public int Z;
    }

    public enum VtkGeoTopoType
    {
        StructuredPoints,
        StructuredGrid,
        UnstructuredGrid,
        Polydata,
        RectilinearGrid,
        Field
    }

    public enum VtkDataType
    {
        Bit,
        UnsignedChar,
        Char,
        UnsignedShort,
        Short,
        UnsignedInt,
        Int,
        UnsignedLong,
        Long,
        Float,
        Double
    }

    public enum VtkCellType
    {
        //linear cell type
        Vertex = 1,
        PolyVertex = 2,
        Line = 3,
        PolyLine = 4,
        Triangle = 5,
        TriangleStrip = 6,
        Polygon = 7,
        Pixel = 8,
        Quad = 9,
        Tetra = 10,
        Voxel = 11,
        Hexahedron = 12,
        Wedge = 13,
        Pyramid = 14,

        //non-linear cell type
        QuadraticEdge = 21,
        QuadraticTriangle = 22,
        QuadraticQuad = 23,
        QuadraticTetra = 24,
        QuadraticHexahedron = 25,
    }
}

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
    public class VtkLoader
    {
        public static VtkModel Load(string filePath)
        {
            VtkFileFormat format;
            using(var reader = new StreamReader(filePath, Encoding.ASCII)) {
                reader.ReadLine();
                reader.ReadLine();
                var line = reader.ReadLine().Trim();
                if(line=="ASCII") {
                    format=VtkFileFormat.Ascii;
                } else if(line=="BINARY") {
                    format=VtkFileFormat.Binary;
                } else {
                    throw new Exception("invalid vtk format.");
                }
            }

            using(var stream = new FileStream(filePath, FileMode.Open)) {
                if(format==VtkFileFormat.Binary) {
                    using(var loader = new BinaryLoader(stream)) {
                        return loader.Load();
                    }
                } else {
                    using(var loader = new AsciiLoader(stream)) {
                        return loader.Load();
                    }
                }
            }
        }

        #region Internal Enums and Classes

        internal enum VtkFileFormat
        {
            ASCII, BINARY, Ascii = ASCII, Binary = BINARY,
        }
        internal enum VtkToken
        {
            None,
            Invalid,

            DATASET,
            POINT_DATA,
            CELL_DATA,

            STRUCTURED_POINT,
            STRUCTURED_GRID,
            UNSTRUCTURED_GRID,
            POLYDATA,
            RECTILINEAR_GRID,
            FIELD,

            DIMENSIONS,
            ORIGIN,
            SPACING,
            POINTS,
            X_COORDINATES,
            Y_COORDINATES,
            Z_COORDINATES,
            VERTICES,
            LINES,
            POLYGONS,
            TRIANGLE_STRIPS,

            CELLS,
            CELL_TYPES,

            SCALARS,
            LOOKUP_TABLE,
            COLOR_SCALARS,
            VECTORS,
            NORMALS,
            TEXTURE_COORDINATES,
            TENSORS,

            Dataset = DATASET,
            PointData = POINT_DATA,
            CellData = CELL_DATA,

            StructuredPoint = STRUCTURED_POINT,
            StructuredGrid = STRUCTURED_GRID,
            UnstructuredGrid = UNSTRUCTURED_GRID,
            Polydata = POLYDATA,
            RectilinearGrid = RECTILINEAR_GRID,
            Field = FIELD,

            Dimensions = DIMENSIONS,
            Origin = ORIGIN,
            Spacing = SPACING,
            Points = POINTS,
            XCoordinates = X_COORDINATES,
            YCoordinates = Y_COORDINATES,
            ZCoordinates = Z_COORDINATES,
            Vertices = VERTICES,
            Lines = LINES,
            Polygons = POLYGONS,
            TriangleStrips = TRIANGLE_STRIPS,

            Cells = CELLS,
            CellTypes = CELL_TYPES,

            Scalars = SCALARS,
            LookupTable = LOOKUP_TABLE,
            ColorScalars = COLOR_SCALARS,
            Vectors = VECTORS,
            Normals = NORMALS,
            TextureCoordinates = TEXTURE_COORDINATES,
            Tensors = TENSORS,

            #region unknown but occurred in vtk ascii format
            METADATA, NAME, INFORMATION, DATA,
            UnknowMetadata = METADATA, UnknownName = NAME, UnknownInformation = INFORMATION, UnknownData = DATA,
            #endregion
        }
        internal abstract class InternalLoader :IDisposable
        {
            protected Stream _baseStream;
            protected VtkToken _curToken = VtkToken.None;
            protected string _curline;
            protected string[] _curLineParts=new string[0];
            protected int _curPartIdx=0;
            protected abstract VtkFileFormat FileFormat { get; }
            public abstract bool EndOfFile
            { get; }
            public InternalLoader(Stream baseStream)
            {
                _baseStream=baseStream;
            }

            public VtkModel Load()
            {
                var model = new VtkModel();
                if(!LoadHeader(model)) {
                    return null;
                }
                while(!EndOfFile) {
                    if(_curToken==VtkToken.None) {
                        _curToken=GetVtkToken();
                    }
                    switch(_curToken) {
                        case VtkToken.Dataset:
                            LoadDataset(model);
                            break;
                        case VtkToken.CellData:
                            LoadCellData(model);
                            break;
                        case VtkToken.PointData:
                            LoadPointData(model);
                            break;
                        case VtkToken.UnknowMetadata:
                            SkipMetadata();
                            break;
                        default:
                            throw new NotImplementedException(string.Format("unsupported vtk token:{0}.", _curToken));
                            break;
                    }
                }
                return model;
            }

            protected bool LoadHeader(VtkModel model)
            {
                //1st line - file version and identifier: # vtk DataFile Version x.x
                var line = GetLine().Trim();
                var tokens = line.Split(' ');
                Debug.WriteLine(string.Format("first line tokens count (should be 5): {0}", tokens));
                var v = tokens.Last();
                Debug.Assert(v.Contains('.'));
                var toks = v.Split('.');
                int majorV = int.Parse(toks[0]);
                int minorV = int.Parse(toks[1]);
                Debug.WriteLine(string.Format("file version: {0}.{1}", majorV, minorV));
                model.MajorVersion=majorV;
                model.MinorVersion=minorV;

                //2nd line - header: no more than 256 char, for data description
                line=GetLine();
                Debug.Assert(line.Length<=255);
                Debug.WriteLine(string.Format("file description: {0}", line));
                model.Description=line;

                //3rd line - file format: either ASCII or BINARY
                line=GetLine().Trim();
                Debug.Assert(line=="ASCII"||line=="BINARY");
                return true;
            }

            protected void LoadDataset(VtkModel model)
            {
                _curToken=GetVtkToken();
                switch(_curToken) {
                    //                case VtkToken.StructuredPoint:break;
                    //                case VtkToken.StructuredGrid:break;
                    case VtkToken.UnstructuredGrid:
                        var usGrid = new VtkUnstructuredGrid();
                        LoadUnstructuredGrid(usGrid);
                        model.DataSet=usGrid;
                        break;
                    case VtkToken.Polydata:
                        var polydata = new VtkPolyData();
                        LoadPolydata(polydata);
                        model.DataSet=polydata;
                        break;
                    //                case VtkToken.RectilinearGrid:break;
                    //                case VtkToken.Field:break;
                    case VtkToken.UnknowMetadata:
                        SkipMetadata();
                        break;
                    default:
                        throw new NotImplementedException(String.Format("unsupported vtk token: {0}.", _curToken));
                        break;
                }
            }

            protected void LoadUnstructuredGrid(VtkUnstructuredGrid dataset)
            {
                while(!EndOfFile) {
                    _curToken=GetVtkToken();
                    switch(_curToken) {
                        case VtkToken.Points:
                            LoadPoints(dataset);
                            break;
                        case VtkToken.Cells:
                            LoadCells(dataset);
                            break;
                        case VtkToken.CellTypes:
                            LoadCellTypes(dataset);
                            break;
                        case VtkToken.UnknowMetadata:
                            SkipMetadata();
                            break;
                        case VtkToken.None:
                            break;
                        default:
                            return;
                    }
                }
            }

            protected void LoadPolydata(VtkPolyData polydata)
            {
                while(!EndOfFile) {
                    _curToken=GetVtkToken();
                    switch(_curToken) {
                        case VtkToken.Points:
                            LoadPoints(polydata);
                            break;
                        case VtkToken.Vertices:
                        case VtkToken.Lines:
                        case VtkToken.Polygons:
                        case VtkToken.TriangleStrips:
                            LoadPolydataItems(polydata);
                            break;
                        case VtkToken.UnknowMetadata:
                            SkipMetadata();
                            break;
                        case VtkToken.None:
                            break;
                        default:
                            return;
                    }
                }
            }

            protected void LoadPoints(VtkDataSet dataSet)
            {
                int n = GetCount();
                VtkDataType dtType = GetDataType();
                Debug.WriteLine(string.Format("POINTS size: {0}", n));
                Debug.WriteLine(string.Format("POINTS data type: {0}", dtType.ToString()));
                dataSet.PointDataType=dtType;
                dataSet.Points=new List<Vector3>(n);
                for(int i = 0; i<n; i++) {
                    dataSet.Points.Add(new Vector3() {
                        X=GetDataAsFloat(dtType),
                        Y=GetDataAsFloat(dtType),
                        Z=GetDataAsFloat(dtType)
                    });
                }
            }

            protected void LoadCells(VtkDataSet dataSet)
            {
                var n = GetCount();
                Debug.WriteLine(string.Format("CELLS size: {0}", n));
                var size = GetCount();
                Debug.WriteLine(string.Format("CELLS size: {0}", size));
                dataSet.Cells=new List<Cell>(n);
                for(int c = 0, s = 0; c<n&&s<size; c++) {
                    var iCountPerCell = GetIntData();
                    s++;
                    var cell = new Cell();
                    cell.Indices=new List<int>(iCountPerCell);
                    for(int i = 0; i<iCountPerCell; i++) {
                        cell.Indices.Add(GetIntData());
                        s++;
                    }
                    dataSet.Cells.Add(cell);
                }
            }

            protected void LoadCellTypes(VtkDataSet dataSet)
            {
                var n = GetCount();
                Debug.WriteLine(string.Format("CELL_TYPES size: {0}", n));
                for(int i = 0; i<n; i++) {
                    var cellType = (VtkCellType)GetIntData();
                    dataSet.Cells[i].Type=cellType;
                }
            }

            protected void LoadPolydataItems(VtkPolyData polyData)
            {
                var n = GetCount();
                var size = GetCount();
                Debug.WriteLine(string.Format("{0} n: {1}, size: {2}", _curToken, n, size));
                if(polyData.Items==null) {
                    polyData.Items=new Dictionary<string, PolyDataItem>();
                }

                var item = new PolyDataItem();
                item.Name=_curToken.ToString();
                item.Indices=new List<int[]>();
                polyData.Items[item.Name]=item;
                for(int ic = 0, s = 0; ic<n&&s<size; ic++) {
                    var iCountPerArr = GetIntData();
                    s++;
                    var arr = new int[iCountPerArr];
                    for(int i = 0; i<iCountPerArr; i++) {
                        arr[i]=GetIntData();
                        s++;
                    }
                    item.Indices.Add(arr);
                }
            }

            protected void LoadCellData(VtkModel model)
            {
                var n = GetCount();
                Debug.WriteLine(string.Format("CELL_DATA size: {0}", n));
                model.CellDatas=new Dictionary<string, VtkDataAttribute>();
                LoadDataAttribs(model.CellDatas, n);
            }

            protected void LoadPointData(VtkModel model)
            {
                var n = GetCount();
                Debug.WriteLine(string.Format("POINT_DATA size: {0}", n));
                model.PointDatas=new Dictionary<string, VtkDataAttribute>();
                LoadDataAttribs(model.PointDatas, n);
            }

            protected void LoadDataAttribs(Dictionary<string, VtkDataAttribute> attributes, int size)
            {
                while(!EndOfFile) {
                    _curToken=GetVtkToken();
                    switch(_curToken) {
                        case VtkToken.Scalars:
                        case VtkToken.ColorScalars:
                            LoadScalars(attributes, size);
                            break;
                        case VtkToken.LookupTable:
                            LoadLookupTable(attributes, size);
                            break;
                        case VtkToken.Vectors:
                        case VtkToken.Normals:
                            LoadVectorsOrNormals(attributes, size);
                            break;
                        case VtkToken.Field:
                            LoadField(attributes, size);
                            break;
                        case VtkToken.UnknowMetadata:
                            SkipMetadata();
                            break;
                        case VtkToken.None:
                            break;
                        case VtkToken.TextureCoordinates:
                            LoadTextureCoordinates(attributes, size);
                            break;
                        case VtkToken.Tensors:
                            throw new NotImplementedException(String.Format("unsupported vtk token: {0}.", _curToken));
                            break;
                        default:
                            return;
                    }
                }
            }

            protected void LoadScalars(Dictionary<string, VtkDataAttribute> attributes, int size)
            {
                Scalars scalars;
                VtkDataType dtType = FileFormat==VtkFileFormat.Ascii ? VtkDataType.Float : VtkDataType.UnsignedChar;
                if(_curToken==VtkToken.ColorScalars) {
                    scalars=new ColorScalars();
                    scalars.Name=GetStringPart();
                    scalars.ComponentCount=4;
                    Debug.WriteLine(string.Format("COLOR_SCALARS name: {0}, dataType: {1}, numComp: {2}", scalars.Name, dtType, scalars.ComponentCount));
                } else {
                    scalars=new Scalars();
                    scalars.Name=GetStringPart();
                    dtType=GetDataType();
                    scalars.ComponentCount=1;
                    if(_curPartIdx<_curLineParts.Length) {
                        scalars.ComponentCount=GetCount();
                    }
                    Debug.WriteLine(string.Format("SCALARS name: {0}, dataType: {1}, numComp: {2}", scalars.Name, dtType, scalars.ComponentCount));
                    var token = GetVtkToken();
                    Debug.Assert(token==VtkToken.LookupTable);
                    scalars.LookupTable=GetStringPart();
                    Debug.WriteLine(string.Format("SCALARS LOOKUP_TABLE: {0}", scalars.LookupTable));
                }
                attributes[scalars.Name]=scalars;

                scalars.Values=new List<float[]>(size);
                for(int i = 0; i<size; i++) {
                    var val = new float[scalars.ComponentCount];
                    for(int j = 0; j<scalars.ComponentCount; j++) {
                        val[j]=GetDataAsFloat(dtType);
                        if(FileFormat==VtkFileFormat.BINARY) {
                            val[j]/=255f;
                        }
                    }
                    scalars.Values.Add(val);
                }
            }

            protected void LoadLookupTable(Dictionary<string, VtkDataAttribute> attributes, int size)
            {
                var lookup = new LookupTable();
                lookup.Name=GetStringPart();
                attributes[lookup.Name]=lookup;

                var lookupSize = GetCount();
                Debug.WriteLine(string.Format("LOOKUP_TABLE name: {0}, size: {1}", lookup.Name, lookupSize));
                lookup.Rgbas=new List<Vector4>(lookupSize);
                if(FileFormat==VtkFileFormat.Ascii) {
                    for(int i = 0; i<lookupSize; i++) {
                        var rgba = new Vector4();
                        rgba.X=GetDataAsFloat(VtkDataType.Float);
                        rgba.Y=GetDataAsFloat(VtkDataType.Float);
                        rgba.Z=GetDataAsFloat(VtkDataType.Float);
                        rgba.W=GetDataAsFloat(VtkDataType.Float);
                        lookup.Rgbas.Add(rgba);
                    }
                } else {
                    for(int i = 0; i<lookupSize; i++) {
                        var rgba = new Vector4();
                        rgba.X=GetDataAsFloat(VtkDataType.UnsignedChar)/255f;
                        rgba.Y=GetDataAsFloat(VtkDataType.UnsignedChar)/255f;
                        rgba.Z=GetDataAsFloat(VtkDataType.UnsignedChar)/255f;
                        rgba.W=GetDataAsFloat(VtkDataType.UnsignedChar)/255f;
                        lookup.Rgbas.Add(rgba);
                    }
                }
            }
            protected void LoadVectorsOrNormals(Dictionary<string, VtkDataAttribute> attributes, int size)
            {
                VectorsOrNormals data;
                if(_curToken==VtkToken.Vectors) {
                    data=new Vectors();
                } else {
                    data=new Normals();
                }
                data.Name=GetStringPart();
                attributes[data.Name]=data;

                var dtType = GetDataType();
                Debug.WriteLine(string.Format("{2} name: {0}, dataType: {1}", data.Name, dtType, _curToken));
                data.Values=new List<Vector3>(size);
                for(int i = 0; i<size; i++) {
                    var v = new Vector3();
                    v.X=GetDataAsFloat(dtType);
                    v.Y=GetDataAsFloat(dtType);
                    v.Z=GetDataAsFloat(dtType);
                    data.Values.Add(v);
                }
            }
            protected void LoadField(Dictionary<string, VtkDataAttribute> attributes, int size)
            {
                var fields = new FieldData();
                fields.Name=GetStringPart();
                attributes[fields.Name]=fields;

                var numArrays = GetCount();
                Debug.WriteLine(string.Format("FIELD name: {0}, numArrays: {1}", fields.Name, numArrays));
                fields.Arrays=new Dictionary<string, FieldDataItem>(numArrays);

                int ac = 0;
                while(!EndOfFile&&ac<numArrays) {
                    var name = GetStringPart();
                    if(string.IsNullOrEmpty(name)) {
                        continue;
                    }
                    var item = new FieldDataItem();
                    item.Name=name;
                    item.ComponentCount=GetCount();
                    var numTuples = GetCount();
                    var dtType = GetDataType();
                    Debug.WriteLine(string.Format("FieldData name: {0}, numComp: {1}, numTuple: {2}, dtType: {3}", item.Name, item.ComponentCount, numTuples, dtType));
                    item.Tuples=new List<float[]>(numTuples);
                    for(int j = 0; j<numTuples; j++) {
                        var arr = new float[item.ComponentCount];
                        for(int k = 0; k<item.ComponentCount; k++) {
                            arr[k]=GetDataAsFloat(dtType);
                        }
                        item.Tuples.Add(arr);
                    }
                    fields.Arrays.Add(item.Name, item);

                    //danger here
                    bool stop = false;
                    while(!stop) {
                        switch((char)Peek()) {
                            case '\0':
                            case '\r':
                            case '\n':
                                GetLine();
                                break;
                            default:
                                stop=true;
                                break;
                        }
                    }
                    if(Peek()==(int)'M') {
                        _curToken=GetVtkToken();
                        Debug.Assert(_curToken==VtkToken.METADATA);
                        SkipMetadata();
                    }

                    ac++;
                }
            }

            protected void SkipMetadata()
            {
                _curPartIdx=_curLineParts.Length;//danger, used for skip this line
                while(!EndOfFile) {
                    _curToken=GetVtkToken();
                    switch(_curToken) {
                        case VtkToken.UnknownInformation:
                        case VtkToken.UnknownName:
                        case VtkToken.UnknownData:
                            _curPartIdx=_curLineParts.Length;//danger, used for skip this line
                            break;
                        default:
                            return;
                    }
                }
            }

            protected void LoadTextureCoordinates(Dictionary<string, VtkDataAttribute> attributes, int size)
            {
                var textureCoordinates = new TextureCoordinates();
                textureCoordinates.Name=GetStringPart();
                textureCoordinates.Dim=GetCount();
                VtkDataType dtType = VtkDataType.Float;
                if(_curPartIdx<_curLineParts.Length) {
                    dtType=GetDataType();
                }
                Debug.WriteLine(string.Format("TEXTURE_COORDINATES name: {0}, dataType: {1}, dim: {2}", textureCoordinates.Name, dtType, textureCoordinates.Dim));


                attributes[textureCoordinates.Name]=textureCoordinates;
                textureCoordinates.Values=new List<float[]>(size);

                for(int i = 0; i<size; i++) {
                    var val = new float[textureCoordinates.Dim];
                    for(int j = 0; j<textureCoordinates.Dim; j++) {
                        val[j]=GetDataAsFloat(dtType);
                    }
                    textureCoordinates.Values.Add(val);
                }
            }

            protected abstract float GetDataAsFloat(VtkDataType dataType);
            protected abstract int GetIntData();

            protected string GetStringPart()
            {
                if(_curPartIdx==_curLineParts.Length) {
                    _curPartIdx=0;
                    _curline=GetLine().Trim();
                    _curLineParts=_curline.Split(' ');
                }

                return _curLineParts[_curPartIdx++];
            }

            protected abstract int Peek();
            private VtkToken GetVtkToken()
            {
                var tok = VtkToken.Invalid;
                VtkToken.TryParse(GetStringPart(), out tok);
                return tok;
            }

            private int GetCount()
            {
                int n = 0;
                int.TryParse(GetStringPart(), out n);

                return n;
            }

            private VtkDataType GetDataType()
            {
                var dataType = VtkDataType.Int;
                VtkDataType.TryParse(GetStringPart(), true, out dataType);

                return dataType;
            }

            protected abstract string GetLine();
            public virtual void Dispose()
            {
                if(_baseStream!=null) {
                    _baseStream.Dispose();
                    _baseStream=null;
                }
            }
        }

        internal class BinaryLoader :InternalLoader
        {
            #region Constants
            private const string CarriageReturnLineFeed = "\r\n";
            private const string Empty = "";
            private const char CarriageReturn = '\r';
            private const char LineFeed = '\n';
            private const char Tab = '\t';
            #endregion
            private StringBuilder _sb=new StringBuilder(1024);

            protected override VtkFileFormat FileFormat { get { return VtkFileFormat.Binary; } }

            public override bool EndOfFile
            {
                get { return _reader==null||_reader.PeekChar()==-1; }
            }

            protected override string GetLine()
            {
                _sb.Clear();
                bool end = false;
                while(!end) {
                    int val = _reader.Read();
                    switch(val) {
                        case CarriageReturn:
                            if(_reader.PeekChar()==LineFeed) {
                                _reader.Read();
                            }
                            end=true;
                            break;
                        case LineFeed:
                            end=true;
                            break;
                        default:
                            _sb.Append((char)val);
                            break;
                    }
                }

                //eat all following '\0'
                //                while(!EndOfFile&&_reader.PeekChar()=='\0') {_reader.Read();}
                return _sb.ToString();
            }

            protected override float GetDataAsFloat(VtkDataType dataType)
            {
                //as stated in file-format.pdf for vtk binary format, it is written in big endian format, so we should check whether we need swap bytes here
                if(BitConverter.IsLittleEndian) {
                    byte[] bytes;
                    switch(dataType) {
                        case VtkDataType.Bit:
                            return (float)_reader.Read();
                            break;
                        case VtkDataType.UnsignedChar:
                            return (float)_reader.ReadSByte();
                            break;
                        case VtkDataType.Char:
                            return (float)_reader.ReadByte();
                            break;
                        case VtkDataType.UnsignedShort:
                            bytes=_reader.ReadBytes(sizeof(ushort));
                            return (float)BitConverter.ToUInt16(bytes.Reverse().ToArray(), 0);
                            break;
                        case VtkDataType.Short:
                            bytes=_reader.ReadBytes(sizeof(short));
                            return (float)BitConverter.ToInt16(bytes.Reverse().ToArray(), 0);
                            break;
                        case VtkDataType.UnsignedInt:
                            bytes=_reader.ReadBytes(sizeof(uint));
                            return (float)BitConverter.ToUInt32(bytes.Reverse().ToArray(), 0);
                            break;
                        case VtkDataType.Int:
                            bytes=_reader.ReadBytes(sizeof(int));
                            return (float)BitConverter.ToInt32(bytes.Reverse().ToArray(), 0);
                            break;
                        case VtkDataType.UnsignedLong:
                            bytes=_reader.ReadBytes(sizeof(ulong));
                            return (float)BitConverter.ToUInt64(bytes.Reverse().ToArray(), 0);
                            break;
                        case VtkDataType.Long:
                            bytes=_reader.ReadBytes(sizeof(long));
                            return (float)BitConverter.ToInt64(bytes.Reverse().ToArray(), 0);
                            break;
                        case VtkDataType.Float:
                            bytes=_reader.ReadBytes(sizeof(float));
                            return (float)BitConverter.ToSingle(bytes.Reverse().ToArray(), 0);
                        case VtkDataType.Double:
                            bytes=_reader.ReadBytes(sizeof(double));
                            return (float)BitConverter.ToDouble(bytes.Reverse().ToArray(), 0);
                            break;
                    }
                } else {
                    switch(dataType) {
                        case VtkDataType.Bit:
                            return (float)_reader.Read();
                            break;
                        case VtkDataType.UnsignedChar:
                            return (float)_reader.ReadSByte();
                            break;
                        case VtkDataType.Char:
                            return (float)_reader.ReadByte();
                            break;
                        case VtkDataType.UnsignedShort:
                            return (float)_reader.ReadUInt16();
                            break;
                        case VtkDataType.Short:
                            return (float)_reader.ReadInt16();
                            break;
                        case VtkDataType.UnsignedInt:
                            return (float)_reader.ReadUInt32();
                            break;
                        case VtkDataType.Int:
                            return (float)_reader.ReadInt32();
                            break;
                        case VtkDataType.UnsignedLong:
                            return (float)_reader.ReadUInt64();
                            break;
                        case VtkDataType.Long:
                            return (float)_reader.ReadInt64();
                            break;
                        case VtkDataType.Float:
                            return (float)_reader.ReadSingle();
                            break;
                        case VtkDataType.Double:
                            return (float)_reader.ReadDouble();
                            break;
                    }
                }

                return 0;
            }

            protected override int GetIntData()
            {
                //as stated in file-format.pdf for vtk binary format, it is written in big endian format, so we should check whether we need swap bytes here
                if(BitConverter.IsLittleEndian) {
                    var bytes = _reader.ReadBytes(4);
                    return BitConverter.ToInt32(bytes.Reverse().ToArray(), 0);
                } else {
                    return _reader.ReadInt32();
                }
            }

            protected override int Peek()
            {
                return _reader.PeekChar();
            }

            private BinaryReader _reader;
            public BinaryLoader(Stream baseStream) : base(baseStream)
            {
                _reader=new BinaryReader(_baseStream, Encoding.ASCII);
            }

            public override void Dispose()
            {
                if(_reader!=null) {
                    _reader.Dispose();
                    _reader=null;
                }
                base.Dispose();
            }

        }

        internal class AsciiLoader :InternalLoader
        {
            protected override VtkFileFormat FileFormat { get { return VtkFileFormat.Ascii; } }

            public override bool EndOfFile
            {
                get { return _reader==null||_reader.EndOfStream; }
            }

            protected override string GetLine()
            {
                return _reader.ReadLine();
            }

            protected override int GetIntData()
            {
                var tok = GetStringPart();
                return int.Parse(tok);
            }

            protected override float GetDataAsFloat(VtkDataType dataType)
            {
                var tok = GetStringPart();
                switch(dataType) {
                    case VtkDataType.Bit:
                        return (float)int.Parse(tok);
                        break;
                    case VtkDataType.UnsignedChar:
                        return (float)sbyte.Parse(tok);
                        break;
                    case VtkDataType.Char:
                        return (float)byte.Parse(tok);
                        break;
                    case VtkDataType.UnsignedShort:
                        return (float)ushort.Parse(tok);
                        break;
                    case VtkDataType.Short:
                        return (float)short.Parse(tok);
                        break;
                    case VtkDataType.UnsignedInt:
                        return (float)int.Parse(tok);
                        break;
                    case VtkDataType.Int:
                        return (float)int.Parse(tok);
                        break;
                    case VtkDataType.UnsignedLong:
                        return (float)ulong.Parse(tok);
                        break;
                    case VtkDataType.Long:
                        return (float)long.Parse(tok);
                        break;
                    case VtkDataType.Float:
                        return (float)float.Parse(tok);
                        break;
                    case VtkDataType.Double:
                        return (float)double.Parse(tok);
                        break;
                }
                return 0;
            }

            protected override int Peek()
            {
                return _reader.Peek();
            }

            private StreamReader _reader;
            public AsciiLoader(Stream baseStream) : base(baseStream)
            {
                _reader=new StreamReader(_baseStream, Encoding.ASCII);
            }

            public override void Dispose()
            {
                if(_reader!=null) {
                    _reader.Dispose();
                    _reader=null;
                }
                base.Dispose();
            }
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Export_glTF
{
  class RhinoGeometryGltfConverter
  {
    public RhinoGeometryGltfConverter(Rhino.DocObjects.RhinoObject rhinoObject, glTFExportOptions options, bool binary, gltfSchemaDummy dummy, List<byte> binaryBuffer)
    {
      RhinoObject = rhinoObject;
      Options = options;
      Binary = binary;
      Dummy = dummy;
      BinaryBuffer = binaryBuffer;
    }

    protected Rhino.DocObjects.RhinoObject RhinoObject
    {
      get;
      private set;
    } = null;

    protected glTFExportOptions Options
    {
      get;
      private set;
    } = null;

    protected bool Binary
    {
      get;
      private set;
    } = false;

    protected gltfSchemaDummy Dummy
    {
      get;
      private set;
    } = null;

    protected List<byte> BinaryBuffer
    {
      get;
      private set;
    } = null;

    protected int GetVertexAccessor(Rhino.Geometry.Point3d[] points)
    {
      int bufferViewIndex = GetBufferView(points, out Rhino.Geometry.Point3d min, out Rhino.Geometry.Point3d max, out int count);

      glTFLoader.Schema.Accessor accessor = new glTFLoader.Schema.Accessor()
      {
        BufferView = bufferViewIndex,
        ByteOffset = 0,
        ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT,
        Count = count,
        Min = min.ToFloatArray(),
        Max = max.ToFloatArray(),
        Type = glTFLoader.Schema.Accessor.TypeEnum.VEC3,
      };

      return Dummy.Accessors.AddAndReturnIndex(accessor);
    }

    private int GetBufferView(Rhino.Geometry.Point3d[] points, out Rhino.Geometry.Point3d min, out Rhino.Geometry.Point3d max, out int count)
    {
      int buffer = 0;
      int byteLength = 0;
      int byteOffset = 0;

      if (Binary)
      {
        byte[] bytes = GetVertexBytes(points, out min, out max);
        buffer = 0;
        byteLength = bytes.Length;
        byteOffset = BinaryBuffer.Count;
        BinaryBuffer.AddRange(bytes);
      }
      else
      {
        buffer = GetVertexBuffer(points, out min, out max, out byteLength);
      }

      glTFLoader.Schema.BufferView vertexBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = buffer,
        ByteOffset = byteOffset,
        ByteLength = byteLength,
        Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
      };

      count = points.Length;

      return Dummy.BufferViews.AddAndReturnIndex(vertexBufferView);
    }

    private int GetVertexBuffer(Rhino.Geometry.Point3d[] points, out Rhino.Geometry.Point3d min, out Rhino.Geometry.Point3d max, out int length)
    {
      byte[] bytes = GetVertexBytes(points, out min, out max);

      length = bytes.Length;

      glTFLoader.Schema.Buffer buffer = new glTFLoader.Schema.Buffer()
      {
        Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
        ByteLength = length,
      };

      return Dummy.Buffers.AddAndReturnIndex(buffer);
    }

    private byte[] GetVertexBytes(Rhino.Geometry.Point3d[] points, out Rhino.Geometry.Point3d min, out Rhino.Geometry.Point3d max)
    {
      min = new Rhino.Geometry.Point3d(Double.PositiveInfinity, Double.PositiveInfinity, Double.PositiveInfinity);
      max = new Rhino.Geometry.Point3d(Double.NegativeInfinity, Double.NegativeInfinity, Double.NegativeInfinity);

      float[] floats = new float[points.Length * 3];

      for (int i = 0; i < points.Length; i++)
      {
        Rhino.Geometry.Point3d vertex = points[i];

        floats[i * 3 + 0] = (float)vertex.X;
        floats[i * 3 + 1] = (float)vertex.Y;
        floats[i * 3 + 2] = (float)vertex.Z;

        min.X = Math.Min(min.X, vertex.X);
        max.X = Math.Max(max.X, vertex.X);

        min.Y = Math.Min(min.Y, vertex.Y);
        max.Y = Math.Max(max.Y, vertex.Y);

        min.Z = Math.Min(min.Z, vertex.Z);
        max.Z = Math.Max(max.Z, vertex.Z);
      }

      byte[] bytes = new byte[floats.Length * sizeof(float)];

      Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);

      return bytes;
    }
  }
}

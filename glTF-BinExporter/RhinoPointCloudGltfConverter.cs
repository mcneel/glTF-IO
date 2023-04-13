using Rhino.FileIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinExporter
{
  class RhinoPointCloudGltfConverter
  {
    public RhinoPointCloudGltfConverter(Rhino.DocObjects.RhinoObject rhinoObject, glTFExportOptions options, bool binary, gltfSchemaDummy dummy, List<byte> binaryBuffer)
    {
      this.rhinoObject = rhinoObject;
      this.options = options;
      this.binary = binary;
      this.dummy = dummy;
      this.binaryBuffer = binaryBuffer;
    }

    private Rhino.DocObjects.RhinoObject rhinoObject = null;
    private glTFExportOptions options = null;
    private bool binary = false;
    private gltfSchemaDummy dummy = null;
    private List<byte> binaryBuffer = null;


    public int AddPointCloud()
    {
      Rhino.Geometry.PointCloud pointCloud = rhinoObject.Geometry.Duplicate() as Rhino.Geometry.PointCloud;

      if (pointCloud == null)
      {
        return -1;
      }

      if (options.MapRhinoZToGltfY)
      {
        pointCloud.Transform(Constants.ZtoYUp);
      }

      int? vertexAccessorIdx = null;
      int? normalAccessorIdx = null;
      int? vertexColorAccessorIdx = null;

      glTFLoader.Schema.MeshPrimitive primitive = new glTFLoader.Schema.MeshPrimitive()
      {
        Mode = glTFLoader.Schema.MeshPrimitive.ModeEnum.POINTS,
        Attributes = new Dictionary<string, int>(),
      };

      if (options.UseDracoCompression)
      {
        glTFExtensions.KHR_draco_mesh_compression compression = DoDracoCompression(pointCloud, out vertexAccessorIdx, out normalAccessorIdx, out vertexColorAccessorIdx);

        primitive.Extensions = new Dictionary<string, object>
        {
          { glTFExtensions.KHR_draco_mesh_compression.Tag, compression }
        };
      }
      else
      {
        Rhino.Geometry.Point3d[] points = pointCloud.GetPoints();

        vertexAccessorIdx = GetVertexAccessor(points);

        if (ExportNormals(pointCloud))
        {
          Rhino.Geometry.Vector3d[] normals = pointCloud.GetNormals();

          normalAccessorIdx = GetNormalsAccessor(normals);
        }

        if (ExportVertexColors(pointCloud))
        {
          System.Drawing.Color[] colors = pointCloud.GetColors();

          vertexColorAccessorIdx = GetVertexColorAccessor(colors);
        }
      }

      if (vertexAccessorIdx == null)
      {
        return -1;
      }

      primitive.Attributes.Add(Constants.PositionAttributeTag, vertexAccessorIdx.Value);

      if(normalAccessorIdx != null)
      {
        primitive.Attributes.Add(Constants.NormalAttributeTag, normalAccessorIdx.Value);
      }

      if(vertexColorAccessorIdx != null)
      {
        primitive.Attributes.Add(Constants.VertexColorAttributeTag, vertexColorAccessorIdx.Value);
      }

      glTFLoader.Schema.Mesh mesh = new glTFLoader.Schema.Mesh()
      {
        Primitives = new glTFLoader.Schema.MeshPrimitive[] { primitive },
      };

      return dummy.Meshes.AddAndReturnIndex(mesh);
    }

    private int GetVertexAccessor(Rhino.Geometry.Point3d[] points)
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

      return dummy.Accessors.AddAndReturnIndex(accessor);
    }

    private int GetBufferView(Rhino.Geometry.Point3d[] points, out Rhino.Geometry.Point3d min, out Rhino.Geometry.Point3d max, out int count)
    {
      int buffer = 0;
      int byteLength = 0;
      int byteOffset = 0;

      if (binary)
      {
        byte[] bytes = GetVertexBytes(points, out min, out max);
        buffer = 0;
        byteLength = bytes.Length;
        byteOffset = binaryBuffer.Count;
        binaryBuffer.AddRange(bytes);
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

      return dummy.BufferViews.AddAndReturnIndex(vertexBufferView);
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

      return dummy.Buffers.AddAndReturnIndex(buffer);
    }

    private byte[] GetVertexBytes(Rhino.Geometry.Point3d[] points, out Rhino.Geometry.Point3d min, out Rhino.Geometry.Point3d max)
    {
      min = new Rhino.Geometry.Point3d(Double.PositiveInfinity, Double.PositiveInfinity, Double.PositiveInfinity);
      max = new Rhino.Geometry.Point3d(Double.NegativeInfinity, Double.NegativeInfinity, Double.NegativeInfinity);

      List<float> floats = new List<float>(points.Length * 3);

      foreach (Rhino.Geometry.Point3d vertex in points)
      {
        floats.AddRange(new float[] { (float)vertex.X, (float)vertex.Y, (float)vertex.Z });

        min.X = Math.Min(min.X, vertex.X);
        max.X = Math.Max(max.X, vertex.X);

        min.Y = Math.Min(min.Y, vertex.Y);
        max.Y = Math.Max(max.Y, vertex.Y);

        min.Z = Math.Min(min.Z, vertex.Z);
        max.Z = Math.Max(max.Z, vertex.Z);
      }

      IEnumerable<byte> bytesEnumerable = floats.SelectMany(value => BitConverter.GetBytes(value));

      return bytesEnumerable.ToArray();
    }

    private int GetVertexColorAccessor(System.Drawing.Color[] vertexColors)
    {
      int vertexColorsBufferViewIdx = GetVertexColorBufferView(vertexColors, out Rhino.Display.Color4f min, out Rhino.Display.Color4f max, out int countVertexColors);

      var type = options.UseDracoCompression ? glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_BYTE : glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT;

      glTFLoader.Schema.Accessor vertexColorAccessor = new glTFLoader.Schema.Accessor()
      {
        BufferView = vertexColorsBufferViewIdx,
        ByteOffset = 0,
        Count = countVertexColors,
        ComponentType = type,
        Min = min.ToFloatArray(),
        Max = max.ToFloatArray(),
        Type = glTFLoader.Schema.Accessor.TypeEnum.VEC4,
        Normalized = options.UseDracoCompression,
      };

      return dummy.Accessors.AddAndReturnIndex(vertexColorAccessor);
    }

    int GetVertexColorBufferView(System.Drawing.Color[] colors, out Rhino.Display.Color4f min, out Rhino.Display.Color4f max, out int countVertexColors)
    {
      int buffer = 0;
      int byteLength = 0;
      int byteOffset = 0;

      if (binary)
      {
        byte[] bytes = GetVertexColorBytes(colors, out min, out max);
        byteLength = bytes.Length;
        byteOffset = binaryBuffer.Count;
        binaryBuffer.AddRange(bytes);
      }
      else
      {
        buffer = GetVertexColorBuffer(colors, out min, out max, out byteLength);
      }

      glTFLoader.Schema.BufferView vertexColorsBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = buffer,
        ByteLength = byteLength,
        ByteOffset = byteOffset,
        Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
      };

      countVertexColors = colors.Length;

      return dummy.BufferViews.AddAndReturnIndex(vertexColorsBufferView);
    }

    int GetVertexColorBuffer(System.Drawing.Color[] colors, out Rhino.Display.Color4f min, out Rhino.Display.Color4f max, out int byteLength)
    {
      byte[] bytes = GetVertexColorBytes(colors, out min, out max);

      glTFLoader.Schema.Buffer vertexColorsBuffer = new glTFLoader.Schema.Buffer()
      {
        Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
        ByteLength = bytes.Length,
      };

      byteLength = bytes.Length;

      return dummy.Buffers.AddAndReturnIndex(vertexColorsBuffer);
    }

    byte[] GetVertexColorBytes(System.Drawing.Color[] colors, out Rhino.Display.Color4f min, out Rhino.Display.Color4f max)
    {
      float[] minArr = new float[] { float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity };
      float[] maxArr = new float[] { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };

      List<float> colorFloats = new List<float>(colors.Length * 4);

      for (int i = 0; i < colors.Length; i++)
      {
        Rhino.Display.Color4f color = new Rhino.Display.Color4f(colors[i]);

        colorFloats.AddRange(color.ToFloatArray());

        minArr[0] = Math.Min(minArr[0], color.R);
        minArr[1] = Math.Min(minArr[1], color.G);
        minArr[2] = Math.Min(minArr[2], color.B);
        minArr[3] = Math.Min(minArr[3], color.A);

        maxArr[0] = Math.Max(maxArr[0], color.R);
        maxArr[1] = Math.Max(maxArr[1], color.G);
        maxArr[2] = Math.Max(maxArr[2], color.B);
        maxArr[3] = Math.Max(maxArr[3], color.A);
      }

      min = new Rhino.Display.Color4f(minArr[0], minArr[1], minArr[2], minArr[3]);
      max = new Rhino.Display.Color4f(maxArr[0], maxArr[1], maxArr[2], maxArr[3]);

      IEnumerable<byte> bytesEnumerable = colorFloats.SelectMany(value => BitConverter.GetBytes(value));

      return bytesEnumerable.ToArray();
    }

    private int GetNormalsAccessor(Rhino.Geometry.Vector3d[] normals)
    {
      int normalsBufferIdx = GetNormalsBufferView(normals, out Rhino.Geometry.Vector3f min, out Rhino.Geometry.Vector3f max, out int normalsCount);

      glTFLoader.Schema.Accessor normalAccessor = new glTFLoader.Schema.Accessor()
      {
        BufferView = normalsBufferIdx,
        ByteOffset = 0,
        ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT,
        Count = normalsCount,
        Min = min.ToFloatArray(),
        Max = max.ToFloatArray(),
        Type = glTFLoader.Schema.Accessor.TypeEnum.VEC3,
      };

      return dummy.Accessors.AddAndReturnIndex(normalAccessor);
    }

    int GetNormalsBufferView(Rhino.Geometry.Vector3d[] normals, out Rhino.Geometry.Vector3f min, out Rhino.Geometry.Vector3f max, out int normalsCount)
    {
      int buffer = 0;
      int byteOffset = 0;
      int byteLength = 0;

      if (binary)
      {
        byte[] bytes = GetNormalsBytes(normals, out min, out max);
        byteLength = bytes.Length;
        byteOffset = binaryBuffer.Count;
        binaryBuffer.AddRange(bytes);
      }
      else
      {
        buffer = GetNormalsBuffer(normals, out min, out max, out byteLength);
      }

      glTFLoader.Schema.BufferView normalsBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = buffer,
        ByteLength = byteLength,
        ByteOffset = byteOffset,
        Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
      };

      normalsCount = normals.Length;

      return dummy.BufferViews.AddAndReturnIndex(normalsBufferView);
    }

    int GetNormalsBuffer(Rhino.Geometry.Vector3d[] normals, out Rhino.Geometry.Vector3f min, out Rhino.Geometry.Vector3f max, out int byteLength)
    {
      byte[] bytes = GetNormalsBytes(normals, out min, out max);

      byteLength = bytes.Length;

      glTFLoader.Schema.Buffer normalBuffer = new glTFLoader.Schema.Buffer()
      {
        Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
        ByteLength = bytes.Length,
      };

      return dummy.Buffers.AddAndReturnIndex(normalBuffer);
    }

    byte[] GetNormalsBytes(Rhino.Geometry.Vector3d[] normals, out Rhino.Geometry.Vector3f min, out Rhino.Geometry.Vector3f max)
    {
      min = new Rhino.Geometry.Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
      max = new Rhino.Geometry.Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

      //Preallocate
      List<float> floats = new List<float>(normals.Length * 3);

      foreach (Rhino.Geometry.Vector3f normal in normals)
      {
        floats.AddRange(new float[] { normal.X, normal.Y, normal.Z });

        min.X = Math.Min(min.X, normal.X);
        max.X = Math.Max(max.X, normal.X);

        min.Y = Math.Min(min.Y, normal.Y);
        max.Y = Math.Max(max.Y, normal.Y);

        max.Z = Math.Max(max.Z, normal.Z);
        min.Z = Math.Min(min.Z, normal.Z);
      }

      IEnumerable<byte> bytesEnumerable = floats.SelectMany(value => BitConverter.GetBytes(value));

      return bytesEnumerable.ToArray();
    }

    glTFExtensions.KHR_draco_mesh_compression DoDracoCompression(
      Rhino.Geometry.PointCloud pointCloud,
      out int? vertexAccessorIdx,
      out int? normalsAccessorIdx,
      out int? vertexColorAccessorIdx)
    {
      vertexAccessorIdx = null;
      normalsAccessorIdx = null;
      vertexColorAccessorIdx = null;

      bool exportNormals = ExportNormals(pointCloud);
      bool exportVertexColors = ExportVertexColors(pointCloud);

      DracoCompression dracoCompression = DracoCompression.Compress(
        pointCloud,
        new DracoCompressionOptions()
      {
          VertexColorFormat = DracoColorFormat.RGBA,
          CompressionLevel = options.DracoCompressionLevel,
          IncludeNormals = exportNormals,
          IncludeVertexColors = exportVertexColors,
          IncludeTextureCoordinates = false,
          PositionQuantizationBits = options.DracoQuantizationBitsPosition,
          NormalQuantizationBits = options.DracoQuantizationBitsNormal,
          TextureCoordintateQuantizationBits = options.DracoQuantizationBitsTexture
      });

      if(dracoCompression == null)
      {
        return null;
      }

      byte[] dracoBytes = dracoCompression.ToByteArray();

      int bufferIndex;
      int byteOffset;

      if (binary)
      {
        bufferIndex = 0;
        byteOffset = binaryBuffer.Count;
        binaryBuffer.AddRange(dracoBytes);
      }
      else
      {
        glTFLoader.Schema.Buffer buffer = new glTFLoader.Schema.Buffer()
        {
          ByteLength = dracoBytes.Length,
          Uri = Constants.TextBufferHeader + Convert.ToBase64String(dracoBytes),
        };

        bufferIndex = dummy.Buffers.AddAndReturnIndex(buffer);
        byteOffset = 0;
      }

      glTFLoader.Schema.BufferView dracoBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = bufferIndex,
        ByteOffset = byteOffset,
        ByteLength = dracoBytes.Length,
      };

      Rhino.Geometry.Point3d vertexMin = new Rhino.Geometry.Point3d(double.MaxValue, double.MaxValue, double.MaxValue);
      Rhino.Geometry.Point3d vertexMax = new Rhino.Geometry.Point3d(double.MinValue, double.MinValue, double.MinValue);

      Rhino.Geometry.Vector3f normalMin = new Rhino.Geometry.Vector3f(float.MaxValue, float.MaxValue, float.MaxValue);
      Rhino.Geometry.Vector3f normalMax = new Rhino.Geometry.Vector3f(float.MinValue, float.MinValue, float.MinValue);

      float[] vertexColorMin = new float[] { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue };
      float[] vertexColorMax = new float[] { float.MinValue, float.MinValue, float.MinValue, float.MinValue };

      for (int i = 0; i < pointCloud.Count; i++)
      {
        Rhino.Geometry.PointCloudItem p = pointCloud[i];

        vertexMin.X = Math.Min(p.X, vertexMin.X);
        vertexMin.Y = Math.Min(p.Y, vertexMin.Y);
        vertexMin.Z = Math.Min(p.Z, vertexMin.Z);

        vertexMax.X = Math.Max(p.X, vertexMax.X);
        vertexMax.Y = Math.Max(p.Y, vertexMax.Y);
        vertexMax.Z = Math.Max(p.Z, vertexMax.Z);

        normalMin.X = Math.Min((float)p.Normal.X, normalMin.X);
        normalMin.Y = Math.Min((float)p.Normal.Y, normalMin.Y);
        normalMin.Z = Math.Min((float)p.Normal.Z, normalMin.Z);

        normalMax.X = Math.Max((float)p.Normal.X, normalMax.X);
        normalMax.Y = Math.Max((float)p.Normal.Y, normalMax.Y);
        normalMax.Z = Math.Max((float)p.Normal.Z, normalMax.Z);

        Rhino.Display.Color4f col = new Rhino.Display.Color4f(p.Color); 

        vertexColorMin[0] = Math.Min(col.R, vertexColorMin[0]);
        vertexColorMin[1] = Math.Min(col.G, vertexColorMin[1]);
        vertexColorMin[2] = Math.Min(col.B, vertexColorMin[2]);
        vertexColorMin[3] = Math.Min(col.A, vertexColorMin[3]);

        vertexColorMax[0] = Math.Max(col.R, vertexColorMax[0]);
        vertexColorMax[1] = Math.Max(col.G, vertexColorMax[1]);
        vertexColorMax[2] = Math.Max(col.B, vertexColorMax[2]);
        vertexColorMax[3] = Math.Max(col.A, vertexColorMax[3]);
      }

      int dracoBufferViewIndex = dummy.BufferViews.AddAndReturnIndex(dracoBufferView);

      glTFExtensions.KHR_draco_mesh_compression extension = new glTFExtensions.KHR_draco_mesh_compression();

      extension.BufferView = dracoBufferViewIndex;
      extension.Attributes.Add(Constants.PositionAttributeTag, dracoCompression.VertexAttributePosition);

      glTFLoader.Schema.Accessor vertexAccessor = new glTFLoader.Schema.Accessor()
      {
        BufferView = null,
        Count = pointCloud.Count,
        Min = vertexMin.ToFloatArray(),
        Max = vertexMax.ToFloatArray(),
        Type = glTFLoader.Schema.Accessor.TypeEnum.VEC3,
        ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT,
        ByteOffset = 0,
      };

      vertexAccessorIdx = dummy.Accessors.AddAndReturnIndex(vertexAccessor);

      if(exportNormals)
      {
        extension.Attributes.Add(Constants.NormalAttributeTag, dracoCompression.NormalAttributePosition);

        glTFLoader.Schema.Accessor normalsAccessor = new glTFLoader.Schema.Accessor()
        {
          BufferView = null,
          Count = pointCloud.Count,
          Min = normalMin.ToFloatArray(),
          Max = normalMax.ToFloatArray(),
          Type = glTFLoader.Schema.Accessor.TypeEnum.VEC3,
          ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT,
          ByteOffset = 0,
        };

        normalsAccessorIdx = dummy.Accessors.AddAndReturnIndex(normalsAccessor);
      }

      if (exportVertexColors)
      {
        extension.Attributes.Add(Constants.VertexColorAttributeTag, dracoCompression.VertexColorAttributePosition);

        glTFLoader.Schema.Accessor vertexColorAccessor = new glTFLoader.Schema.Accessor()
        {
          BufferView = null,
          Count = pointCloud.Count,
          Min = vertexColorMin,
          Max = vertexColorMax,
          Type = glTFLoader.Schema.Accessor.TypeEnum.VEC4,
          ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_BYTE,
          Normalized = true,
          ByteOffset = 0,
        };

        vertexColorAccessorIdx = dummy.Accessors.AddAndReturnIndex(vertexColorAccessor);
      }

      return extension;
    }

    private bool ExportNormals(Rhino.Geometry.PointCloud pointCloud)
    {
      return options.ExportVertexNormals && pointCloud.ContainsNormals;
    }

    private bool ExportVertexColors(Rhino.Geometry.PointCloud pointCloud)
    {
      return options.ExportVertexColors && pointCloud.ContainsColors;
    }
  }
}

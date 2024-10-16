using Rhino.Display;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Export_glTF
{
  internal class TextureCoordinateInfo
  {
    /// <summary>
    /// Channel id is the key, texture coordinates are the value
    /// </summary>
    public Dictionary<int, Rhino.Geometry.Point2f[]> TextureMappings;

    public int WcsMappingChannelId = -1;
    public int WcsBoxMappingChannelId = -1;
  }

  class RhinoMeshGltfConverter
  {
    public RhinoMeshGltfConverter(RhinoDocGltfConverter converter, ObjectExportData exportData, FileGltfWriteOptions options, bool binary, gltfSchemaDummy dummy, List<byte> binaryBuffer)
    {
      this.converter = converter;
      this.exportData = exportData;
      this.options = options;
      this.binary = binary;
      this.dummy = dummy;
      this.binaryBuffer = binaryBuffer;
    }

    private RhinoDocGltfConverter converter = null;
    private ObjectExportData exportData = null;
    private FileGltfWriteOptions options = null;
    private bool binary = false;
    private gltfSchemaDummy dummy = null;
    private List<byte> binaryBuffer = null;

    private DracoGeometryInfo currentGeometryInfo = null;

    public int AddMesh()
    {
      List<glTFLoader.Schema.MeshPrimitive> primitives = GetPrimitives();

      glTFLoader.Schema.Mesh mesh = new glTFLoader.Schema.Mesh()
      {
        Primitives = primitives.ToArray(),
      };

      return dummy.Meshes.AddAndReturnIndex(mesh);
    }

    private void PreprocessMesh(Mesh rhinoMesh)
    {
      Transform transform = converter.DocumentToGltfScale;

      if (options.MapZToY)
      {
        transform = transform * Constants.ZtoYUp;
      }

      rhinoMesh.Transform(transform);
    }

    TextureCoordinateInfo GetTexCoords(Mesh mesh, Rhino.DocObjects.Material material)
    {
      Dictionary<int, Point2f[]> dict = new Dictionary<int, Point2f[]>();

      int[] channels = exportData.Object.GetTextureChannels();

      int max = -1;

      if (channels == null || channels.Length == 0)
      {
        if (mesh.TextureCoordinates.Count > 0) //Only if there are texture coordinates to export
        {
          dict.Add(1, mesh.TextureCoordinates.ToArray());
          max = 1;
        }
      }
      else
      {
        foreach (int channel in channels)
        {
          Rhino.Render.TextureMapping mapping = exportData.Object.GetTextureMapping(channel);

          if (mapping == null)
          {
            continue;
          }

          Rhino.Render.CachedTextureCoordinates tc = mesh.GetCachedTextureCoordinates(mapping.Id);

          if (tc == null) //No need to set if null, only the mappings used by the material are set in the cached texture coordinates
          {
            continue;
          }

          dict.Add(channel, ToTextureCoordinateList(tc));
          max = Math.Max(channel, max);
        }
      }

      TextureCoordinateInfo rc = new TextureCoordinateInfo();

      foreach (var texture in material.GetTextures())
      {
        if(texture.WcsProjected && rc.WcsMappingChannelId == -1)
        {
          var wcsCached = mesh.GetCachedTextureCoordinates(exportData.Object, texture);

          if(wcsCached != null)
          {
            max += 1;

            dict.Add(max, ToTextureCoordinateList(wcsCached));
            rc.WcsMappingChannelId = max;
          }
        }
        else if(texture.WcsBoxProjected && rc.WcsBoxMappingChannelId == -1)
        {
          var wcsBoxCached = mesh.GetCachedTextureCoordinates(exportData.Object, texture);

          if (wcsBoxCached != null)
          {
            max += 1;

            dict.Add(max, ToTextureCoordinateList(wcsBoxCached));
            rc.WcsBoxMappingChannelId = max;
          }
        }
      }
      
      rc.TextureMappings = dict;

      return rc;
    }

    private List<glTFLoader.Schema.MeshPrimitive> GetPrimitives()
    {
      List<glTFLoader.Schema.MeshPrimitive> primitives = new List<glTFLoader.Schema.MeshPrimitive>();

      foreach (MeshMaterialPair meshMaterialPair in exportData.Meshes)
      {
        Mesh rhinoMesh = meshMaterialPair.Mesh;

        if(meshMaterialPair.Material != null)
        {
          rhinoMesh.SetCachedTextureCoordinatesFromMaterial(exportData.Object, meshMaterialPair.Material);
        }

        //TexCoords need retrieved and cached before preprocessing the mesh
        //So mapping transform accounts for the Z to Y Up transform and unit scale conversion
        TextureCoordinateInfo textureCoordinates = GetTexCoords(rhinoMesh, meshMaterialPair.Material);

        PreprocessMesh(rhinoMesh);

        if (options.UseDracoCompression)
        {
          if (!SetDracoGeometryInfo(rhinoMesh, textureCoordinates.TextureMappings))
          {
            continue;
          }
        }

        bool exportNormals = ExportNormals(rhinoMesh);
        bool exportTextureCoordinates = ExportTextureCoordinates(textureCoordinates.TextureMappings);
        bool exportVertexColors = ExportVertexColors(rhinoMesh);

        glTFLoader.Schema.MeshPrimitive primitive = new glTFLoader.Schema.MeshPrimitive()
        {
          Attributes = new Dictionary<string, int>(),
        };

        int vertexAccessorIdx = GetVertexAccessor(rhinoMesh.Vertices);

        primitive.Attributes.Add(Constants.PositionAttributeTag, vertexAccessorIdx);

        int indicesAccessorIdx = GetIndicesAccessor(rhinoMesh.Faces, rhinoMesh.Vertices.Count);

        primitive.Indices = indicesAccessorIdx;

        if (exportNormals)
        {
          int normalsAccessorIdx = GetNormalsAccessor(rhinoMesh.Normals);

          primitive.Attributes.Add(Constants.NormalAttributeTag, normalsAccessorIdx);
        }

        Dictionary<int, int> mappingChannelToTexCoordIdx = new Dictionary<int, int>();

        if (exportTextureCoordinates)
        {
          int texCoordIdx = 0;
          foreach (var textureCoordinatePair in textureCoordinates.TextureMappings)
          {
            int textureCoordinatesAccessorIdx = GetTextureCoordinatesAccessor(textureCoordinatePair.Value);

            string tag = Constants.TexCoordAttributeTagStem + texCoordIdx.ToString();

            primitive.Attributes.Add(tag, textureCoordinatesAccessorIdx);

            mappingChannelToTexCoordIdx.Add(textureCoordinatePair.Key, texCoordIdx);

            texCoordIdx++;
          }
        }

        if (exportVertexColors)
        {
          int vertexColorsAccessorIdx = GetVertexColorAccessor(rhinoMesh.VertexColors);

          primitive.Attributes.Add(Constants.VertexColorAttributeTag, vertexColorsAccessorIdx);
        }

        if (options.UseDracoCompression)
        {
          glTFExtensions.KHR_draco_mesh_compression dracoCompressionObject = new glTFExtensions.KHR_draco_mesh_compression();

          dracoCompressionObject.BufferView = currentGeometryInfo.BufferViewIndex;

          dracoCompressionObject.Attributes.Add(Constants.PositionAttributeTag, currentGeometryInfo.VertexAttributePosition);

          if (exportNormals)
          {
            dracoCompressionObject.Attributes.Add(Constants.NormalAttributeTag, currentGeometryInfo.NormalAttributePosition);
          }

          if (exportTextureCoordinates)
          {
            dracoCompressionObject.Attributes.Add(Constants.TexCoord0AttributeTag, currentGeometryInfo.TextureCoordinatesAttributePosition);
          }

          if (exportVertexColors)
          {
            dracoCompressionObject.Attributes.Add(Constants.VertexColorAttributeTag, currentGeometryInfo.VertexColorAttributePosition);
          }

          primitive.Extensions = new Dictionary<string, object>
          {
            { glTFExtensions.KHR_draco_mesh_compression.Tag, dracoCompressionObject }
          };
        }

        TextureCoordinateMappingInfo info = new TextureCoordinateMappingInfo()
        {
          RhinoChannelToTexCoordsIdx = mappingChannelToTexCoordIdx,
          WcsMappingChannelId = textureCoordinates.WcsMappingChannelId,
          WcsBoxMappingChannelId = textureCoordinates.WcsBoxMappingChannelId,
        };

        primitive.Material = converter.GetMaterial(meshMaterialPair, info, exportData.Object);

        primitives.Add(primitive);
      }

      return primitives;
    }

    private bool ExportNormals(Mesh rhinoMesh)
    {
      return rhinoMesh.Normals.Count > 0 && options.ExportVertexNormals;
    }

    private bool ExportTextureCoordinates(Dictionary<int, Point2f[]>  textureCoordinates)
    {
      return textureCoordinates.Count > 0 && options.ExportTextureCoordinates;
    }

    private bool ExportVertexColors(Mesh rhinoMesh)
    {
      return rhinoMesh.VertexColors.Count > 0 && options.ExportVertexColors;
    }

    private bool SetDracoGeometryInfo(Mesh rhinoMesh, Dictionary<int, Point2f[]> textureCoordinates)
    {
      var dracoComp = DracoCompression.Compress(
          rhinoMesh,
          new DracoCompressionOptions()
          {
            VertexColorFormat = DracoColorFormat.RGBA,
            CompressionLevel = options.DracoCompressionLevel,
            IncludeNormals = ExportNormals(rhinoMesh),
            IncludeTextureCoordinates = ExportTextureCoordinates(textureCoordinates),
            IncludeVertexColors = ExportVertexColors(rhinoMesh),
            PositionQuantizationBits = options.DracoQuantizationBitsPosition,
            NormalQuantizationBits = options.DracoQuantizationBitsNormal,
            TextureCoordintateQuantizationBits = options.DracoQuantizationBitsTextureCoordinate
          }
      );

      currentGeometryInfo = AddDracoGeometry(dracoComp);

      return currentGeometryInfo.Success;
    }


    private int GetVertexAccessor(MeshVertexList vertices)
    {
      int? vertexBufferViewIdx = GetVertexBufferView(vertices, out Point3d min, out Point3d max, out int countVertices);

      glTFLoader.Schema.Accessor vertexAccessor = new glTFLoader.Schema.Accessor()
      {
        BufferView = vertexBufferViewIdx,
        ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT,
        Count = countVertices,
        Min = min.ToFloatArray(),
        Max = max.ToFloatArray(),
        Type = glTFLoader.Schema.Accessor.TypeEnum.VEC3,
        ByteOffset = 0,
      };

      return dummy.Accessors.AddAndReturnIndex(vertexAccessor);
    }

    private int? GetVertexBufferView(MeshVertexList vertices, out Point3d min, out Point3d max, out int countVertices)
    {
      if (options.UseDracoCompression)
      {
        min = currentGeometryInfo.VerticesMin;
        max = currentGeometryInfo.VerticesMax;
        countVertices = currentGeometryInfo.VerticesCount;
        return null;
      }

      int buffer = 0;
      int byteLength = 0;
      int byteOffset = 0;

      if (binary)
      {
        byte[] bytes = GetVertexBytes(vertices, out min, out max);
        buffer = 0;
        byteLength = bytes.Length;
        byteOffset = binaryBuffer.Count;
        binaryBuffer.AddRange(bytes);
      }
      else
      {
        buffer = GetVertexBuffer(vertices, out min, out max, out byteLength);
      }

      glTFLoader.Schema.BufferView vertexBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = buffer,
        ByteOffset = byteOffset,
        ByteLength = byteLength,
        Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
      };

      countVertices = vertices.Count;

      return dummy.BufferViews.AddAndReturnIndex(vertexBufferView);
    }

    private int GetVertexBuffer(MeshVertexList vertices, out Point3d min, out Point3d max, out int length)
    {
      byte[] bytes = GetVertexBytes(vertices, out min, out max);

      length = bytes.Length;

      glTFLoader.Schema.Buffer buffer = new glTFLoader.Schema.Buffer()
      {
        Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
        ByteLength = length,
      };

      return dummy.Buffers.AddAndReturnIndex(buffer);
    }

    private byte[] GetVertexBytes(MeshVertexList vertices, out Point3d min, out Point3d max)
    {
      min = new Point3d(Double.PositiveInfinity, Double.PositiveInfinity, Double.PositiveInfinity);
      max = new Point3d(Double.NegativeInfinity, Double.NegativeInfinity, Double.NegativeInfinity);

      float[] floats = new float[vertices.Count * 3];
      
      for (int i = 0; i < vertices.Count; i++)
      {
        Point3d vertex = vertices[i];

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

      byte[] bytes = new byte[sizeof(float) * floats.Length];

      Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);

      return bytes;
    }

    private int GetIndicesAccessor(MeshFaceList faces, int verticesCount)
    {
      int? indicesBufferViewIdx = GetIndicesBufferView(faces, verticesCount, out float min, out float max, out int indicesCount);

      glTFLoader.Schema.Accessor indicesAccessor = new glTFLoader.Schema.Accessor()
      {
        BufferView = indicesBufferViewIdx,
        Count = indicesCount,
        Min = new float[] { min },
        Max = new float[] { max },
        Type = glTFLoader.Schema.Accessor.TypeEnum.SCALAR,
        ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_INT,
        ByteOffset = 0,
      };

      return dummy.Accessors.AddAndReturnIndex(indicesAccessor);
    }

    private int? GetIndicesBufferView(MeshFaceList faces, int verticesCount, out float min, out float max, out int indicesCount)
    {
      if (options.UseDracoCompression)
      {
        min = currentGeometryInfo.IndicesMin;
        max = currentGeometryInfo.IndicesMax;
        indicesCount = currentGeometryInfo.IndicesCount;
        return null;
      }

      int bufferIndex = 0;
      int byteOffset = 0;
      int byteLength = 0;

      if (binary)
      {
        byte[] bytes = GetIndicesBytes(faces, out indicesCount);
        byteLength = bytes.Length;
        byteOffset = binaryBuffer.Count;
        binaryBuffer.AddRange(bytes);
      }
      else
      {
        bufferIndex = GetIndicesBuffer(faces, out indicesCount, out byteLength);
      }

      glTFLoader.Schema.BufferView indicesBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = bufferIndex,
        ByteOffset = byteOffset,
        ByteLength = byteLength,
        Target = glTFLoader.Schema.BufferView.TargetEnum.ELEMENT_ARRAY_BUFFER,
      };

      min = 0;
      max = verticesCount - 1;

      return dummy.BufferViews.AddAndReturnIndex(indicesBufferView);
    }

    private int GetIndicesBuffer(MeshFaceList faces, out int indicesCount, out int byteLength)
    {
      byte[] bytes = GetIndicesBytes(faces, out indicesCount);

      byteLength = bytes.Length;

      glTFLoader.Schema.Buffer indicesBuffer = new glTFLoader.Schema.Buffer()
      {
        Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
        ByteLength = bytes.Length,
      };

      return dummy.Buffers.AddAndReturnIndex(indicesBuffer);
    }

    private byte[] GetIndicesBytes(MeshFaceList faces, out int indicesCount)
    {
      List<uint> faceIndices = new List<uint>(faces.Count * 3);
      
      foreach (MeshFace face in faces)
      {
        if (face.IsTriangle)
        {
          faceIndices.AddRange(new uint[] { (uint)face.A, (uint)face.B, (uint)face.C });
        }
        else
        {
          //Triangulate
          faceIndices.AddRange(new uint[] { (uint)face.A, (uint)face.B, (uint)face.C, (uint)face.A, (uint)face.C, (uint)face.D });
        }
      }

      uint[] indices = faceIndices.ToArray();

      indicesCount = indices.Length;

      byte[] bytes = new byte[indices.Length * sizeof(uint)];

      Buffer.BlockCopy(indices, 0, bytes, 0, bytes.Length);

      return bytes;
    }

    private int GetNormalsAccessor(MeshVertexNormalList normals)
    {
      int? normalsBufferIdx = GetNormalsBufferView(normals, out Vector3f min, out Vector3f max, out int normalsCount);

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

    int? GetNormalsBufferView(MeshVertexNormalList normals, out Vector3f min, out Vector3f max, out int normalsCount)
    {
      if (options.UseDracoCompression)
      {
        min = currentGeometryInfo.NormalsMin;
        max = currentGeometryInfo.NormalsMax;
        normalsCount = currentGeometryInfo.NormalsCount;
        return null;
      }

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

      normalsCount = normals.Count;

      return dummy.BufferViews.AddAndReturnIndex(normalsBufferView);
    }

    int GetNormalsBuffer(MeshVertexNormalList normals, out Vector3f min, out Vector3f max, out int byteLength)
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

    byte[] GetNormalsBytes(MeshVertexNormalList normals, out Vector3f min, out Vector3f max)
    {
      min = new Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
      max = new Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

      //Preallocate
      float[] floats = new float[normals.Count * 3];

      for(int i = 0; i < normals.Count; i++)
      {
        Vector3f normal = normals[i];

        floats[i * 3 + 0] = normal.X;
        floats[i * 3 + 1] = normal.Y;
        floats[i * 3 + 2] = normal.Z;

        min.X = Math.Min(min.X, normal.X);
        max.X = Math.Max(max.X, normal.X);

        min.Y = Math.Min(min.Y, normal.Y);
        max.Y = Math.Max(max.Y, normal.Y);

        max.Z = Math.Max(max.Z, normal.Z);
        min.Z = Math.Min(min.Z, normal.Z);
      }

      byte[] bytes = new byte[floats.Length * sizeof(float)];

      Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);

      return bytes;
    }

    int GetTextureCoordinatesAccessor(Point2f[] textureCoordinates)
    {
      int? textureCoordinatesBufferViewIdx = GetTextureCoordinatesBufferView(textureCoordinates, out Point2f min, out Point2f max, out int countCoordinates);

      glTFLoader.Schema.Accessor textureCoordinatesAccessor = new glTFLoader.Schema.Accessor()
      {
        BufferView = textureCoordinatesBufferViewIdx,
        ByteOffset = 0,
        ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT,
        Count = countCoordinates,
        Min = min.ToFloatArray(),
        Max = max.ToFloatArray(),
        Type = glTFLoader.Schema.Accessor.TypeEnum.VEC2,
      };

      return dummy.Accessors.AddAndReturnIndex(textureCoordinatesAccessor);
    }

    int? GetTextureCoordinatesBufferView(Point2f[] textureCoordinates, out Point2f min, out Point2f max, out int countCoordinates)
    {
      if (options.UseDracoCompression)
      {
        min = currentGeometryInfo.TexCoordsMin;
        max = currentGeometryInfo.TexCoordsMax;
        countCoordinates = currentGeometryInfo.TexCoordsCount;
        return null;
      }

      int buffer = 0;
      int byteLength = 0;
      int byteOffset = 0;

      if (binary)
      {
        byte[] bytes = GetTextureCoordinatesBytes(textureCoordinates, out min, out max);
        byteLength = bytes.Length;
        byteOffset = binaryBuffer.Count;
        binaryBuffer.AddRange(bytes);
      }
      else
      {
        buffer = GetTextureCoordinatesBuffer(textureCoordinates, out min, out max, out byteLength);
      }

      glTFLoader.Schema.BufferView textureCoordinatesBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = buffer,
        ByteLength = byteLength,
        ByteOffset = byteOffset,
        Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
      };

      countCoordinates = textureCoordinates.Length;

      return dummy.BufferViews.AddAndReturnIndex(textureCoordinatesBufferView);
    }

    int GetTextureCoordinatesBuffer(Point2f[] textureCoordinates, out Point2f min, out Point2f max, out int byteLength)
    {
      byte[] bytes = GetTextureCoordinatesBytes(textureCoordinates, out min, out max);

      glTFLoader.Schema.Buffer textureCoordinatesBuffer = new glTFLoader.Schema.Buffer()
      {
        Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
        ByteLength = bytes.Length,
      };

      byteLength = bytes.Length;

      return dummy.Buffers.AddAndReturnIndex(textureCoordinatesBuffer);
    }

    private byte[] GetTextureCoordinatesBytes(Point2f[] textureCoordinates, out Point2f min, out Point2f max)
    {
      min = new Point2f(float.PositiveInfinity, float.PositiveInfinity);
      max = new Point2f(float.NegativeInfinity, float.NegativeInfinity);

      float[] coordinates = new float[textureCoordinates.Length * 2];

      for(int i = 0; i < textureCoordinates.Length; i++)
      {
        Point2f coordinate = textureCoordinates[i];

        coordinates[i * 2 + 0] = coordinate.X;
        coordinates[i * 2 + 1] = coordinate.Y;

        min.X = Math.Min(min.X, coordinate.X);
        max.X = Math.Max(max.X, coordinate.X);

        min.Y = Math.Min(min.Y, coordinate.Y);
        max.Y = Math.Max(max.Y, coordinate.Y);
      }

      byte[] bytes = new byte[coordinates.Length * sizeof(float)];

      Buffer.BlockCopy(coordinates, 0, bytes, 0, bytes.Length);

      return bytes;
    }

    private int GetVertexColorAccessor(MeshVertexColorList vertexColors)
    {
      int? vertexColorsBufferViewIdx = GetVertexColorBufferView(vertexColors, out Color4f min, out Color4f max, out int countVertexColors);

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

    int? GetVertexColorBufferView(MeshVertexColorList vertexColors, out Color4f min, out Color4f max, out int countVertexColors)
    {
      if (options.UseDracoCompression)
      {
        min = currentGeometryInfo.VertexColorMin;
        max = currentGeometryInfo.VertexColorMax;
        countVertexColors = currentGeometryInfo.VertexColorCount;
        return null;
      }

      int buffer = 0;
      int byteLength = 0;
      int byteOffset = 0;

      if (binary)
      {
        byte[] bytes = GetVertexColorBytes(vertexColors, out min, out max);
        byteLength = bytes.Length;
        byteOffset = binaryBuffer.Count;
        binaryBuffer.AddRange(bytes);
      }
      else
      {
        buffer = GetVertexColorBuffer(vertexColors, out min, out max, out byteLength);
      }

      glTFLoader.Schema.BufferView vertexColorsBufferView = new glTFLoader.Schema.BufferView()
      {
        Buffer = buffer,
        ByteLength = byteLength,
        ByteOffset = byteOffset,
        Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
      };

      countVertexColors = vertexColors.Count;

      return dummy.BufferViews.AddAndReturnIndex(vertexColorsBufferView);
    }

    int GetVertexColorBuffer(MeshVertexColorList vertexColors, out Color4f min, out Color4f max, out int byteLength)
    {
      byte[] bytes = GetVertexColorBytes(vertexColors, out min, out max);

      glTFLoader.Schema.Buffer vertexColorsBuffer = new glTFLoader.Schema.Buffer()
      {
        Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
        ByteLength = bytes.Length,
      };

      byteLength = bytes.Length;

      return dummy.Buffers.AddAndReturnIndex(vertexColorsBuffer);
    }

    byte[] GetVertexColorBytes(MeshVertexColorList vertexColors, out Color4f min, out Color4f max)
    {
      float[] minArr = new float[] { float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity };
      float[] maxArr = new float[] { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };

      float[] colors = new float[vertexColors.Count * 4];

      for (int i = 0; i < vertexColors.Count; i++)
      {
        Color4f color = new Color4f(vertexColors[i]);

        colors[i * 4 + 0] = color.R;
        colors[i * 4 + 1] = color.G;
        colors[i * 4 + 2] = color.B;
        colors[i * 4 + 3] = color.A;

        minArr[0] = Math.Min(minArr[0], color.R);
        minArr[1] = Math.Min(minArr[1], color.G);
        minArr[2] = Math.Min(minArr[2], color.B);
        minArr[3] = Math.Min(minArr[3], color.A);

        maxArr[0] = Math.Max(maxArr[0], color.R);
        maxArr[1] = Math.Max(maxArr[1], color.G);
        maxArr[2] = Math.Max(maxArr[2], color.B);
        maxArr[3] = Math.Max(maxArr[3], color.A);
      }

      min = new Color4f(minArr[0], minArr[1], minArr[2], minArr[3]);
      max = new Color4f(maxArr[0], maxArr[1], maxArr[2], maxArr[3]);

      byte[] bytes = new byte[colors.Length * sizeof(float)];

      Buffer.BlockCopy(colors, 0, bytes, 0, bytes.Length);

      return bytes;
    }

    public DracoGeometryInfo AddDracoGeometry(DracoCompression dracoCompression)
    {
      var dracoGeoInfo = new DracoGeometryInfo();

      try
      {
        dracoGeoInfo.VertexAttributePosition = dracoCompression.VertexAttributePosition;
        dracoGeoInfo.NormalAttributePosition = dracoCompression.NormalAttributePosition;
        dracoGeoInfo.TextureCoordinatesAttributePosition = dracoCompression.TextureCoordinatesAttributePosition;
        dracoGeoInfo.VertexColorAttributePosition = dracoCompression.VertexColorAttributePosition;

        byte[] dracoBytes = dracoCompression.ToByteArray();

        WriteDracoBytes(dracoBytes, out dracoGeoInfo.BufferIndex, out dracoGeoInfo.ByteOffset, out dracoGeoInfo.ByteLength);

        glTFLoader.Schema.BufferView compMeshBufferView = new glTFLoader.Schema.BufferView()
        {
          Buffer = dracoGeoInfo.BufferIndex,
          ByteOffset = dracoGeoInfo.ByteOffset,
          ByteLength = dracoGeoInfo.ByteLength,
        };

        dracoGeoInfo.BufferViewIndex = dummy.BufferViews.AddAndReturnIndex(compMeshBufferView);

        dracoGeoInfo.ByteLength = dracoBytes.Length;

        var geo = DracoCompression.DecompressByteArray(dracoBytes);
        if (geo.ObjectType == Rhino.DocObjects.ObjectType.Mesh)
        {
          var mesh = (Rhino.Geometry.Mesh)geo;

          // Vertices Stats
          dracoGeoInfo.VerticesCount = mesh.Vertices.Count;
          dracoGeoInfo.VerticesMin = new Point3d(mesh.Vertices.Min());
          dracoGeoInfo.VerticesMax = new Point3d(mesh.Vertices.Max());

          dracoGeoInfo.IndicesCount = mesh.Faces.TriangleCount;
          dracoGeoInfo.IndicesMin = 0;
          dracoGeoInfo.IndicesMax = dracoGeoInfo.VerticesCount - 1;

          dracoGeoInfo.NormalsCount = mesh.Normals.Count;
          if(dracoGeoInfo.NormalsCount > 0)
          {
            dracoGeoInfo.NormalsMin = mesh.Normals.Min();
            dracoGeoInfo.NormalsMax = mesh.Normals.Max();
          }

          // TexCoord Stats
          dracoGeoInfo.TexCoordsCount = mesh.TextureCoordinates.Count;
          if (dracoGeoInfo.TexCoordsCount > 0)
          {
            dracoGeoInfo.TexCoordsMin = mesh.TextureCoordinates.Min();
            dracoGeoInfo.TexCoordsMax = mesh.TextureCoordinates.Max();
          }

          dracoGeoInfo.VertexColorCount = mesh.VertexColors.Count;
          dracoGeoInfo.VertexColorMin = Color4f.Black;
          dracoGeoInfo.VertexColorMax = Color4f.White;

          dracoGeoInfo.Success = true;
        }
        geo.Dispose();
        dracoCompression.Dispose();
      }
      catch(Exception) {  }

      return dracoGeoInfo;
    }

    private byte[] GetDracoBytes(string fileName)
    {
      using (FileStream stream = File.Open(fileName, FileMode.Open))
      {
        var bytes = new byte[stream.Length];
        stream.Read(bytes, 0, (int)stream.Length);

        return bytes;
      }
    }

    public void WriteDracoBytes(byte[] bytes, out int bufferIndex, out int byteOffset, out int byteLength)
    {
      byteLength = bytes.Length;

      if (binary)
      {
        byteOffset = (int)binaryBuffer.Count;
        binaryBuffer.AddRange(bytes);
        bufferIndex = 0;
      }
      else
      {
        glTFLoader.Schema.Buffer buffer = new glTFLoader.Schema.Buffer()
        {
          Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
          ByteLength = bytes.Length,
        };
        bufferIndex = dummy.Buffers.AddAndReturnIndex(buffer);
        byteOffset = 0;
      }
    }

    Point2f[] ToTextureCoordinateList(Rhino.Render.CachedTextureCoordinates ct)
    {
      Point2f[] pts = new Point2f[ct.Count];
      
      Parallel.For(0, ct.Count, i => {
        Point3d pt = ct[i];
        pts[i] = new Point2f(pt.X, pt.Y);
      });

      return pts.ToArray();
    }

  }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Import_glTF
{
  class GltfRhinoConverter
  {
    public GltfRhinoConverter(glTFLoader.Schema.Gltf gltf, Rhino.RhinoDoc doc, string path)
    {
      this.gltf = gltf;
      this.doc = doc;

      this.path = path;
      directory = Path.GetDirectoryName(path);
      filename = Path.GetFileName(path);
      filenameNoExtension = Path.GetFileNameWithoutExtension(path);
      extension = Path.GetExtension(path);

      binaryFile = extension.ToLower() == ".glb";

      double scaleFactor = Rhino.RhinoMath.UnitScale(Rhino.UnitSystem.Meters, doc.ModelUnitSystem);
      GltfToDocumentScale = Rhino.Geometry.Transform.Scale(Rhino.Geometry.Point3d.Origin, scaleFactor);
    }

    glTFLoader.Schema.Gltf gltf = null;
    Rhino.RhinoDoc doc = null;

    string path = "";
    string directory = "";
    string filename = "";
    string filenameNoExtension = "";
    string extension = "";

    bool binaryFile = false;

    List<byte[]> buffers = new List<byte[]>();

    List<Rhino.Render.RenderMaterial> materials = new List<Rhino.Render.RenderMaterial>();

    List<GltfMeshHolder> meshHolders = new List<GltfMeshHolder>();

    HashSet<string> Names = new HashSet<string>();

    int nameCounter = 0;

    public readonly Rhino.Geometry.Transform GltfToDocumentScale;

    List<ImageHolder> images = new List<ImageHolder>();

    public string GetUniqueName(string name)
    {
      if (string.IsNullOrEmpty(name))
      {
        name = "Unnamed";
      }

      while (Names.Contains(name))
      {
        name = name + "-" + nameCounter.ToString();
        nameCounter++;
      }

      Names.Add(name);

      return name;
    }

    public string GetUnpackedTexturePath()
    {
      string root = Rhino.Render.Utilities.GetUnpackedFilesCacheFolder(doc, true);
      string full = Path.Combine(root, filenameNoExtension);

      if(!Directory.Exists(full))
      {
        Directory.CreateDirectory(full);
      }

      return full;
    }

    public bool Convert()
    {
      for (int i = 0; i < gltf.Buffers.Length; i++)
      {
        buffers.Add(glTFLoader.Interface.LoadBinaryBuffer(gltf, i, path));
      }

      if (gltf.Images != null)
      {
        for (int i = 0; i < gltf.Images.Length; i++)
        {
          Stream stream = glTFLoader.Interface.OpenImageFile(gltf, i, path);

          System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(stream);

          string name = gltf.Images[i].Name;

          images.Add(new ImageHolder(this, bmp, name));
        }
      }

      if (gltf.Materials != null)
      {
        for (int i = 0; i < gltf.Materials.Length; i++)
        {
          GltfRhinoMaterialConverter converter = new GltfRhinoMaterialConverter(gltf.Materials[i], doc, this);
          materials.Add(converter.Convert());
        }
      }

      for (int i = 0; i < gltf.Meshes.Length; i++)
      {
        GltfRhinoMeshConverter converter = new GltfRhinoMeshConverter(gltf.Meshes[i], this, doc);
        meshHolders.Add(converter.Convert());
      }

      ProcessHierarchy();

      return true;
    }

    private void ProcessHierarchy()
    {
      HashSet<int> children = new HashSet<int>();

      for (int i = 0; i < gltf.Nodes.Length; i++)
      {
        glTFLoader.Schema.Node node = gltf.Nodes[i];

        if (node.Children != null)
        {
          foreach (int child in node.Children)
          {
            children.Add(child);
          }
        }
      }

      List<int> parents = new List<int>();

      for (int i = 0; i < gltf.Nodes.Length; i++)
      {
        if (!children.Contains(i))
        {
          parents.Add(i);
        }
      }

      foreach (int parentIndex in parents)
      {
        glTFLoader.Schema.Node parent = gltf.Nodes[parentIndex];

        AddNodeRecursive(parent, Rhino.Geometry.Transform.Identity, null);
      }

    }

    private void AddNodeRecursive(glTFLoader.Schema.Node node, Rhino.Geometry.Transform transform, int? activeLayerIdx)
    {
      Rhino.Geometry.Transform finalTransform = transform * GetNodeTransform(node);

      if (node.Mesh.HasValue)
      {
        meshHolders[node.Mesh.Value].AddInstance(finalTransform, activeLayerIdx);
      }
      else if(IsLayerNode(node))
      {
        activeLayerIdx = CreateLayerFromNode(node, activeLayerIdx);
      }

      if (node.Children != null)
      {
        foreach (int childIndex in node.Children)
        {
          glTFLoader.Schema.Node child = gltf.Nodes[childIndex];

          AddNodeRecursive(child, finalTransform, activeLayerIdx);
        }
      }
    }

    bool IsLayerNode(glTFLoader.Schema.Node node)
    {
      //Looking for a empty node that has a name we can call a node

      if (node.Mesh != null) //No mesh
      {
        return false;
      }

      if (node.Camera != null) //No camera
      {
        return false;
      }

      if (string.IsNullOrEmpty(node.Name)) //Has a name
      {
        return false;
      }

      return true;
    }

    int? CreateLayerFromNode(glTFLoader.Schema.Node node, int? parentLayerIndex)
    {
      Rhino.DocObjects.Layer layerForNode = new Rhino.DocObjects.Layer();
      layerForNode.Name = node.Name;

      if(parentLayerIndex.HasValue)
      {
        Rhino.DocObjects.Layer parentLayer = doc.Layers[parentLayerIndex.Value];
        if(parentLayer != null)
        {
          layerForNode.ParentLayerId = parentLayer.Id;
        }
      }  

      int rc = doc.Layers.Add(layerForNode);

      if (rc < 0)
      {
        return null;
      }

      return rc;
    }

    Rhino.Geometry.Transform GetNodeTransform(glTFLoader.Schema.Node node)
    {
      Rhino.Geometry.Transform matrixTransform = GetMatrixTransform(node);

      if (!matrixTransform.IsIdentity)
      {
        return matrixTransform;
      }
      else
      {
        return GetTrsTransform(node);
      }
    }

    public Rhino.Geometry.Transform GetMatrixTransform(glTFLoader.Schema.Node node)
    {
      Rhino.Geometry.Transform xform = Rhino.Geometry.Transform.Identity;

      if (node.Matrix != null)
      {
        xform.M00 = node.Matrix[0];
        xform.M01 = node.Matrix[1];
        xform.M02 = node.Matrix[2];
        xform.M03 = node.Matrix[3];
        xform.M10 = node.Matrix[4];
        xform.M11 = node.Matrix[5];
        xform.M12 = node.Matrix[6];
        xform.M13 = node.Matrix[7];
        xform.M20 = node.Matrix[8];
        xform.M21 = node.Matrix[9];
        xform.M22 = node.Matrix[10];
        xform.M23 = node.Matrix[11];
        xform.M30 = node.Matrix[12];
        xform.M31 = node.Matrix[13];
        xform.M32 = node.Matrix[14];
        xform.M33 = node.Matrix[15];

        xform = xform.Transpose();
      }

      return xform;
    }

    public Rhino.Geometry.Transform GetTrsTransform(glTFLoader.Schema.Node node)
    {
      Rhino.Geometry.Vector3d translation = Rhino.Geometry.Vector3d.Zero;

      if (node.Translation != null && node.Translation.Length == 3)
      {
        translation.X = node.Translation[0];
        translation.Y = node.Translation[1];
        translation.Z = node.Translation[2];
      }

      Rhino.Geometry.Quaternion rotation = Rhino.Geometry.Quaternion.Identity;

      if (node.Rotation != null && node.Rotation.Length == 4)
      {
        rotation.A = node.Rotation[3];
        rotation.B = node.Rotation[0];
        rotation.C = node.Rotation[1];
        rotation.D = node.Rotation[2];
      }

      Rhino.Geometry.Vector3d scaling = new Rhino.Geometry.Vector3d(1.0, 1.0, 1.0);

      if (node.Scale != null && node.Scale.Length == 3)
      {
        scaling.X = node.Scale[0];
        scaling.Y = node.Scale[1];
        scaling.Z = node.Scale[2];
      }

      Rhino.Geometry.Transform translationTransform = Rhino.Geometry.Transform.Translation(translation);

      rotation.GetRotation(out Rhino.Geometry.Transform rotationTransform);

      Rhino.Geometry.Transform scalingTransform = Rhino.Geometry.Transform.Scale(Rhino.Geometry.Plane.WorldXY, scaling.X, scaling.Y, scaling.Z);

      return translationTransform * rotationTransform * scalingTransform;
    }

    public Rhino.Render.RenderTexture CreateMultiplyTexture(Rhino.Render.RenderTexture texture, Rhino.Display.Color4f factor)
    {
      Rhino.Render.RenderContent rc = Rhino.Render.RenderContentType.NewContentFromTypeId(Rhino.Render.ContentUuids.MultiplyTextureType);

      Rhino.Render.RenderTexture multiplyTexture = rc as Rhino.Render.RenderTexture;

      if(multiplyTexture == null)
      {
        return null;
      }

      multiplyTexture.BeginChange(Rhino.Render.RenderContent.ChangeContexts.Program);

      const string colorOneName = "color-one";
      const string colorTwoName = "color-two";

      multiplyTexture.SetChild(texture, colorOneName);
      multiplyTexture.SetChildSlotOn(colorOneName, true, Rhino.Render.RenderContent.ChangeContexts.Program);
      multiplyTexture.SetChildSlotAmount(colorOneName, 100.0, Rhino.Render.RenderContent.ChangeContexts.Program);

      multiplyTexture.SetParameter(colorTwoName, factor);

      multiplyTexture.EndChange();

      return multiplyTexture;
    }

    public Rhino.Render.RenderTexture GetRenderTexture(int textureIndex)
    {
      glTFLoader.Schema.Texture texture = gltf.Textures[textureIndex];

      if(!texture.Source.HasValue)
      {
        return null;
      }

      ImageHolder holder = images[texture.Source.Value];

      string textureName = GetUniqueName(GetUsefulTextureName(texture));

      string textureFilename = holder.RgbaImagePath();

      return RenderTextureForFile(textureFilename, textureName);
    }

    public Rhino.Render.RenderTexture GetRenderTexture(int textureIndex, ArgbChannel channel)
    {
      glTFLoader.Schema.Texture texture = gltf.Textures[textureIndex];

      if (!texture.Source.HasValue)
      {
        return null;
      }

      ImageHolder holder = images[texture.Source.Value];

      string textureName = GetUniqueName(GetUsefulTextureName(texture));

      string textureFilename = holder.ImagePathForChannel(channel);

      return RenderTextureForFile(textureFilename, textureName);
    }

    Rhino.Render.RenderTexture RenderTextureForFile(string textureFilename, string textureName)
    {
      if (File.Exists(textureFilename))
      {
        Rhino.DocObjects.Texture tex = new Rhino.DocObjects.Texture();
        tex.FileName = textureFilename;

        Rhino.Render.SimulatedTexture sim = new Rhino.Render.SimulatedTexture(doc, tex);
        Rhino.Render.RenderTexture texture = Rhino.Render.RenderTexture.NewBitmapTexture(sim, doc);

        texture.BeginChange(Rhino.Render.RenderContent.ChangeContexts.Program);

        texture.Name = textureName;

        texture.EndChange();

        return texture;
      }

      return null;
    }

    string GetUsefulTextureName(glTFLoader.Schema.Texture texture)
    {
      if(!string.IsNullOrEmpty(texture.Name)) //First try the textures name
      {
        return texture.Name;
      }
      
      if(texture.Source.HasValue)
      {
        glTFLoader.Schema.Image img = gltf.Images[texture.Source.Value];

        if(!string.IsNullOrEmpty(img.Name)) //then try the source image name
        {
          return img.Name;
        }
        else if(img.Uri != null && !img.Uri.StartsWith("data:image/")) //if its not an embedded image in a binary buffer this gets the filename
        {
          return Path.GetFileNameWithoutExtension(img.Uri);
        }
      }

      return ""; //Unique will get us some unnamed image
    }

    public byte[] GetBuffer(int index)
    {
      if (index < 0 || index >= buffers.Count)
      {
        return null;
      }

      return buffers[index];
    }

    public Rhino.Render.RenderMaterial GetMaterial(int? index)
    {
      if (index == null)
      {
        return null;
      }

      if (index < 0 || index >= materials.Count)
      {
        return null;
      }

      return materials[index.Value];
    }

    public glTFLoader.Schema.Accessor GetAccessor(int? index)
    {
      if (index == null)
      {
        return null;
      }

      if (index < 0 || index >= gltf.Accessors.Length)
      {
        return null;
      }

      return gltf.Accessors[index.Value];
    }

    public glTFLoader.Schema.BufferView GetBufferView(int? index)
    {
      if (index == null)
      {
        return null;
      }

      if (index < 0 || index >= gltf.BufferViews.Length)
      {
        return null;
      }

      return gltf.BufferViews[index.Value];
    }

  }
}

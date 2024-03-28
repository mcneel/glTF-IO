using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino;
using Rhino.FileIO;

namespace Export_glTF
{
  internal class MeshMaterialPair
  {
    public MeshMaterialPair(Rhino.Geometry.Mesh mesh, Rhino.Render.RenderMaterial renderMaterial)
    {
      Mesh = mesh;

      SetRenderMaterial(renderMaterial);
    }

    public void SetRenderMaterial(Rhino.Render.RenderMaterial renderMaterial)
    {
      RenderMaterial = renderMaterial;

      if (RenderMaterial != null)
      {
        Material = RenderMaterial.ToMaterial(Rhino.Render.RenderTexture.TextureGeneration.Allow);

        if (!Material.IsPhysicallyBased)
        {
          Material.ToPhysicallyBased();
        }

        PBR = Material.PhysicallyBased;
      }
    }

    public Rhino.Geometry.Mesh Mesh
    {
      get;
      private set;
    } = null;

    public Rhino.Render.RenderMaterial RenderMaterial
    {
      get;
      private set;
    } = null;

    public Rhino.DocObjects.Material Material
    {
      get;
      private set;
    } = null;

    public Rhino.DocObjects.PhysicallyBasedMaterial PBR
    {
      get;
      private set;
    } = null;
  }

  internal class ObjectExportData
  {
    public List<MeshMaterialPair> Meshes = new List<MeshMaterialPair>();
    public Rhino.Geometry.Transform Transform = Rhino.Geometry.Transform.Identity;
    public Rhino.DocObjects.RhinoObject Object = null;
  }

  class ExportedMaterialAndMapping
  {
    public int GltfMaterialIndex = -1;
    public Dictionary<int, int> RhinoMappingToGltfTexCoords = new Dictionary<int, int>();
  }

  class RhinoDocGltfConverter
  {
    public RhinoDocGltfConverter(FileGltfWriteOptions options, bool binary, RhinoDoc doc, IEnumerable<Rhino.DocObjects.RhinoObject> objects, Rhino.Render.LinearWorkflow workflow)
    {
      this.doc = doc;
      this.options = options;
      this.binary = binary;
      this.objects = objects;
      this.workflow = workflow;

      //glTF is in meters
      //https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#coordinate-system-and-units
      double scaleFactor = Rhino.RhinoMath.UnitScale(doc.ModelUnitSystem, UnitSystem.Meters);
      DocumentToGltfScale = Rhino.Geometry.Transform.Scale(Rhino.Geometry.Point3d.Origin, scaleFactor);
    }

    private RhinoDoc doc = null;

    private IEnumerable<Rhino.DocObjects.RhinoObject> objects = null;

    private bool binary = false;
    private FileGltfWriteOptions options = null;
    private Rhino.Render.LinearWorkflow workflow = null;

    private Dictionary<Guid, List<ExportedMaterialAndMapping>> materialsMap = new Dictionary<Guid, List<ExportedMaterialAndMapping>>();

    private gltfSchemaDummy dummy = new gltfSchemaDummy();

    private List<byte> binaryBuffer = new List<byte>();

    private Dictionary<int, glTFLoader.Schema.Node> layers = new Dictionary<int, glTFLoader.Schema.Node>();

    private Rhino.Render.RenderMaterial defaultMaterial = null;
    private Rhino.Render.RenderMaterial DefaultMaterial
    {
      get
      {
        if (defaultMaterial == null)
        {
          defaultMaterial = Rhino.DocObjects.Material.DefaultMaterial.RenderMaterial;
        }

        return defaultMaterial;
      }
    }

    public readonly Rhino.Geometry.Transform DocumentToGltfScale;

    public glTFLoader.Schema.Gltf ConvertToGltf()
    {
      dummy.Scene = 0;
      dummy.Scenes.Add(new gltfSchemaSceneDummy());

      dummy.Asset = new glTFLoader.Schema.Asset()
      {
        Version = "2.0",
      };

      dummy.Samplers.Add(new glTFLoader.Schema.Sampler()
      {
        MinFilter = glTFLoader.Schema.Sampler.MinFilterEnum.LINEAR,
        MagFilter = glTFLoader.Schema.Sampler.MagFilterEnum.LINEAR,
        WrapS = glTFLoader.Schema.Sampler.WrapSEnum.REPEAT,
        WrapT = glTFLoader.Schema.Sampler.WrapTEnum.REPEAT,
      });

      if (options.UseDracoCompression)
      {
        dummy.ExtensionsUsed.Add(glTFExtensions.KHR_draco_mesh_compression.Tag);
        dummy.ExtensionsRequired.Add(glTFExtensions.KHR_draco_mesh_compression.Tag);
      }

      dummy.ExtensionsUsed.Add(glTFExtensions.KHR_materials_transmission.Tag);
      dummy.ExtensionsUsed.Add(glTFExtensions.KHR_materials_clearcoat.Tag);
      dummy.ExtensionsUsed.Add(glTFExtensions.KHR_materials_ior.Tag);
      dummy.ExtensionsUsed.Add(glTFExtensions.KHR_materials_specular.Tag);
      dummy.ExtensionsUsed.Add(glTFExtensions.KHR_materials_emissive_strength.Tag);
      dummy.ExtensionsUsed.Add(glTFExtensions.KHR_texture_transform.Tag);

      IEnumerable<Rhino.DocObjects.RhinoObject> pointClouds = objects.Where(x => x.ObjectType == Rhino.DocObjects.ObjectType.PointSet);

      foreach (Rhino.DocObjects.RhinoObject rhinoObject in pointClouds)
      {
        RhinoPointCloudGltfConverter converter = new RhinoPointCloudGltfConverter(this, rhinoObject, options, binary, dummy, binaryBuffer);
        int meshIndex = converter.AddPointCloud();

        if (meshIndex != -1)
        {
          glTFLoader.Schema.Node node = new glTFLoader.Schema.Node()
          {
            Mesh = meshIndex,
            Name = GetObjectName(rhinoObject),
          };

          int nodeIndex = dummy.Nodes.AddAndReturnIndex(node);

          AddNode(nodeIndex, rhinoObject);
        }
      }

      IEnumerable<Rhino.DocObjects.RhinoObject> curveObjects = objects.Where(x => {

        //Must be curve
        if(x.ObjectType != Rhino.DocObjects.ObjectType.Curve)
        {
          return false;
        }

        var flags = Rhino.Render.CustomRenderMeshes.RenderMeshProvider.Flags.Recursive;
        Rhino.Render.CustomRenderMeshes.RenderMeshes renderMeshes = x.RenderMeshes(Rhino.Geometry.MeshType.Render, null, null, ref flags, null, null);

        //Without any render meshes
        return renderMeshes.InstanceCount == 0;
      });

      foreach(var curveObject in curveObjects)
      {
        RhinoCurveGltfConverter converter = new RhinoCurveGltfConverter(this, curveObject, options, binary, dummy, binaryBuffer);

        int idx = converter.AddCurve();

        if(idx != -1)
        {
          glTFLoader.Schema.Node node = new glTFLoader.Schema.Node()
          {
            Mesh = idx,
            Name = GetObjectName(curveObject),
          };

          int nodeIndex = dummy.Nodes.AddAndReturnIndex(node);

          AddNode(nodeIndex, curveObject);
        }
      }

      var sanitized = SanitizeRhinoObjects(objects);

      foreach (ObjectExportData exportData in sanitized)
      {
        RhinoMeshGltfConverter meshConverter = new RhinoMeshGltfConverter(this, exportData, options, binary, dummy, binaryBuffer);
        int meshIndex = meshConverter.AddMesh();

        glTFLoader.Schema.Node node = new glTFLoader.Schema.Node()
        {
          Mesh = meshIndex,
          Name = GetObjectName(exportData.Object),
        };

        int nodeIndex = dummy.Nodes.AddAndReturnIndex(node);

        AddNode(nodeIndex, exportData.Object);
      }

      if (binary && binaryBuffer.Count > 0)
      {
        //have to add the empty buffer for the binary file header
        dummy.Buffers.Add(new glTFLoader.Schema.Buffer()
        {
          ByteLength = (int)binaryBuffer.Count,
          Uri = null,
        });
      }

      return dummy.ToSchemaGltf();
    }

    private void AddNode(int nodeIndex, Rhino.DocObjects.RhinoObject rhinoObject)
    {
      if (options.ExportLayers)
      {
        AddToLayer(doc.Layers[rhinoObject.Attributes.LayerIndex], nodeIndex);
      }
      else
      {
        dummy.Scenes[dummy.Scene].Nodes.Add(nodeIndex);
      }
    }

    private void AddToLayer(Rhino.DocObjects.Layer layer, int child)
    {
      if (layers.TryGetValue(layer.Index, out glTFLoader.Schema.Node node))
      {
        if (node.Children == null)
        {
          node.Children = new int[1] { child };
        }
        else
        {
          node.Children = node.Children.Append(child).ToArray();
        }
      }
      else
      {
        node = new glTFLoader.Schema.Node()
        {
          Name = layer.Name,
          Children = new int[1] { child },
        };

        layers.Add(layer.Index, node);

        int nodeIndex = dummy.Nodes.AddAndReturnIndex(node);

        Rhino.DocObjects.Layer parentLayer = doc.Layers.FindId(layer.ParentLayerId);

        if (parentLayer == null)
        {
          dummy.Scenes[dummy.Scene].Nodes.Add(nodeIndex);
        }
        else
        {
          AddToLayer(parentLayer, nodeIndex);
        }
      }
    }

    public string GetObjectName(Rhino.DocObjects.RhinoObject rhinoObject)
    {
      return string.IsNullOrEmpty(rhinoObject.Name) ? null : rhinoObject.Name;
    }

    public byte[] GetBinaryBuffer()
    {
      return binaryBuffer.ToArray();
    }

    private bool MappingToTexCoordDictsAreEqual(Dictionary<int, int> a, Dictionary<int, int> b)
    {
      if(a.Count != b.Count)
      {
        return false;
      }

      foreach(var kvp in a)
      {
        if(!b.TryGetValue(kvp.Key, out int value))
        {
          return false;
        }

        if(kvp.Value != value)
        {
          return false;
        }
      }

      return true;
    }

    public int? GetMaterial(MeshMaterialPair pair, Dictionary<int, int> mappingToGltfTexCoord, Rhino.DocObjects.RhinoObject rhinoObject)
    {
      if (!options.ExportMaterials)
      {
        return null;
      }

      if (pair.RenderMaterial == null && options.UseDisplayColorForUnsetMaterials)
      {
        System.Drawing.Color objectColor = GetObjectColor(rhinoObject);
        return GetSolidColorMaterial(objectColor);
      }
      else if (pair.RenderMaterial == null)
      {
        pair.SetRenderMaterial(DefaultMaterial);
      }

      Guid materialId = pair.RenderMaterial.Id;

      int materialIndex = -1;

      if (materialsMap.TryGetValue(materialId, out List<ExportedMaterialAndMapping> materials))
      {
        foreach(ExportedMaterialAndMapping materialAndMapping in materials)
        {
          //The mappings from mapping channels to texcoord indices must be the same
          if(MappingToTexCoordDictsAreEqual(materialAndMapping.RhinoMappingToGltfTexCoords, mappingToGltfTexCoord))
          {
            materialIndex = materialAndMapping.GltfMaterialIndex;
            break;
          }
        }

        if(materialIndex == -1)
        {
          materialIndex = CreateMaterial(pair, mappingToGltfTexCoord);

          materials.Add(new ExportedMaterialAndMapping()
          {
            GltfMaterialIndex = materialIndex,
            RhinoMappingToGltfTexCoords = mappingToGltfTexCoord
          });
        }
      }
      else
      {
        materials = new List<ExportedMaterialAndMapping>();

        materialIndex = CreateMaterial(pair, mappingToGltfTexCoord);

        materials.Add(new ExportedMaterialAndMapping()
        {
          GltfMaterialIndex = materialIndex,
          RhinoMappingToGltfTexCoords = mappingToGltfTexCoord
        });

        materialsMap.Add(materialId, materials);
      }

      return materialIndex;
    }

    private int CreateMaterial(MeshMaterialPair pair, Dictionary<int, int> mappingToGltfTexCoord)
    {
      RhinoMaterialGltfConverter materialConverter = new RhinoMaterialGltfConverter(options, binary, dummy, binaryBuffer, pair, mappingToGltfTexCoord, workflow);
      return materialConverter.AddMaterial();
    }

    public int GetSolidColorMaterial(System.Drawing.Color color)
    {
      glTFLoader.Schema.Material material = new glTFLoader.Schema.Material()
      {
        PbrMetallicRoughness = new glTFLoader.Schema.MaterialPbrMetallicRoughness()
        {
          BaseColorFactor = new Rhino.Display.Color4f(color).ToFloatArray(),
        },
        DoubleSided = !options.CullBackfaces,
      };

      return dummy.Materials.AddAndReturnIndex(material);
    }

    public System.Drawing.Color GetObjectColor(Rhino.DocObjects.RhinoObject rhinoObject)
    {
      if (rhinoObject.Attributes.ColorSource == Rhino.DocObjects.ObjectColorSource.ColorFromLayer)
      {
        int layerIndex = rhinoObject.Attributes.LayerIndex;

        return rhinoObject.Document.Layers[layerIndex].Color;
      }
      else
      {
        return rhinoObject.Attributes.ObjectColor;
      }
    }

    public bool MeshIsValidForExport(Rhino.Geometry.Mesh mesh)
    {
      if (mesh == null)
      {
        return false;
      }

      if (mesh.Vertices.Count == 0)
      {
        return false;
      }

      if (mesh.Faces.Count == 0)
      {
        return false;
      }

      if (!options.ExportOpenMeshes && !mesh.IsClosed)
      {
        return false;
      }

      return true;
    }

    public List<ObjectExportData> SanitizeRhinoObjects(IEnumerable<Rhino.DocObjects.RhinoObject> rhinoObjects)
    {
      List<ObjectExportData> explodedObjects = new List<ObjectExportData>();

      foreach (Rhino.DocObjects.RhinoObject rhinoObject in rhinoObjects)
      {
        if (rhinoObject.ObjectType == Rhino.DocObjects.ObjectType.InstanceReference && rhinoObject is Rhino.DocObjects.InstanceObject instanceObject)
        {
          List<Rhino.DocObjects.RhinoObject> pieces = new List<Rhino.DocObjects.RhinoObject>();
          List<Rhino.Geometry.Transform> transforms = new List<Rhino.Geometry.Transform>();

          ExplodeRecursive(instanceObject, instanceObject.InstanceXform, pieces, transforms);

          foreach (var item in pieces.Zip(transforms, (rObj, trans) => (rhinoObject: rObj, trans)))
          {
            explodedObjects.Add(new ObjectExportData()
            {
              Object = item.rhinoObject,
              Transform = item.trans,
            });
          }
        }
        else
        {
          explodedObjects.Add(new ObjectExportData()
          {
            Object = rhinoObject,
          });
        }
      }

      var flags = Rhino.Render.CustomRenderMeshes.RenderMeshProvider.Flags.Recursive;

      foreach (var item in explodedObjects)
      {
        //Handle SubD objects with the SubD options
        if (
          item.Object.ObjectType == Rhino.DocObjects.ObjectType.SubD &&
          item.Object.Geometry is Rhino.Geometry.SubD subd &&
          options.SubDMeshType == FileGltfWriteOptions.SubDMeshing.ControlNet
          )
        {
          Rhino.Geometry.Mesh mesh = Rhino.Geometry.Mesh.CreateFromSubDControlNet(subd);

          mesh.Transform(item.Transform);

          item.Meshes.Add(new MeshMaterialPair(mesh, GetObjectMaterial(item.Object)));
        }
        else
        {
          Rhino.Render.CustomRenderMeshes.RenderMeshes renderMeshes = item.Object.RenderMeshes(Rhino.Geometry.MeshType.Render, null, null, ref flags, null, null);

          if (renderMeshes.InstanceCount != 0)
          {
            foreach (var mesh in renderMeshes)
            {
              Rhino.Geometry.Mesh copy = new Rhino.Geometry.Mesh();
              copy.CopyFrom(mesh.Mesh);

              if (!mesh.Transform.IsIdentity)
              {
                copy.Transform(mesh.Transform);
              }

              if (!item.Transform.IsIdentity)
              {
                copy.Transform(item.Transform);
              }

              item.Meshes.Add(new MeshMaterialPair(copy, mesh.Material));
            }
          }
          else
          {
            Rhino.Geometry.MeshingParameters parameters = item.Object.GetRenderMeshParameters();

            if (item.Object.MeshCount(Rhino.Geometry.MeshType.Render, parameters) == 0)
            {
              item.Object.CreateMeshes(Rhino.Geometry.MeshType.Render, parameters, false);
            }

            Rhino.Geometry.Mesh[] meshes = item.Object.GetMeshes(Rhino.Geometry.MeshType.Render);

            Rhino.Render.RenderMaterial material = GetObjectMaterial(item.Object);

            foreach (Rhino.Geometry.Mesh mesh in meshes)
            {
              mesh.EnsurePrivateCopy();

              if(!item.Transform.IsIdentity)
              {
                mesh.Transform(item.Transform);
              }

              item.Meshes.Add(new MeshMaterialPair(mesh, material));
            }
          }
        }

        //Remove bad meshes
        item.Meshes.RemoveAll(x => x == null || !MeshIsValidForExport(x.Mesh));
      }

      //Remove meshless objects
      explodedObjects.RemoveAll(x => x.Meshes.Count == 0);

      return explodedObjects;
    }

    private Rhino.Render.RenderMaterial GetObjectMaterial(Rhino.DocObjects.RhinoObject rhinoObject)
    {
      Rhino.DocObjects.ObjectMaterialSource source = rhinoObject.Attributes.MaterialSource;

      Rhino.Render.RenderMaterial renderMaterial = null;

      if (source == Rhino.DocObjects.ObjectMaterialSource.MaterialFromObject)
      {
        renderMaterial = rhinoObject.RenderMaterial;
      }
      else if (source == Rhino.DocObjects.ObjectMaterialSource.MaterialFromLayer)
      {
        int layerIndex = rhinoObject.Attributes.LayerIndex;

        renderMaterial = GetLayerMaterial(layerIndex);
      }

      return renderMaterial;
    }

    private Rhino.Render.RenderMaterial GetLayerMaterial(int layerIndex)
    {
      if (layerIndex < 0 || layerIndex >= doc.Layers.Count)
      {
        return null;
      }

      return doc.Layers[layerIndex].RenderMaterial;
    }

    private void ExplodeRecursive(Rhino.DocObjects.InstanceObject instanceObject, Rhino.Geometry.Transform instanceTransform, List<Rhino.DocObjects.RhinoObject> pieces, List<Rhino.Geometry.Transform> transforms)
    {
      for (int i = 0; i < instanceObject.InstanceDefinition.ObjectCount; i++)
      {
        Rhino.DocObjects.RhinoObject rhinoObject = instanceObject.InstanceDefinition.Object(i);

        if (rhinoObject is Rhino.DocObjects.InstanceObject nestedObject)
        {
          Rhino.Geometry.Transform nestedTransform = instanceTransform * nestedObject.InstanceXform;

          ExplodeRecursive(nestedObject, nestedTransform, pieces, transforms);
        }
        else
        {
          pieces.Add(rhinoObject);

          transforms.Add(instanceTransform);
        }
      }
    }
  }
}

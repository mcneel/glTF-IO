using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Export_glTF
{
  internal class RhinoCurveGltfConverter : RhinoGeometryGltfConverter
  {
    public RhinoCurveGltfConverter(RhinoDocGltfConverter converter, Rhino.DocObjects.RhinoObject rhinoObject, glTFExportOptions options, bool binary, gltfSchemaDummy dummy, List<byte> binaryBuffer)
      : base(rhinoObject, options, binary, dummy, binaryBuffer)
    {
      this.converter = converter;
    }

    RhinoDocGltfConverter converter = null;

    public int AddCurve()
    {
      Rhino.Geometry.Curve curve = RhinoObject.Geometry as Rhino.Geometry.Curve;

      if (curve == null)
      {
        return -1;
      }

      if(Options.MapRhinoZToGltfY)
      {
        curve.Transform(Constants.ZtoYUp);
      }

      if(!curve.TryGetPolyline(out Rhino.Geometry.Polyline polyline))
      {
        return -1;
      }

      Rhino.Geometry.Point3d[] pts = polyline.ToArray();

      int? vertexAccessorIdx = GetVertexAccessor(pts);
      if(vertexAccessorIdx == null)
      {
        return -1;
      }

      System.Drawing.Color color = converter.GetObjectColor(RhinoObject);
      int materialIdx = converter.GetSolidColorMaterial(color);

      glTFLoader.Schema.MeshPrimitive primitive = new glTFLoader.Schema.MeshPrimitive()
      {
        Attributes = new Dictionary<string, int>(),
        Mode = glTFLoader.Schema.MeshPrimitive.ModeEnum.LINE_STRIP,
        Material = materialIdx,
      };

      primitive.Attributes.Add(Constants.PositionAttributeTag, vertexAccessorIdx.Value);

      glTFLoader.Schema.Mesh mesh = new glTFLoader.Schema.Mesh()
      {
        Primitives = new glTFLoader.Schema.MeshPrimitive[]
        {
          primitive
        }
      };

      return Dummy.Meshes.AddAndReturnIndex(mesh);
    }

  }
}

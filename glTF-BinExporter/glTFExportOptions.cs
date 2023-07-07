using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Export_glTF
{
  public class glTFExportOptions
  {
    public bool MapRhinoZToGltfY = Export_glTFPlugin.MapRhinoZToGltfYDefault;
    public bool ExportMaterials = Export_glTFPlugin.ExportMaterialsDefault;
    public bool CullBackfaces = Export_glTFPlugin.CullBackfacesDefault;
    public bool UseDisplayColorForUnsetMaterials = Export_glTFPlugin.UseDisplayColorForUnsetMaterialsDefault;

    public SubDMode SubDExportMode = Export_glTFPlugin.SubDModeDefault;
    public int SubDLevel = Export_glTFPlugin.SubDLevelDefault;

    public bool ExportTextureCoordinates = Export_glTFPlugin.ExportTextureCoordinatesDefault;
    public bool ExportVertexNormals = Export_glTFPlugin.ExportVertexNormalsDefault;
    public bool ExportOpenMeshes = Export_glTFPlugin.ExportOpenMeshesDefault;
    public bool ExportVertexColors = Export_glTFPlugin.ExportVertexColorsDefault;

    public bool UseDracoCompression = Export_glTFPlugin.UseDracoCompressionDefault;
    public int DracoCompressionLevel = Export_glTFPlugin.DracoCompressionLevelDefault;
    public int DracoQuantizationBitsPosition = Export_glTFPlugin.DracoQuantizationBitsPositionDefault;
    public int DracoQuantizationBitsNormal = Export_glTFPlugin.DracoQuantizationBitsNormalDefault;
    public int DracoQuantizationBitsTexture = Export_glTFPlugin.DracoQuantizationBitsTextureDefault;

    public bool ExportLayers = Export_glTFPlugin.ExportLayers;
  }
}

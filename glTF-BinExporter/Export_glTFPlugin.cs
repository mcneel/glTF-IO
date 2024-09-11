using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.PlugIns;
using Rhino.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Export_glTF
{
  /// <summary>
  /// This is flipped from Rhino.FileIO.File.FileGltfWriteOptions.SubDMeshing
  /// This was here first but when FileGltfWriteOptions.SubDMeshing it was made to match the ObjWriteOptions
  /// to prevent confusion in the public interface. Now this exists for clarity when casting from the saved app setting integer.
  /// </summary>
  public enum SubDMode : int
  {
    ControlNet = 0,
    Surface = 1,
  }

  public class Export_glTFPlugin : Rhino.PlugIns.FileExportPlugIn
  {
    public static Export_glTFPlugin Instance { get; private set; }

    public Export_glTFPlugin()
    {
      Instance = this;
    }

    protected override FileTypeList AddFileTypes(FileWriteOptions options)
    {
      FileTypeList typeList = new FileTypeList();

      typeList.AddFileType(Rhino.UI.Localization.LocalizeString("glTF text file (*.gltf)", 1), "gltf", true);
      typeList.AddFileType(Rhino.UI.Localization.LocalizeString("glTF binary file (*.glb)", 2), "glb", true);

      return typeList;
    }

    protected override WriteFileResult WriteFile(string filename, int index, RhinoDoc doc, FileWriteOptions options)
    {
      bool binary = GlTFUtils.IsFileGltfBinary(filename);

      if (!UseSavedSettingsDontShowDialog && !options.SuppressDialogBoxes)
      {
        ExportOptionsDialog optionsDlg = new ExportOptionsDialog();

        optionsDlg.RestorePosition();
        var result = optionsDlg.ShowModal();

        if (result != Result.Success)
        {
          return WriteFileResult.Cancel;
        }

        optionsDlg.DialogToOptions();
      }

      FileGltfWriteOptions exportOptions = null;

      if (options.OptionsDictionary.Count > 0)
      {
        exportOptions = GetDictionaryOptions(options.OptionsDictionary);
      }
      else
      {
        exportOptions = Export_glTFPlugin.GetSavedOptions();
      }

      IEnumerable<Rhino.DocObjects.RhinoObject> objects = GetObjectsToExport(doc, options);

      if (!DoExport(filename, exportOptions, binary, doc, objects, doc.RenderSettings.LinearWorkflow))
      {
        return WriteFileResult.Failure;
      }

      return WriteFileResult.Success;
    }

    private IEnumerable<Rhino.DocObjects.RhinoObject> GetObjectsToExport(RhinoDoc doc, FileWriteOptions options)
    {
      if (options.WriteSelectedObjectsOnly)
      {
        //For some reason just returning doc.Objects.GetSelectedObjects(false, false) returns an empty IEnumerable
        return doc.Objects.GetSelectedObjects(false, false).ToArray();
      }
      else
      {
        return doc.Objects;
      }
    }

    protected override void DisplayOptionsDialog(IntPtr parent, string description, string extension)
    {
      ExportOptionsDialog exportOptionsDialog = new ExportOptionsDialog();

      exportOptionsDialog.RestorePosition();
      var rc = exportOptionsDialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);

      if(rc == Result.Success)
      {
        exportOptionsDialog.DialogToOptions();
      }
    }

    public static bool DoExport(string fileName, FileGltfWriteOptions options, bool binary, RhinoDoc doc, IEnumerable<Rhino.DocObjects.RhinoObject> rhinoObjects, Rhino.Render.LinearWorkflow workflow)
    {
      RhinoDocGltfConverter converter = new RhinoDocGltfConverter(options, binary, doc, rhinoObjects, workflow);
      glTFLoader.Schema.Gltf gltf = converter.ConvertToGltf();

      if (binary)
      {
        byte[] bytes = converter.GetBinaryBuffer();
        glTFLoader.Interface.SaveBinaryModel(gltf, bytes.Length == 0 ? null : bytes, fileName);
      }
      else
      {
        glTFLoader.Interface.SaveModel(gltf, fileName);
      }

      return true;
    }

    #region Settings

    private const string useDracoCompressionKey = "UseDracoCompression";
    public const bool UseDracoCompressionDefault = false;

    public static bool UseDracoCompression
    {
      get => Instance.Settings.GetBool(useDracoCompressionKey, UseDracoCompressionDefault);
      set => Instance.Settings.SetBool(useDracoCompressionKey, value);
    }

    private const string mapRhinoZToGltfYKey = "MapZYpToYUp";
    public const bool MapRhinoZToGltfYDefault = true;

    public static bool MapRhinoZToGltfY
    {
      get => Instance.Settings.GetBool(mapRhinoZToGltfYKey, MapRhinoZToGltfYDefault);
      set => Instance.Settings.SetBool(mapRhinoZToGltfYKey, value);
    }

    private const string exportMaterialsKey = "ExportMaterials";
    public const bool ExportMaterialsDefault = true;

    public static bool ExportMaterials
    {
      get => Instance.Settings.GetBool(exportMaterialsKey, ExportMaterialsDefault);
      set => Instance.Settings.SetBool(exportMaterialsKey, value);
    }

    private const string cullBackfacesKey = "CullBackfaces";
    public const bool CullBackfacesDefault = true;

    public static bool CullBackfaces
    {
      get => Instance.Settings.GetBool(cullBackfacesKey, CullBackfacesDefault);
      set => Instance.Settings.SetBool(cullBackfacesKey, value);
    }

    private const string useDisplayColorForUnsetMaterialsKey = "UseDisplayColorForUnsetMaterials";
    public const bool UseDisplayColorForUnsetMaterialsDefault = true;

    public static bool UseDisplayColorForUnsetMaterials
    {
      get => Instance.Settings.GetBool(useDisplayColorForUnsetMaterialsKey, UseDisplayColorForUnsetMaterialsDefault);
      set => Instance.Settings.SetBool(useDisplayColorForUnsetMaterialsKey, value);
    }

    public const string SubDModeKey = "SubDMode";
    public const SubDMode SubDModeDefault = SubDMode.Surface;

    public static SubDMode SubDExportMode
    {
      get => (SubDMode)Instance.Settings.GetInteger(SubDModeKey, (int)SubDModeDefault);
      set => Instance.Settings.SetInteger(SubDModeKey, (int)value);
    }

    public const string SubDLevelKey = "SubDLevel";
    public const int SubDLevelDefault = 4;

    public static int SubDLevel
    {
      get => Instance.Settings.GetInteger(SubDLevelKey, SubDLevelDefault);
      set => Instance.Settings.SetInteger(SubDLevelKey, value);
    }

    public const string ExportTextureCoordinatesKey = "ExportTextureCoordinates";
    public const bool ExportTextureCoordinatesDefault = true;

    public static bool ExportTextureCoordinates
    {
      get => Instance.Settings.GetBool(ExportTextureCoordinatesKey, ExportTextureCoordinatesDefault);
      set => Instance.Settings.SetBool(ExportTextureCoordinatesKey, value);
    }

    public const string ExportVertexNormalsKey = "ExportVertexNormals";
    public const bool ExportVertexNormalsDefault = true;

    public static bool ExportVertexNormals
    {
      get => Instance.Settings.GetBool(ExportVertexNormalsKey, ExportVertexNormalsDefault);
      set => Instance.Settings.SetBool(ExportVertexNormalsKey, value);
    }

    public const string ExportVertexColorsKey = "ExportVertexColors";
    public const bool ExportVertexColorsDefault = false;

    public static bool ExportVertexColors
    {
      get => Instance.Settings.GetBool(ExportVertexColorsKey, ExportVertexColorsDefault);
      set => Instance.Settings.SetBool(ExportVertexColorsKey, value);
    }

    private const string exportOpenMeshesKey = "ExportOpenMeshes";
    public const bool ExportOpenMeshesDefault = true;

    public static bool ExportOpenMeshes
    {
      get => Instance.Settings.GetBool(exportOpenMeshesKey, ExportOpenMeshesDefault);
      set => Instance.Settings.SetBool(exportOpenMeshesKey, value);
    }

    private const string dracoCompressionLevelKey = "DracoCompressionLevel";
    public const int DracoCompressionLevelDefault = 10;

    public static int DracoCompressionLevel
    {
      get => Instance.Settings.GetInteger(dracoCompressionLevelKey, DracoCompressionLevelDefault);
      set => Instance.Settings.SetInteger(dracoCompressionLevelKey, value);
    }

    private const string dracoQuantizationBitsPositionKey = "DracoQuantizationBitsPosition";
    public const int DracoQuantizationBitsPositionDefault = 11;

    public static int DracoQuantizationBitsPosition
    {
      get => Instance.Settings.GetInteger(dracoQuantizationBitsPositionKey, DracoQuantizationBitsPositionDefault);
      set => Instance.Settings.SetInteger(dracoQuantizationBitsPositionKey, value);
    }

    private const string dracoQuantizationBitsNormalKey = "DracoQuantizationBitsNormal";
    public const int DracoQuantizationBitsNormalDefault = 8;

    public static int DracoQuantizationBitsNormal
    {
      get => Instance.Settings.GetInteger(dracoQuantizationBitsNormalKey, DracoQuantizationBitsNormalDefault);
      set => Instance.Settings.SetInteger(dracoQuantizationBitsNormalKey, value);
    }

    private const string dracoQuantizationBitsTextureKey = "DracoQuantizationBitsTextureKey";
    public const int DracoQuantizationBitsTextureDefault = 10;

    public static int DracoQuantizationBitsTexture
    {
      get => Instance.Settings.GetInteger(dracoQuantizationBitsTextureKey, DracoQuantizationBitsTextureDefault);
      set => Instance.Settings.SetInteger(dracoQuantizationBitsTextureKey, value);
    }

    private const string useSavedSettingsDontShowDialogKey = "UseSavedSettingsDontShowDialog";
    public const bool UseSavedSettingsDontShowDialogDefault = false;

    public static bool UseSavedSettingsDontShowDialog
    {
      get => Instance.Settings.GetBool(useSavedSettingsDontShowDialogKey, UseSavedSettingsDontShowDialogDefault);
      set => Instance.Settings.SetBool(useSavedSettingsDontShowDialogKey, value);
    }

    private const string ExportLayersDialogKey = "ExportLayers";
    public const bool ExportLayersDialogDefault = false;

    public static bool ExportLayers
    {
      get => Instance.Settings.GetBool(ExportLayersDialogKey, ExportLayersDialogDefault);
      set => Instance.Settings.SetBool(ExportLayersDialogKey, value);
    }

    static FileGltfWriteOptions GetSavedOptions()
    {
      FileGltfWriteOptions.SubDMeshing subDMode = SubDExportMode == SubDMode.Surface ? FileGltfWriteOptions.SubDMeshing.Surface : FileGltfWriteOptions.SubDMeshing.ControlNet;

      return new FileGltfWriteOptions()
      {
        MapZToY = MapRhinoZToGltfY,
        ExportMaterials = ExportMaterials,
        CullBackfaces = CullBackfaces,
        UseDisplayColorForUnsetMaterials = UseDisplayColorForUnsetMaterials,

        SubDMeshType = subDMode,
        SubDSurfaceMeshingDensity = SubDLevel,

        ExportTextureCoordinates = ExportTextureCoordinates,
        ExportVertexNormals = ExportVertexNormals,
        ExportOpenMeshes = ExportOpenMeshes,
        ExportVertexColors = ExportVertexColors,

        UseDracoCompression = UseDracoCompression,
        DracoCompressionLevel = DracoCompressionLevel,
        DracoQuantizationBitsPosition = DracoQuantizationBitsPosition,
        DracoQuantizationBitsNormal = DracoQuantizationBitsNormal,
        DracoQuantizationBitsTextureCoordinate = DracoQuantizationBitsTexture,

        ExportLayers = ExportLayers
      };
    }

    #endregion

    FileGltfWriteOptions GetDictionaryOptions(Rhino.Collections.ArchivableDictionary dict)
    {
      FileGltfWriteOptions rc = new FileGltfWriteOptions();

      if (dict.TryGetBool(nameof(FileGltfWriteOptions.MapZToY), out bool mapYToZ))
      {
        rc.MapZToY = mapYToZ;
      }

      if (dict.TryGetBool(nameof(FileGltfWriteOptions.ExportMaterials), out bool exportMaterials))
      {
        rc.ExportMaterials = exportMaterials;
      }

      if (dict.TryGetBool(nameof(FileGltfWriteOptions.CullBackfaces), out bool cullBackfaces))
      {
        rc.CullBackfaces = cullBackfaces;
      }

      if (dict.TryGetBool(nameof(FileGltfWriteOptions.UseDisplayColorForUnsetMaterials), out bool useDisplayColor))
      {
        rc.UseDisplayColorForUnsetMaterials = useDisplayColor;
      }

      if (dict.TryGetInteger(nameof(FileGltfWriteOptions.SubDMeshType), out int meshType))
      {
        rc.SubDMeshType = (FileGltfWriteOptions.SubDMeshing)meshType;
      }

      if (dict.TryGetInteger(nameof(FileGltfWriteOptions.SubDSurfaceMeshingDensity), out int meshDensity))
      {
        rc.SubDSurfaceMeshingDensity = meshDensity;
      }

      if (dict.TryGetBool(nameof(FileGltfWriteOptions.ExportTextureCoordinates), out bool exportTextureCoordinates))
      {
        rc.ExportTextureCoordinates = exportTextureCoordinates;
      }

      if (dict.TryGetBool(nameof(FileGltfWriteOptions.ExportVertexNormals), out bool exportVertexNormals))
      {
        rc.ExportVertexNormals = exportVertexNormals;
      }

      if (dict.TryGetBool(nameof(FileGltfWriteOptions.ExportOpenMeshes), out bool exportOpenMeshes))
      {
        rc.ExportOpenMeshes = exportOpenMeshes;
      }

      if (dict.TryGetBool(nameof(FileGltfWriteOptions.ExportVertexColors), out bool exportVertexColors))
      {
        rc.ExportVertexColors = exportVertexColors;
      }

      if (dict.TryGetBool(nameof(FileGltfWriteOptions.UseDracoCompression), out bool useDracoCompression))
      {
        rc.UseDracoCompression = useDracoCompression;
      }

      if (dict.TryGetInteger(nameof(FileGltfWriteOptions.DracoCompressionLevel), out int dracoCompressionLevel))
      {
        rc.DracoCompressionLevel = dracoCompressionLevel;
      }

      if (dict.TryGetInteger(nameof(FileGltfWriteOptions.DracoQuantizationBitsPosition), out int positionBits))
      {
        rc.DracoQuantizationBitsPosition = positionBits;
      }

      if (dict.TryGetInteger(nameof(FileGltfWriteOptions.DracoQuantizationBitsNormal), out int normalBits))
      {
        rc.DracoQuantizationBitsNormal = normalBits;
      }

      if (dict.TryGetInteger(nameof(FileGltfWriteOptions.DracoQuantizationBitsTextureCoordinate), out int textureCoordinateBits))
      {
        rc.DracoQuantizationBitsTextureCoordinate = textureCoordinateBits;
      }

      if (dict.TryGetBool(nameof(FileGltfWriteOptions.ExportLayers), out bool exportLayers))
      {
        rc.ExportLayers = exportLayers;
      }

      return rc;
    }

  }
}

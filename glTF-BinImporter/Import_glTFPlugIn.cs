using Rhino;
using Rhino.PlugIns;
using System;

namespace Import_glTF
{
  public class Import_glTFPlugIn : FileImportPlugIn
  {
    public Import_glTFPlugIn()
    {
      Instance = this;
    }

    public static Import_glTFPlugIn Instance
    {
      get; private set;
    }

    protected override FileTypeList AddFileTypes(Rhino.FileIO.FileReadOptions options)
    {
      FileTypeList result = new FileTypeList();
      result.AddFileType(Rhino.UI.Localization.LocalizeString("glTF text file (*.gltf)", 1), "gltf");
      result.AddFileType(Rhino.UI.Localization.LocalizeString("glTF binary file (*.glb)", 2), "glb");
      return result;
    }

    protected override bool ReadFile(string filename, int index, RhinoDoc doc, Rhino.FileIO.FileReadOptions options)
    {
      glTFLoader.Schema.Gltf gltf = glTFLoader.Interface.LoadModel(filename);

      GltfRhinoConverter converter = new GltfRhinoConverter(gltf, doc, filename);

      try
      {
        return converter.Convert();
      }
      catch (Exception e)
      {
        System.Diagnostics.Debug.WriteLine(e.Message);
        return false;
      }
    }

  }
}

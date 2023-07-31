using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTFExtensions
{
  /// <summary>
  /// https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_texture_transform
  /// </summary>
  public class KHR_texture_transform
  {
    public const string Tag = "KHR_texture_transform";

    [Newtonsoft.Json.JsonPropertyAttribute("offset")]
    public float[] Offset = new float[2]
    {
      0.0f, 
      0.0f,
    };

    [Newtonsoft.Json.JsonPropertyAttribute("rotation")]
    public float Rotation = 0.0f;

    [Newtonsoft.Json.JsonPropertyAttribute("scale")]
    public float[] Scale = new float[2]
    {
      1.0f,
      1.0f,
    };

    [Newtonsoft.Json.JsonPropertyAttribute("texCoord")]
    public int? TexCoord = null;

    public bool ShouldSerializeOffset()
    {
      return Offset != null;
    }

    public bool ShouldSerializeRotation()
    {
      return Rotation != 0.0f;
    }

    public bool ShouldSerializeScale()
    {
      return Scale != null;
    }

    public bool ShouldSerializeTexCoord()
    {
      return TexCoord != null;
    }
  }
}

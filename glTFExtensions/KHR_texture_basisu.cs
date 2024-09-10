using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTFExtensions
{
  /// <summary>
  /// https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_texture_basisu/README.md
  /// </summary>
  public class KHR_texture_basisu
  {
    public const string Tag = "KHR_texture_basisu";

    [Newtonsoft.Json.JsonPropertyAttribute("source")]
    public int? Source = null;
  }
}

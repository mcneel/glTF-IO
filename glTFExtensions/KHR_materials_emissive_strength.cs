using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTFExtensions
{
  /// <summary>
  /// https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_materials_emissive_strength/README.md
  /// </summary>
  public class KHR_materials_emissive_strength
  {
    public const string Tag = "KHR_materials_emissive_strength";

    [Newtonsoft.Json.JsonPropertyAttribute("emissiveStrength")]
    public float EmissiveStrength = 1.0f;
  }
}

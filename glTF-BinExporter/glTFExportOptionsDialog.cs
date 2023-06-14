using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eto.Forms;

namespace Export_glTF
{
  class ExportOptionsDialog : Dialog<DialogResult>
  {
    private const int DefaultPadding = 5;
    private static readonly Eto.Drawing.Size DefaultSpacing = new Eto.Drawing.Size(2, 2);

    private CheckBox mapZtoY = new CheckBox();
    private CheckBox exportMaterials = new CheckBox();
    private CheckBox useDoubleSidedMaterials = new CheckBox();
    private CheckBox useDisplayColorForUnsetMaterial = new CheckBox();
    private CheckBox exportLayers = new CheckBox();

    private GroupBox subdBox = new GroupBox();
    private CheckBox useSubdControlNet = new CheckBox();
    private Label subdLevelLabel = new Label();
    private Slider subdLevel = new Slider();

    private CheckBox exportTextureCoordinates = new CheckBox();
    private CheckBox exportVertexNormals = new CheckBox();
    private CheckBox exportOpenMeshes = new CheckBox();
    private CheckBox exportVertexColors = new CheckBox();

    private CheckBox useDracoCompressionCheck = new CheckBox();

    private Label dracoCompressionLabel = new Label();
    private NumericStepper dracoCompressionLevelInput = new NumericStepper();

    private Label dracoQuantizationBitsLabel = new Label();
    private NumericStepper dracoQuantizationBitsInputPosition = new NumericStepper() { DecimalPlaces = 0, MinValue = 8, MaxValue = 32 };
    private NumericStepper dracoQuantizationBitsInputNormal = new NumericStepper() { DecimalPlaces = 0, MinValue = 8, MaxValue = 32 };
    private NumericStepper dracoQuantizationBitsInputTexture = new NumericStepper() { DecimalPlaces = 0, MinValue = 8, MaxValue = 32 };

    private Button cancelButton = new Button();
    private Button okButton = new Button();

    private CheckBox useSettingsDontShowDialogCheck = new CheckBox();

    public ExportOptionsDialog()
    {
      Resizable = false;

      Title = Rhino.UI.Localization.LocalizeString("glTF Export Options", 3);

      mapZtoY.Text = Rhino.UI.Localization.LocalizeString("Map Rhino Z to glTF Y", 4);

      exportMaterials.Text = Rhino.UI.Localization.LocalizeString("Export materials", 5);

      useDoubleSidedMaterials.Text = Rhino.UI.Localization.LocalizeString("Use double sided materials", 28);

      useDisplayColorForUnsetMaterial.Text = Rhino.UI.Localization.LocalizeString("Use display color for objects with no material set", 6);

      exportLayers.Text = Rhino.UI.Localization.LocalizeString("Export Layers", 7);

      subdBox.Text = Rhino.UI.Localization.LocalizeString("SubD Meshing", 8);

      useSubdControlNet.Text = Rhino.UI.Localization.LocalizeString("Use control net", 9);

      subdLevelLabel.Text = Rhino.UI.Localization.LocalizeString("Subdivision level", 10);
      subdLevelLabel.TextAlignment = TextAlignment.Left;

      subdLevel.SnapToTick = true;
      subdLevel.TickFrequency = 1;
      subdLevel.MinValue = 1;
      subdLevel.MaxValue = 5;

      subdBox.Content = new TableLayout()
      {
        Padding = DefaultPadding,
        Spacing = DefaultSpacing,
        Rows =
        {
          new TableRow(useSubdControlNet, null),
          new TableRow(subdLevel, subdLevelLabel),
        }
      };

      exportTextureCoordinates.Text = Rhino.UI.Localization.LocalizeString("Export texture coordinates", 11);

      exportVertexNormals.Text = Rhino.UI.Localization.LocalizeString("Export vertex normals", 12);

      exportOpenMeshes.Text = Rhino.UI.Localization.LocalizeString("Export open meshes", 13);

      exportVertexColors.Text = Rhino.UI.Localization.LocalizeString("Export vertex colors", 14);

      useDracoCompressionCheck.Text = Rhino.UI.Localization.LocalizeString("Use Draco compression", 15);

      dracoCompressionLabel.Text = Rhino.UI.Localization.LocalizeString("Draco compression Level", 16);
      dracoCompressionLevelInput.DecimalPlaces = 0;
      dracoCompressionLevelInput.MinValue = 1;
      dracoCompressionLevelInput.MaxValue = 10;

      dracoQuantizationBitsLabel.Text = Rhino.UI.Localization.LocalizeString("Quantization", 17);

      cancelButton.Text = Rhino.UI.Localization.LocalizeString("Cancel", 18);

      okButton.Text = Rhino.UI.Localization.LocalizeString("Ok", 19);

      useSettingsDontShowDialogCheck.Text = Rhino.UI.Localization.LocalizeString("Always use these settings. Do not show this dialog again.", 20);

      OptionsToDialog();

      useDracoCompressionCheck.CheckedChanged += UseDracoCompressionCheck_CheckedChanged;
      exportMaterials.CheckedChanged += ExportMaterials_CheckedChanged;

      useSubdControlNet.CheckedChanged += UseSubdControlNet_CheckedChanged;

      cancelButton.Click += CancelButton_Click;
      okButton.Click += OkButton_Click;

      var dracoGroupBox = new GroupBox() { Text = Rhino.UI.Localization.LocalizeString("Draco Quantization Bits", 21) };
      dracoGroupBox.Content = new TableLayout()
      {
        Padding = DefaultPadding,
        Spacing = DefaultSpacing,
        Rows =
        {
          new TableRow
          (
            new Label()
            {
              Text = Rhino.UI.Localization.LocalizeString("Position", 22),
              TextAlignment = TextAlignment.Left,
            },
            new Label()
            {
              Text = Rhino.UI.Localization.LocalizeString("Normal", 23),
              TextAlignment = TextAlignment.Left,
            },
            new Label()
            {
              Text = Rhino.UI.Localization.LocalizeString("Texture", 24),
              TextAlignment = TextAlignment.Left,
            }
          ),
          new TableRow(dracoQuantizationBitsInputPosition, dracoQuantizationBitsInputNormal, dracoQuantizationBitsInputTexture)
        }
      };

      var layout = new DynamicLayout()
      {
        Padding = DefaultPadding,
        Spacing = DefaultSpacing,
      };

      layout.AddSeparateRow(useDracoCompressionCheck, null);
      layout.AddSeparateRow(dracoCompressionLabel, dracoCompressionLevelInput, null);
      layout.AddSeparateRow(dracoGroupBox, null);
      layout.AddSeparateRow(null);

      TabControl tabControl = new TabControl();

      TabPage formattingPage = new TabPage()
      {
        Text = Rhino.UI.Localization.LocalizeString("Formatting", 25),
        Content = new TableLayout()
        {
          Padding = DefaultPadding,
          Spacing = DefaultSpacing,
          Rows =
          {
            new TableRow(mapZtoY),
            new TableRow(exportMaterials),
            new TableRow(useDoubleSidedMaterials),
            new TableRow(useDisplayColorForUnsetMaterial),
            new TableRow(exportLayers),
            null,
          },
        },
      };

      tabControl.Pages.Add(formattingPage);

      TabPage meshPage = new TabPage()
      {
        Text = Rhino.UI.Localization.LocalizeString("Mesh", 26),
        Content = new TableLayout()
        {
          Padding = DefaultPadding,
          Spacing = DefaultSpacing,
          Rows =
          {
            new TableRow(subdBox),
            new TableRow(exportTextureCoordinates),
            new TableRow(exportVertexNormals),
            new TableRow(exportOpenMeshes),
            new TableRow(exportVertexColors),
            null,
          },
        },
      };

      tabControl.Pages.Add(meshPage);

      TabPage compressionPage = new TabPage()
      {
        Text = Rhino.UI.Localization.LocalizeString("Compression", 27),
        Content = layout,
      };

      tabControl.Pages.Add(compressionPage);

      this.Content = new TableLayout()
      {
        Padding = DefaultPadding,
        Spacing = DefaultSpacing,
        Rows =
        {
          new TableRow(tabControl)
          {
            ScaleHeight = true,
          },
          new TableRow(useSettingsDontShowDialogCheck)
          {
            ScaleHeight = false,
          },
          new TableRow(new TableLayout()
          {
            Padding = DefaultPadding,
            Spacing = DefaultSpacing,
            Rows =
            {
              new TableRow(new TableCell(cancelButton, true), new TableCell(okButton, true)),
            }
          })
          {
            ScaleHeight = false,
          },
        }
      };
    }

    private void OptionsToDialog()
    {
      useSettingsDontShowDialogCheck.Checked = Export_glTFPlugin.UseSavedSettingsDontShowDialog;

      mapZtoY.Checked = Export_glTFPlugin.MapRhinoZToGltfY;
      exportMaterials.Checked = Export_glTFPlugin.ExportMaterials;
      EnableDisableMaterialControls(Export_glTFPlugin.ExportMaterials);
      exportLayers.Checked = Export_glTFPlugin.ExportLayers;

      useDoubleSidedMaterials.Checked = Export_glTFPlugin.UseDoubleSidedMaterials;
      useDisplayColorForUnsetMaterial.Checked = Export_glTFPlugin.UseDisplayColorForUnsetMaterials;

      bool controlNet = Export_glTFPlugin.SubDExportMode == SubDMode.ControlNet;
      useSubdControlNet.Checked = controlNet;
      EnabledDisableSubDLevel(!controlNet);

      subdLevel.Value = Export_glTFPlugin.SubDLevel;

      exportTextureCoordinates.Checked = Export_glTFPlugin.ExportTextureCoordinates;
      exportVertexNormals.Checked = Export_glTFPlugin.ExportVertexNormals;
      exportOpenMeshes.Checked = Export_glTFPlugin.ExportOpenMeshes;
      exportVertexColors.Checked = Export_glTFPlugin.ExportVertexColors;

      useDracoCompressionCheck.Checked = Export_glTFPlugin.UseDracoCompression;
      EnableDisableDracoControls(Export_glTFPlugin.UseDracoCompression);

      dracoCompressionLevelInput.Value = Export_glTFPlugin.DracoCompressionLevel;
      dracoQuantizationBitsInputPosition.Value = Export_glTFPlugin.DracoQuantizationBitsPosition;
      dracoQuantizationBitsInputNormal.Value = Export_glTFPlugin.DracoQuantizationBitsNormal;
      dracoQuantizationBitsInputTexture.Value = Export_glTFPlugin.DracoQuantizationBitsTexture;
    }

    private void DialogToOptions()
    {
      Export_glTFPlugin.UseSavedSettingsDontShowDialog = GetCheckboxValue(useSettingsDontShowDialogCheck);

      Export_glTFPlugin.MapRhinoZToGltfY = GetCheckboxValue(mapZtoY);
      Export_glTFPlugin.ExportMaterials = GetCheckboxValue(exportMaterials);
      Export_glTFPlugin.UseDoubleSidedMaterials = GetCheckboxValue(useDoubleSidedMaterials);
      Export_glTFPlugin.UseDisplayColorForUnsetMaterials = GetCheckboxValue(useDisplayColorForUnsetMaterial);
      Export_glTFPlugin.ExportLayers = GetCheckboxValue(exportLayers);

      bool controlNet = GetCheckboxValue(useSubdControlNet);
      Export_glTFPlugin.SubDExportMode = controlNet ? SubDMode.ControlNet : SubDMode.Surface;

      Export_glTFPlugin.SubDLevel = subdLevel.Value;

      Export_glTFPlugin.ExportTextureCoordinates = GetCheckboxValue(exportTextureCoordinates);
      Export_glTFPlugin.ExportVertexNormals = GetCheckboxValue(exportVertexNormals);
      Export_glTFPlugin.ExportOpenMeshes = GetCheckboxValue(exportOpenMeshes);
      Export_glTFPlugin.ExportVertexColors = GetCheckboxValue(exportVertexColors);

      Export_glTFPlugin.UseDracoCompression = GetCheckboxValue(useDracoCompressionCheck);
      Export_glTFPlugin.DracoCompressionLevel = (int)dracoCompressionLevelInput.Value;
      Export_glTFPlugin.DracoQuantizationBitsPosition = (int)dracoQuantizationBitsInputPosition.Value;
      Export_glTFPlugin.DracoQuantizationBitsNormal = (int)dracoQuantizationBitsInputNormal.Value;
      Export_glTFPlugin.DracoQuantizationBitsTexture = (int)dracoQuantizationBitsInputTexture.Value;
    }

    private bool GetCheckboxValue(CheckBox checkBox)
    {
      return checkBox.Checked.HasValue ? checkBox.Checked.Value : false;
    }

    private void EnabledDisableSubDLevel(bool enable)
    {
      subdLevel.Enabled = enable;
    }

    private void EnableDisableDracoControls(bool enable)
    {
      dracoCompressionLevelInput.Enabled = enable;
      dracoQuantizationBitsInputPosition.Enabled = enable;
      dracoQuantizationBitsInputNormal.Enabled = enable;
      dracoQuantizationBitsInputTexture.Enabled = enable;
    }

    private void UseDracoCompressionCheck_CheckedChanged(object sender, EventArgs e)
    {
      bool useDraco = GetCheckboxValue(useDracoCompressionCheck);

      EnableDisableDracoControls(useDraco);
    }

    private void ExportMaterials_CheckedChanged(object sender, EventArgs e)
    {
      bool enabled = GetCheckboxValue(exportMaterials);

      EnableDisableMaterialControls(enabled);
    }

    private void EnableDisableMaterialControls(bool enabled)
    {
      useDisplayColorForUnsetMaterial.Enabled = enabled;
      useDoubleSidedMaterials.Enabled = enabled;
    }

    private void UseSubdControlNet_CheckedChanged(object sender, EventArgs e)
    {
      bool controlNet = GetCheckboxValue(useSubdControlNet);
      EnabledDisableSubDLevel(!controlNet);
    }

    private void CancelButton_Click(object sender, EventArgs e)
    {
      this.Close(DialogResult.Cancel);
    }

    private void OkButton_Click(object sender, EventArgs e)
    {
      DialogToOptions();

      this.Close(DialogResult.Ok);
    }
  }
}

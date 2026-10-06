using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eto.Forms;
using Rhino.UI.Controls;

namespace Export_glTF
{
  class ExportOptionsDialog : Rhino.UI.Forms.CommandDialog
  {
    private const int GroupGap = 12; // between the sections of a tab
    private const int RowSpacing = 5; // between the rows of a group
    private const int InputWidth = 75;

    private CheckBox mapZtoY = new CheckBox();
    private CheckBox exportMaterials = new CheckBox();
    private CheckBox cullBackfaces = new CheckBox();
    private CheckBox useDisplayColorForUnsetMaterial = new CheckBox();
    private CheckBox exportLayers = new CheckBox();

    private CheckBox useRenderMeshes = new CheckBox();

    private CheckBox useSubdControlNet = new CheckBox();
    private Label subdLevelLabel = new Label();
    private Eto.Forms.Slider subdLevel = new Eto.Forms.Slider();
    private Label subdLevelValue = new Label();

    private CheckBox exportTextureCoordinates = new CheckBox();
    private CheckBox exportVertexNormals = new CheckBox();
    private CheckBox exportOpenMeshes = new CheckBox();
    private CheckBox exportVertexColors = new CheckBox();

    private CheckBox useDracoCompressionCheck = new CheckBox();

    private Label dracoCompressionLabel = new Label();
    private NumericUpDownWithUnitParsing dracoCompressionLevelInput = NewStepper(1, 10);

    private Label dracoQuantizationBitsPositionLabel = new Label();
    private Label dracoQuantizationBitsNormalLabel = new Label();
    private Label dracoQuantizationBitsTextureLabel = new Label();
    private NumericUpDownWithUnitParsing dracoQuantizationBitsInputPosition = NewStepper(8, 32);
    private NumericUpDownWithUnitParsing dracoQuantizationBitsInputNormal = NewStepper(8, 32);
    private NumericUpDownWithUnitParsing dracoQuantizationBitsInputTexture = NewStepper(8, 32);

    private CheckBox useSettingsDontShowDialogCheck = new CheckBox();

    public ExportOptionsDialog()
    {
      HelpButtonClick += (e, s) =>
      {
        Rhino.UI.RhinoHelp.Show("fileio/gltf_import_export.htm");
      };

      Resizable = false;

      Title = Rhino.UI.Localization.LocalizeString("glTF Export Options", 3);

      mapZtoY.Text = Rhino.UI.Localization.LocalizeString("Map Rhino Z to glTF Y", 4);

      exportMaterials.Text = Rhino.UI.Localization.LocalizeString("Export materials", 5);

      cullBackfaces.Text = Rhino.UI.Localization.LocalizeString("Cull backfaces", 28);

      useDisplayColorForUnsetMaterial.Text = Rhino.UI.Localization.LocalizeString("Use display color for objects with no material set", 6);

      exportLayers.Text = Rhino.UI.LOC.STR("Export layers");

      useRenderMeshes.Text = Rhino.UI.Localization.LocalizeString("Use render meshes", 18);

      useSubdControlNet.Text = Rhino.UI.Localization.LocalizeString("Use control net", 9);

      subdLevelLabel.Text = Rhino.UI.Localization.LocalizeString("Subdivision level", 10);

      subdLevel.SnapToTick = true;
      subdLevel.TickFrequency = 1;
      subdLevel.MinValue = 1;
      subdLevel.MaxValue = 5;
      subdLevel.Width = 120;

      // show the level next to the slider
      subdLevelValue.VerticalAlignment = VerticalAlignment.Center;
      subdLevel.ValueChanged += (sender, e) => subdLevelValue.Text = subdLevel.Value.ToString();

      exportTextureCoordinates.Text = Rhino.UI.Localization.LocalizeString("Export texture coordinates", 11);

      exportVertexNormals.Text = Rhino.UI.Localization.LocalizeString("Export vertex normals", 12);

      exportOpenMeshes.Text = Rhino.UI.Localization.LocalizeString("Export open meshes", 13);

      exportVertexColors.Text = Rhino.UI.Localization.LocalizeString("Export vertex colors", 14);

      useDracoCompressionCheck.Text = Rhino.UI.Localization.LocalizeString("Use Draco compression", 15);

      dracoCompressionLabel.Text = Rhino.UI.LOC.STR("Draco compression level");

      dracoQuantizationBitsPositionLabel.Text = Rhino.UI.Localization.LocalizeString("Position", 22);
      dracoQuantizationBitsNormalLabel.Text = Rhino.UI.Localization.LocalizeString("Normal", 23);
      dracoQuantizationBitsTextureLabel.Text = Rhino.UI.Localization.LocalizeString("Texture", 24);

      useSettingsDontShowDialogCheck.Text = Rhino.UI.LOC.STR("Always use these settings and don't show this dialog again");

      OptionsToDialog();

      useDracoCompressionCheck.CheckedChanged += UseDracoCompressionCheck_CheckedChanged;
      exportMaterials.CheckedChanged += ExportMaterials_CheckedChanged;

      useSubdControlNet.CheckedChanged += UseSubdControlNet_CheckedChanged;

      TabControl tabControl = new TabControl();

      tabControl.Pages.Add(new TabPage()
      {
        Text = Rhino.UI.Localization.LocalizeString("Formatting", 25),
        Content = Tab(
          Rows(mapZtoY, exportMaterials, cullBackfaces, useDisplayColorForUnsetMaterial, exportLayers)
        ),
      });

      // all lead-in labels of a tab share one width, so the inputs line up
      var meshLabels = new List<Label>();
      ShareWidth(meshLabels);
      tabControl.Pages.Add(new TabPage()
      {
        Text = Rhino.UI.Localization.LocalizeString("Mesh", 26),
        Content = Tab(
          Section(Rhino.UI.Localization.LocalizeString("SubD Meshing", 8),
            useSubdControlNet,
            LeadIn(meshLabels, subdLevelLabel, new TableLayout() { Spacing = new Eto.Drawing.Size(8, 0), Rows = { new TableRow(subdLevel, subdLevelValue) } })),
          Rows(exportTextureCoordinates, exportVertexNormals, exportOpenMeshes, exportVertexColors, useRenderMeshes)
        ),
      });

      var compressionLabels = new List<Label>();
      ShareWidth(compressionLabels);
      tabControl.Pages.Add(new TabPage()
      {
        Text = Rhino.UI.Localization.LocalizeString("Compression", 27),
        Content = Tab(
          Rows(useDracoCompressionCheck, LeadIn(compressionLabels, dracoCompressionLabel, dracoCompressionLevelInput)),
          Section(Rhino.UI.Localization.LocalizeString("Draco Quantization Bits", 21),
            LeadIn(compressionLabels, dracoQuantizationBitsPositionLabel, dracoQuantizationBitsInputPosition),
            LeadIn(compressionLabels, dracoQuantizationBitsNormalLabel, dracoQuantizationBitsInputNormal),
            LeadIn(compressionLabels, dracoQuantizationBitsTextureLabel, dracoQuantizationBitsInputTexture))
        ),
      });

      // The tabs take any extra height, and the checkbox is a row of the same table, so the
      // dialog is at least as wide as its text
      this.Content = new TableLayout()
      {
        Spacing = new Eto.Drawing.Size(5, GroupGap),
        Rows =
        {
          new TableRow(tabControl) { ScaleHeight = true },
          new TableRow(useSettingsDontShowDialogCheck),
        }
      };
    }

    private static NumericUpDownWithUnitParsing NewStepper(double min, double max)
    {
      return new NumericUpDownWithUnitParsing()
      {
        ShowStepper = true,
        Width = InputWidth,
        DecimalPlaces = 0,
        Increment = 1,
        MinValue = min,
        MaxValue = max,
      };
    }

    /// <summary>A section: a Title Case heading with a divider line, then its rows at the same left edge</summary>
    private static TableLayout Section(string title, params TableRow[] rows)
    {
      var table = new TableLayout() { Spacing = new Eto.Drawing.Size(0, RowSpacing), Rows = { new LabelSeparator() { Text = title } } };
      foreach (var row in rows)
        table.Rows.Add(row);
      return table;
    }

    /// <summary>Rows of checkboxes, RowSpacing apart</summary>
    private static TableLayout Rows(params TableRow[] rows)
    {
      var table = new TableLayout() { Spacing = new Eto.Drawing.Size(0, RowSpacing) };
      foreach (var row in rows)
        table.Rows.Add(row);
      return table;
    }

    /// <summary>A label with its input to the right; the labels collected in <paramref name="labels"/> share one width</summary>
    private static TableLayout LeadIn(List<Label> labels, Label label, Control input)
    {
      label.VerticalAlignment = VerticalAlignment.Center;
      labels.Add(label);
      return new TableLayout() { Spacing = new Eto.Drawing.Size(8, 0), Rows = { new TableRow(label, input, null) } };
    }

    /// <summary>Gives the lead-in labels of a tab the width of the widest one once the dialog has loaded</summary>
    private void ShareWidth(List<Label> labels)
    {
      LoadComplete += (sender, e) =>
      {
        int width = 0;
        foreach (var label in labels)
          width = Math.Max(width, (int)Math.Ceiling(label.GetPreferredSize().Width));
        foreach (var label in labels)
          label.Width = width;
      };
    }

    /// <summary>The content of a tab: groups GroupGap apart, placed at the top of the tab</summary>
    private static Control Tab(params Control[] groups)
    {
      var table = new TableLayout() { Spacing = new Eto.Drawing.Size(0, GroupGap), Padding = new Eto.Drawing.Padding(5) };
      foreach (var group in groups)
        table.Rows.Add(group);
      table.Rows.Add(null);
      return table;
    }

    private void OptionsToDialog()
    {
      useSettingsDontShowDialogCheck.Checked = Export_glTFPlugin.UseSavedSettingsDontShowDialog;

      mapZtoY.Checked = Export_glTFPlugin.MapRhinoZToGltfY;
      exportMaterials.Checked = Export_glTFPlugin.ExportMaterials;
      EnableDisableMaterialControls(Export_glTFPlugin.ExportMaterials);
      exportLayers.Checked = Export_glTFPlugin.ExportLayers;

      cullBackfaces.Checked = Export_glTFPlugin.CullBackfaces;
      useDisplayColorForUnsetMaterial.Checked = Export_glTFPlugin.UseDisplayColorForUnsetMaterials;

      bool controlNet = Export_glTFPlugin.SubDExportMode == SubDMode.ControlNet;
      useSubdControlNet.Checked = controlNet;
      EnabledDisableSubDLevel(!controlNet);

      subdLevel.Value = Export_glTFPlugin.SubDLevel;
      subdLevelValue.Text = subdLevel.Value.ToString();

      useRenderMeshes.Checked = Export_glTFPlugin.UseRenderMeshes;

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

    public void DialogToOptions()
    {
      Export_glTFPlugin.UseSavedSettingsDontShowDialog = GetCheckboxValue(useSettingsDontShowDialogCheck);

      Export_glTFPlugin.MapRhinoZToGltfY = GetCheckboxValue(mapZtoY);
      Export_glTFPlugin.ExportMaterials = GetCheckboxValue(exportMaterials);
      Export_glTFPlugin.CullBackfaces = GetCheckboxValue(cullBackfaces);
      Export_glTFPlugin.UseDisplayColorForUnsetMaterials = GetCheckboxValue(useDisplayColorForUnsetMaterial);
      Export_glTFPlugin.ExportLayers = GetCheckboxValue(exportLayers);

      bool controlNet = GetCheckboxValue(useSubdControlNet);
      Export_glTFPlugin.SubDExportMode = controlNet ? SubDMode.ControlNet : SubDMode.Surface;

      Export_glTFPlugin.SubDLevel = subdLevel.Value;

      Export_glTFPlugin.UseRenderMeshes = GetCheckboxValue(useRenderMeshes);

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
      subdLevelLabel.Enabled = enable;
      subdLevel.Enabled = enable;
      subdLevelValue.Enabled = enable;
    }

    private void EnableDisableDracoControls(bool enable)
    {
      dracoCompressionLabel.Enabled = enable;
      dracoCompressionLevelInput.Enabled = enable;
      dracoQuantizationBitsPositionLabel.Enabled = enable;
      dracoQuantizationBitsNormalLabel.Enabled = enable;
      dracoQuantizationBitsTextureLabel.Enabled = enable;
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
      cullBackfaces.Enabled = enabled;
    }

    private void UseSubdControlNet_CheckedChanged(object sender, EventArgs e)
    {
      bool controlNet = GetCheckboxValue(useSubdControlNet);
      EnabledDisableSubDLevel(!controlNet);
    }
  }
}

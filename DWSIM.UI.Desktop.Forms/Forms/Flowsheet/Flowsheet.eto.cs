﻿using System;
using System.Collections.Generic;
using Eto.Forms;
using Eto.Drawing;
using System.Xml;
using System.Xml.Linq;
using DWSIM.UI.Shared;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using System.Linq;
using System.Threading.Tasks;
using DWSIM.UI.Desktop.Editors;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.Drawing.SkiaSharp.GraphicObjects;
using DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables;
using System.Timers;
using System.Diagnostics;
using DWSIM.Drawing.SkiaSharp.GraphicObjects.Charts;
using System.Reflection;
using s = DWSIM.GlobalSettings.Settings;

namespace DWSIM.UI.Forms
{
    partial class Flowsheet : Form
    {

        public Desktop.Shared.Flowsheet FlowsheetObject;
        public DWSIM.UI.Desktop.Editors.Spreadsheet Spreadsheet;
        private DWSIM.UI.Controls.FlowsheetSurfaceControlBase FlowsheetControl;

        private DocumentControl EditorHolder;

        public Color BGColor = new Color(0.051f, 0.447f, 0.651f);

        private TableLayout SpreadsheetControl;

        private Eto.Forms.Splitter SplitterFlowsheet;

        private TabPage TabPageSpreadsheet;
        private TabControl TabContainer;
        private bool TabSwitch = true;

        private DWSIM.UI.Desktop.Editors.ResultsViewer ResultsControl;

        private DWSIM.UI.Desktop.Editors.MaterialStreamListViewer MaterialStreamListControl;

        private DWSIM.UI.Desktop.Editors.ScriptManagerBase ScriptListControl;

        string imgprefix = "DWSIM.UI.Forms.Resources.Icons.";

        private string backupfilename = "";

        public bool newsim = false;

        ContextMenu selctxmenu, deselctxmenu;

        public Dictionary<string, Interfaces.ISimulationObject> ObjectList = new Dictionary<string, Interfaces.ISimulationObject>();

        public Action ActComps, ActBasis, ActGlobalOptions, ActSave, ActSaveAs, ActOptions, ActZoomIn, ActZoomOut, ActZoomFit, ActSimultAdjustSolver, ActInspector;

        private double sf = s.UIScalingFactor; 

        void InitializeComponent()
        {

            if (s.DarkMode) BGColor = SystemColors.ControlBackground;

            if (Application.Instance.Platform.IsWpf)
            {
                GlobalSettings.Settings.DpiScale = Screen.RealDPI / 96.0;
            }

            // setup backup timer

            backupfilename = DateTime.Now.ToString().Replace('-', '_').Replace(':', '_').Replace(' ', '_').Replace('/', '_') + ".dwxmz";

            var BackupTimer = new Timer(GlobalSettings.Settings.BackupInterval * 60 * 1000);
            BackupTimer.Elapsed += (sender, e) =>
            {
                Task.Factory.StartNew(() => SaveBackupCopy());
            };
            BackupTimer.Enabled = true;
            BackupTimer.Start();

            WindowState = Eto.Forms.WindowState.Maximized;

            FlowsheetObject = new Desktop.Shared.Flowsheet() { FlowsheetForm = this };
            FlowsheetObject.Initialize();

            Title = "New Flowsheet";

            Icon = Eto.Drawing.Icon.FromResource(imgprefix + "DWSIM_ico.ico");

            if (GlobalSettings.Settings.FlowsheetRenderer == GlobalSettings.Settings.SkiaCanvasRenderer.CPU)
            {
                FlowsheetControl = new DWSIM.UI.Controls.FlowsheetSurfaceControl() { FlowsheetObject = FlowsheetObject, FlowsheetSurface = (DWSIM.Drawing.SkiaSharp.GraphicsSurface)FlowsheetObject.GetSurface() };
            }
            else
            {
                FlowsheetControl = new DWSIM.UI.Controls.FlowsheetSurfaceControl_OpenGL() { FlowsheetObject = FlowsheetObject, FlowsheetSurface = (DWSIM.Drawing.SkiaSharp.GraphicsSurface)FlowsheetObject.GetSurface() };
            }

            FlowsheetObject.FlowsheetControl = FlowsheetControl;

            FlowsheetControl.FlowsheetSurface.InvalidateCallback = (() =>
            {
                Application.Instance.Invoke(() =>
                {
                    FlowsheetControl.Invalidate();
                });
            });

            ClientSize = new Size((int)(sf * 1024), (int)(sf * 768));

            // toolbar

            var btnmSave = new ButtonToolItem { ToolTip = "Save Flowsheet", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-save.png")) };

            var btnmSolve = new ButtonToolItem { ToolTip = "Solve Flowsheet", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-play.png")) };
            var btnmSimultSolve = new CheckToolItem { ToolTip = "Enable/Disable Simultaneous Adjust Solver", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "Checked_96px.png")) };

            var btnmComps = new ButtonToolItem { ToolTip = "Compounds", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-thin_test_tube.png")) };
            var btnmBasis = new ButtonToolItem { ToolTip = "Basis", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-math.png")) };
            var btnmOptions = new ButtonToolItem { ToolTip = "Settings", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-sorting_options.png")) };

            var btnmZoomIn = new ButtonToolItem { ToolTip = "Zoom In", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-zoom_in_filled.png")) };
            var btnmZoomOut = new ButtonToolItem { ToolTip = "Zoom Out", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-zoom_out_filled.png")) };
            var btnmZoomFit = new ButtonToolItem { ToolTip = "Zoom to Fit", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-fit_to_page_filled.png")) };

            var btnmInspector = new ButtonToolItem { ToolTip = "Inspector", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-spy_filled.png")) };

            if (Application.Instance.Platform.IsMac)
            {
                btnmSave.Text = "Save";
                btnmSolve.Text = "Solve Flowsheet";
                btnmSimultSolve.Text = "E/D Simult. Adj. Solver";
                btnmComps.Text = "Compounds";
                btnmBasis.Text = "Basis";
                btnmOptions.Text = "Settings";
                btnmZoomIn.Text = "Zoom In";
                btnmZoomOut.Text = "Zoom Out";
                btnmZoomFit.Text = "Zoom to Fit";
                btnmInspector.Text = "Inspector";
            }

            ToolBar = new ToolBar
            {
                Items = { btnmSave, new SeparatorToolItem { Type = SeparatorToolItemType.Space } , btnmComps, btnmBasis, btnmOptions,
                new SeparatorToolItem{ Type = SeparatorToolItemType.Space }, btnmSolve, btnmSimultSolve, new SeparatorToolItem{ Type = SeparatorToolItemType.Space },
                    btnmZoomOut, btnmZoomIn, btnmZoomFit,new SeparatorToolItem{ Type = SeparatorToolItemType.Space }, btnmInspector }
            };

            // menu items

            var btnSave = new ButtonMenuItem { Text = "Save Flowsheet", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-save.png")), Shortcut = Keys.S | Application.Instance.CommonModifier };
            var btnSaveAs = new ButtonMenuItem { Text = "Save As...", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-save_as.png")), Shortcut = Keys.S | Application.Instance.CommonModifier | Keys.Shift };
            var btnClose = new ButtonMenuItem { Text = "Close Flowsheet", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "Delete_96px.png")), Shortcut = Keys.Q | Application.Instance.CommonModifier };
            var btnComps = new ButtonMenuItem { Text = "Compounds", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-thin_test_tube.png")), Shortcut = Keys.C | Application.Instance.AlternateModifier };
            var btnBasis = new ButtonMenuItem { Text = "Basis", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-math.png")), Shortcut = Keys.B | Application.Instance.AlternateModifier };
            var btnOptions = new ButtonMenuItem { Text = "Flowsheet Settings", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-sorting_options.png")), Shortcut = Keys.M | Application.Instance.AlternateModifier };
            var btnGlobalOptions = new ButtonMenuItem { Text = "Global Settings", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-sorting_options.png")), Shortcut = Keys.G | Application.Instance.AlternateModifier };
            var btnSolve = new ButtonMenuItem { Text = "Solve Flowsheet", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-play.png")), Shortcut = Keys.F5 };

            // actions

            ActComps = () =>
            {
                var cont = new TableLayout();
                new DWSIM.UI.Desktop.Editors.Compounds(FlowsheetObject, cont);
                cont.Tag = "Simulation Compounds";

                var cont2 = new Desktop.Editors.CompoundTools(FlowsheetObject);
                cont2.Tag = "Compound Tools";

                var form = UI.Shared.Common.GetDefaultTabbedForm("Compounds", (int)(sf * 920), (int)(sf * 500), new Control[] { cont, cont2 });
                form.Show();
            };

            ActBasis = () =>
            {
                var cont1 = UI.Shared.Common.GetDefaultContainer();
                cont1.Tag = "Thermodynamics";
                new DWSIM.UI.Desktop.Editors.Models(FlowsheetObject, cont1);
                var cont2 = UI.Shared.Common.GetDefaultContainer();
                cont2.Tag = "Reactions";
                new DWSIM.UI.Desktop.Editors.ReactionsManager(FlowsheetObject, cont2);
                var form = UI.Shared.Common.GetDefaultTabbedForm("Simulation Basis", (int)(sf * 800), (int)(sf * 600), new[] { cont1, cont2 });
                form.Show();
                form.Width += 10;
            };

            ActOptions = () =>
            {
                var cont = UI.Shared.Common.GetDefaultContainer();
                new DWSIM.UI.Desktop.Editors.SimulationSettings(FlowsheetObject, cont);
                cont.Tag = "Settings";
                var cont2 = new UI.Desktop.Editors.FloatingTablesView(FlowsheetObject);
                cont2.Tag = "Visible Properties";
                var form = UI.Shared.Common.GetDefaultTabbedForm("Flowsheet Settings", (int)(sf * 800), (int)(sf * 600), new[] { cont, cont2 });
                form.Show();
                form.Width += 1;
            };

            ActGlobalOptions = () =>
            {
                new DWSIM.UI.Forms.Forms.GeneralSettings().GetForm().Show();
            };

            ActSave = () =>
            {
                try
                {
                    if (FlowsheetObject.Options.FilePath != "")
                    {
                        SaveSimulation(FlowsheetObject.Options.FilePath);
                    }
                    else
                    {
                        btnSaveAs.PerformClick();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(),"Error saving file", MessageBoxButtons.OK, MessageBoxType.Error, MessageBoxDefaultButton.OK);
                }
            };

            ActSaveAs = () =>
            {
                var dialog = new SaveFileDialog();
                dialog.Title = "Save File".Localize();
                dialog.Filters.Add(new FileFilter("XML Simulation File (Compressed)".Localize(), new[] { ".dwxmz" }));
                dialog.Filters.Add(new FileFilter("Mobile XML Simulation File (Android/iOS)".Localize(), new[] { ".xml" }));
                dialog.CurrentFilterIndex = 0;
                if (dialog.ShowDialog(this) == DialogResult.Ok)
                {
                    SaveSimulation(dialog.FileName);
                }
            };

            ActZoomIn = () =>
            {
                FlowsheetControl.FlowsheetSurface.Zoom += 0.1f;
                FlowsheetControl.Invalidate();
            };

            ActZoomOut = () =>
            {
                FlowsheetControl.FlowsheetSurface.Zoom -= 0.1f;
                FlowsheetControl.Invalidate();
            };

            ActZoomFit = () =>
            {
                FlowsheetControl.FlowsheetSurface.ZoomAll((int)(FlowsheetControl.Width * GlobalSettings.Settings.DpiScale), (int)(FlowsheetControl.Height * GlobalSettings.Settings.DpiScale));
                FlowsheetControl.FlowsheetSurface.ZoomAll((int)(FlowsheetControl.Width * GlobalSettings.Settings.DpiScale), (int)(FlowsheetControl.Height * GlobalSettings.Settings.DpiScale));
                FlowsheetControl.Invalidate();
            };

            ActInspector = () =>
            {
                var iwindow = DWSIM.Inspector.Window_Eto.GetInspectorWindow();
                var iform = Common.GetDefaultEditorForm("DWSIM - Solution Inspector", (int)(sf * 1024), (int)(sf * 768), iwindow, false);
                iform.WindowState = WindowState.Maximized;
                iform.Show();
            };

            FlowsheetObject.ActBasis = ActBasis;
            FlowsheetObject.ActComps = ActComps;
            FlowsheetObject.ActGlobalOptions = ActGlobalOptions;
            FlowsheetObject.ActOptions = ActOptions;
            FlowsheetObject.ActSave = ActSave;
            FlowsheetObject.ActSaveAs = ActSaveAs;
            FlowsheetObject.ActZoomFit = ActZoomFit;
            FlowsheetObject.ActZoomIn = ActZoomIn;
            FlowsheetObject.ActZoomOut = ActZoomOut;

            // button click events

            btnmInspector.Click += (sender, e) => ActInspector.Invoke();

            btnClose.Click += (sender, e) => Close();

            btnComps.Click += (sender, e) => ActComps.Invoke();
            btnmComps.Click += (sender, e) => ActComps.Invoke();

            btnBasis.Click += (sender, e) => ActBasis.Invoke();
            btnmBasis.Click += (sender, e) => ActBasis.Invoke();

            btnOptions.Click += (sender, e) => ActOptions.Invoke();
            btnmOptions.Click += (sender, e) => ActOptions.Invoke();

            btnGlobalOptions.Click += (sender, e) => ActGlobalOptions.Invoke();

            btnSolve.Click += (sender, e) => SolveFlowsheet();
            btnmSolve.Click += (sender, e) => SolveFlowsheet();

            btnSave.Click += (sender, e) => ActSave.Invoke();
            btnmSave.Click += (sender, e) => ActSave.Invoke();

            btnSaveAs.Click += (sender, e) => ActSaveAs.Invoke();

            btnmZoomOut.Click += (sender, e) => ActZoomOut.Invoke();

            btnmZoomIn.Click += (sender, e) => ActZoomIn.Invoke();

            btnmZoomFit.Click += (sender, e) => ActZoomFit.Invoke();

            var btnUtilities_TrueCriticalPoint = new ButtonMenuItem { Text = "True Critical Point", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-swiss_army_knife.png")) };
            var btnUtilities_BinaryEnvelope = new ButtonMenuItem { Text = "Binary Envelope", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-swiss_army_knife.png")) };
            var btnUtilities_PhaseEnvelope = new ButtonMenuItem { Text = "Phase Envelope", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-swiss_army_knife.png")) };

            btnUtilities_TrueCriticalPoint.Click += (sender, e) =>
            {
                var tcp = new Desktop.Editors.Utilities.TrueCriticalPointView(FlowsheetObject);
                var form = Common.GetDefaultEditorForm("True Critical Point", (int)(sf * 500), (int)(sf * 250), tcp);
                form.Show();
            };

            btnUtilities_BinaryEnvelope.Click += (sender, e) =>
            {
                var bpe = new Desktop.Editors.Utilities.BinaryEnvelopeView(FlowsheetObject);
                var form = Common.GetDefaultEditorForm("Binary Phase Envelope", (int)(sf * 500), (int)(sf * 750), bpe);
                form.Show();
            };

            btnUtilities_PhaseEnvelope.Click += (sender, e) =>
            {
                var pe = new Desktop.Editors.Utilities.PhaseEnvelopeView(FlowsheetObject);
                var form = Common.GetDefaultEditorForm("Phase Envelope", (int)(sf * 500), (int)(sf * 650), pe);
                form.Show();
            };

            var btnObjects = new ButtonMenuItem { Text = "Add New Simulation Object", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-workflow.png")), Shortcut = Keys.A | Application.Instance.AlternateModifier };
            var btnInsertText = new ButtonMenuItem { Text = "Add New Text Block", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "TextWidth_96px.png")) };
            var btnInsertTable = new ButtonMenuItem { Text = "Add New Property Table", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "Grid_96px.png")) };
            var btnInsertMasterTable = new ButtonMenuItem { Text = "Add New Master Property Table", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "GridView_96px.png")) };
            var btnInsertSpreadsheetTable = new ButtonMenuItem { Text = "Add New Linked Spreadsheet Table", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "PivotTable_96px.png")) };
            var btnInsertChartObject = new ButtonMenuItem { Text = "Add New Chart Object", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "AreaChart_100px.png")) };

            var btnSensAnalysis = new ButtonMenuItem { Text = "Sensitivity Analysis", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-maintenance.png")) };
            var btnOptimization = new ButtonMenuItem { Text = "Flowsheet Optimizer", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-maintenance.png")) };

            var btnInspector = new ButtonMenuItem { Text = "Solution Inspector", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-spy_filled.png")) };

            btnInspector.Click += (sender, e) => ActInspector.Invoke();

            btnObjects.Click += (sender, e) =>
            {
                var insform = new DWSIM.UI.Desktop.Editors.InsertObject { Flowsheet = FlowsheetObject, ObjList = ObjectList, FlowsheetHeight = FlowsheetControl.Height };
                insform.ShowModal(this);

            };

            btnSensAnalysis.Click += (sender, e) =>
            {
                var saeditor = new Desktop.Editors.SensAnalysisView(FlowsheetObject);
                var form = Common.GetDefaultEditorForm("Sensitivity Analysis", (int)(sf * 500), (int)(sf * 700), saeditor);
                form.Show();
            };

            btnOptimization.Click += (sender, e) =>
            {
                var foeditor = new Desktop.Editors.OptimizerView(FlowsheetObject);
                var form = Common.GetDefaultEditorForm("Flowsheet Optimizer", (int)(sf * 500), (int)(sf * 700), foeditor);
                form.Show();
            };

            FlowsheetControl.FlowsheetSurface.BackgroundColor = s.DarkMode ? SkiaSharp.SKColors.Black : SkiaSharp.SKColors.White;
            FlowsheetControl.KeyDown += (sender, e) =>
            {
                if (e.Key == Keys.Delete) DeleteObject();
            };
            btnInsertText.Click += (sender, e) =>
            {
                FlowsheetControl.AddObject("Text", 50, 50);
            };
            btnInsertTable.Click += (sender, e) =>
            {
                FlowsheetControl.AddObject("Property Table", 50, 50);
            };
            btnInsertMasterTable.Click += (sender, e) =>
            {
                FlowsheetControl.AddObject("Master Property Table", 50, 50);
            };
            btnInsertSpreadsheetTable.Click += (sender, e) =>
            {
                FlowsheetControl.AddObject("Spreadsheet Table", 50, 50);
            };
            btnInsertChartObject.Click += (sender, e) =>
            {
                FlowsheetControl.AddObject("Chart Object", 50, 50);
            };
            FlowsheetControl.MouseDoubleClick += (sender, e) =>
            {
                if (Application.Instance.Platform.IsMac) FlowsheetControl.FlowsheetSurface.InputRelease();
                var obj = FlowsheetControl.FlowsheetSurface.SelectedObject;
                if (obj == null)
                {
                    FlowsheetControl.FlowsheetSurface.ZoomAll((int)(FlowsheetControl.Width * GlobalSettings.Settings.DpiScale), (int)(FlowsheetControl.Height * GlobalSettings.Settings.DpiScale));
                    FlowsheetControl.FlowsheetSurface.ZoomAll((int)(FlowsheetControl.Width * GlobalSettings.Settings.DpiScale), (int)(FlowsheetControl.Height * GlobalSettings.Settings.DpiScale));
                    FlowsheetControl.Invalidate();
                }
                if (e.Modifiers == Keys.Shift)
                {
                    if (obj == null) return;
                    if (obj.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream ||
                        obj.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.EnergyStream)
                    {
                        return;
                    }
                    EditConnections();
                }
                else if (e.Modifiers == Keys.Alt)
                {
                    if (obj == null) return;
                    ViewSelectedObjectResults();
                }
                else if (e.Modifiers == Keys.Control)
                {
                    if (obj == null) return;
                    DebugObject();
                }
                else
                {
                    if (obj == null) return;
                    EditSelectedObjectProperties();
                }
            };

            var chkSimSolver = new CheckMenuItem { Text = "Simultaneous Adjust Solver Active" };
            chkSimSolver.Checked = FlowsheetObject.Options.SimultaneousAdjustSolverEnabled;
            chkSimSolver.CheckedChanged += (sender, e) =>
            {
                FlowsheetObject.Options.SimultaneousAdjustSolverEnabled = chkSimSolver.Checked;
                btnmSimultSolve.Checked = chkSimSolver.Checked;
            };

            btnmSimultSolve.Checked = FlowsheetObject.Options.SimultaneousAdjustSolverEnabled;
            btnmSimultSolve.CheckedChanged += (sender, e) =>
            {
                FlowsheetObject.Options.SimultaneousAdjustSolverEnabled = btnmSimultSolve.Checked;
                chkSimSolver.Checked = btnmSimultSolve.Checked;
            };

            ActSimultAdjustSolver = () =>
            {
                FlowsheetObject.Options.SimultaneousAdjustSolverEnabled = !FlowsheetObject.Options.SimultaneousAdjustSolverEnabled;
                chkSimSolver.Checked = FlowsheetObject.Options.SimultaneousAdjustSolverEnabled;
                btnmSimultSolve.Checked = FlowsheetObject.Options.SimultaneousAdjustSolverEnabled;
            };

            FlowsheetObject.ActSimultAdjustSolver = ActSimultAdjustSolver;

            // menu items

            Menu = new MenuBar();

            if (Application.Instance.Platform.IsMac)
            {
                var btnfile = (ButtonMenuItem)Menu.Items.Where((x) => x.Text == "&File").FirstOrDefault();
                btnfile.Items.AddRange(new[] { btnSave, btnSaveAs });
            }
            else if (Application.Instance.Platform.IsGtk)
            {
                Menu.Items.Add(new ButtonMenuItem { Text = "File", Items = { btnSave, btnSaveAs, btnClose } });
            }
            else if (Application.Instance.Platform.IsWinForms || Application.Instance.Platform.IsWpf)
            {
                Menu.ApplicationItems.AddRange(new[] { btnSave, btnSaveAs, btnClose });
            }

            var btnShowHideObjectPalette = new ButtonMenuItem { Text = "Show/Hide Object Palette" };

            var btnShowHideObjectEditorPanel = new ButtonMenuItem { Text = "Show/Hide Object Editor Panel" };

            //process plugin list

            var pluginbuttons = new List<ButtonMenuItem>();

            var mform = (MainForm)Application.Instance.MainForm;

            foreach (Interfaces.IUtilityPlugin5 iplugin in mform.plugins)
            {
                ButtonMenuItem tsmi = new ButtonMenuItem();
                tsmi.Text = iplugin.Name;
                tsmi.Tag = iplugin.UniqueID;
                tsmi.Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "Electrical_96px.png"));
                tsmi.Click += (sender, e) =>
                {
                    iplugin.SetFlowsheet(this.FlowsheetObject);
                    Application.Instance.Invoke(() =>
                    {
                        Form f = (Form)iplugin.UtilityForm;
                        f.Show();
                    });
                };
                pluginbuttons.Add(tsmi);
            }

            var pluginsmenu = new ButtonMenuItem { Text = "Plugins" };
            pluginsmenu.Items.AddRange(pluginbuttons);

            switch (GlobalSettings.Settings.RunningPlatform())
            {
                case GlobalSettings.Settings.Platform.Mac:
                    if (Application.Instance.Platform.IsMac)
                    {
                        Menu.Items.Insert(3, new ButtonMenuItem { Text = "Setup", Items = { btnComps, btnBasis, btnOptions, btnGlobalOptions } });
                        Menu.Items.Insert(4, new ButtonMenuItem { Text = "Objects", Items = { btnObjects, btnInsertText, btnInsertTable, btnInsertMasterTable, btnInsertSpreadsheetTable, btnInsertChartObject } });
                        Menu.Items.Insert(5, new ButtonMenuItem { Text = "Solver", Items = { btnSolve, chkSimSolver } });
                        Menu.Items.Insert(6, new ButtonMenuItem { Text = "Tools", Items = { btnSensAnalysis, btnOptimization, btnInspector } });
                        Menu.Items.Insert(7, new ButtonMenuItem { Text = "Utilities", Items = { btnUtilities_TrueCriticalPoint, btnUtilities_PhaseEnvelope, btnUtilities_BinaryEnvelope } });
                        Menu.Items.Insert(7, pluginsmenu);
                        Menu.Items.Insert(7, new ButtonMenuItem { Text = "View", Items = { btnShowHideObjectPalette, btnShowHideObjectEditorPanel } });
                    }
                    else
                    {
                        Menu.Items.Add(new ButtonMenuItem { Text = "Setup", Items = { btnComps, btnBasis, btnOptions, btnGlobalOptions } });
                        Menu.Items.Add(new ButtonMenuItem { Text = "Objects", Items = { btnObjects, btnInsertText, btnInsertTable, btnInsertMasterTable, btnInsertSpreadsheetTable, btnInsertChartObject } });
                        Menu.Items.Add(new ButtonMenuItem { Text = "Solver", Items = { btnSolve, chkSimSolver } });
                        Menu.Items.Add(new ButtonMenuItem { Text = "Tools", Items = { btnSensAnalysis, btnOptimization, btnInspector } });
                        Menu.Items.Add(new ButtonMenuItem { Text = "Utilities", Items = { btnUtilities_TrueCriticalPoint, btnUtilities_PhaseEnvelope, btnUtilities_BinaryEnvelope } });
                        Menu.Items.Add(pluginsmenu);
                        Menu.Items.Add(new ButtonMenuItem { Text = "View", Items = { btnShowHideObjectPalette, btnShowHideObjectEditorPanel } });
                    }
                    break;
                case GlobalSettings.Settings.Platform.Linux:
                case GlobalSettings.Settings.Platform.Windows:
                    Menu.Items.Add(new ButtonMenuItem { Text = "Setup", Items = { btnComps, btnBasis, btnOptions, btnGlobalOptions } });
                    Menu.Items.Add(new ButtonMenuItem { Text = "Objects", Items = { btnObjects, btnInsertText, btnInsertTable, btnInsertMasterTable, btnInsertSpreadsheetTable, btnInsertChartObject } });
                    Menu.Items.Add(new ButtonMenuItem { Text = "Solver", Items = { btnSolve, chkSimSolver } });
                    Menu.Items.Add(new ButtonMenuItem { Text = "Tools", Items = { btnSensAnalysis, btnOptimization, btnInspector } });
                    Menu.Items.Add(new ButtonMenuItem { Text = "Utilities", Items = { btnUtilities_TrueCriticalPoint, btnUtilities_PhaseEnvelope, btnUtilities_BinaryEnvelope } });
                    Menu.Items.Add(pluginsmenu);
                    Menu.Items.Add(new ButtonMenuItem { Text = "View", Items = { btnShowHideObjectPalette, btnShowHideObjectEditorPanel } });
                    break;
            }

            var hitem1 = new ButtonMenuItem { Text = "Online Help", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "help_browser.png")) };
            hitem1.Click += (sender, e) =>
            {
                Process.Start("http://dwsim.inforside.com.br/docs/crossplatform/help/");
            };

            var hitem2 = new ButtonMenuItem { Text = "Support".Localize(), Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "help_browser.png")) };
            hitem2.Click += (sender, e) =>
            {
                Process.Start("http://dwsim.inforside.com.br/wiki/index.php?title=Support");
            };

            var hitem3 = new ButtonMenuItem { Text = "Report a Bug".Localize(), Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "help_browser.png")) };
            hitem3.Click += (sender, e) =>
            {
                Process.Start("https://sourceforge.net/p/dwsim/tickets/");
            };

            var hitem4 = new ButtonMenuItem { Text = "Go to DWSIM's Website".Localize(), Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "help_browser.png")) };
            hitem4.Click += (sender, e) =>
            {
                Process.Start("http://dwsim.inforside.com.br");
            };

            Menu.HelpItems.Add(hitem1);
            Menu.HelpItems.Add(hitem4);
            Menu.HelpItems.Add(hitem2);
            Menu.HelpItems.Add(hitem3);

            Spreadsheet = new DWSIM.UI.Desktop.Editors.Spreadsheet(FlowsheetObject) { ObjList = ObjectList };

            FlowsheetObject.LoadSpreadsheetData = new Action<XDocument>((xdoc) =>
            {
                if (xdoc.Element("DWSIM_Simulation_Data").Element("SensitivityAnalysis") != null)
                {
                    string data1 = xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Element("Data1").Value;
                    string data2 = xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Element("Data2").Value;
                    if (!string.IsNullOrEmpty(data1)) Spreadsheet.CopyDT1FromString(data1);
                    if (!string.IsNullOrEmpty(data2)) Spreadsheet.CopyDT2FromString(data2);
                    Spreadsheet.CopyFromDT();
                    Spreadsheet.EvaluateAll();
                }
            });

            FlowsheetObject.SaveSpreadsheetData = new Action<XDocument>((xdoc) =>
            {
                try { Spreadsheet.CopyToDT(); }
                catch (Exception) { }
                xdoc.Element("DWSIM_Simulation_Data").Add(new XElement("Spreadsheet"));
                xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Add(new XElement("Data1"));
                xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Add(new XElement("Data2"));
                Spreadsheet.CopyToDT();
                xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Element("Data1").Value = Spreadsheet.CopyDT1ToString();
                xdoc.Element("DWSIM_Simulation_Data").Element("Spreadsheet").Element("Data2").Value = Spreadsheet.CopyDT2ToString();
            });

            FlowsheetObject.RetrieveSpreadsheetData = new Func<string, List<string[]>>((range) =>
            {
                return Spreadsheet.GetDataFromRange(range);
            });

            SpreadsheetControl = Spreadsheet.GetSpreadsheet(FlowsheetObject);

            ResultsControl = new DWSIM.UI.Desktop.Editors.ResultsViewer(FlowsheetObject);

            MaterialStreamListControl = new DWSIM.UI.Desktop.Editors.MaterialStreamListViewer(FlowsheetObject);

            if (Application.Instance.Platform.IsMac)
            {
                ScriptListControl = new DWSIM.UI.Desktop.Editors.ScriptManager_Mac(FlowsheetObject);
            }
            else
            {
                ScriptListControl = new DWSIM.UI.Desktop.Editors.ScriptManager(FlowsheetObject);
            }

            LoadObjects();

            var split0 = new Eto.Forms.Splitter();

            SplitterFlowsheet = new Eto.Forms.Splitter();

            SplitterFlowsheet.Orientation = Orientation.Horizontal;
            SplitterFlowsheet.FixedPanel = SplitterFixedPanel.Panel1;

            EditorHolder = new DocumentControl() { AllowReordering = true, BackgroundColor = SystemColors.ControlBackground };

            var PanelEditorsLabel = new Label { Text = "  " + "Object Editors", Font = SystemFonts.Bold(), VerticalAlignment = VerticalAlignment.Bottom, TextColor = Colors.White, Height = (int)(sf * 20) };
            PanelEditorsLabel.Font = new Font(SystemFont.Bold, DWSIM.UI.Shared.Common.GetEditorFontSize());

            var PanelEditorsDescription = new Label { Text = "  " + "Object Editing Panels will appear here.", VerticalAlignment = VerticalAlignment.Bottom, TextColor = Colors.White, Height = (int)(sf * 20) };
            PanelEditorsDescription.Font = new Font(SystemFont.Default, DWSIM.UI.Shared.Common.GetEditorFontSize());

            var PanelEditors = new TableLayout { Rows = { PanelEditorsLabel, PanelEditorsDescription, EditorHolder }, Spacing = new Size(5, 5), BackgroundColor = !s.DarkMode ? BGColor : SystemColors.ControlBackground };

            SplitterFlowsheet.Panel1 = PanelEditors;

            SplitterFlowsheet.Panel1.Width = (int)(sf * 300);
            SplitterFlowsheet.Panel1.Visible = true;

            btnShowHideObjectEditorPanel.Click += (sender, e) =>
            {
                SplitterFlowsheet.Panel1.Visible = !SplitterFlowsheet.Panel1.Visible;
            };

            SplitterFlowsheet.Panel2 = FlowsheetControl;

            split0.Panel1 = SplitterFlowsheet;

            var objcontainer = new StackLayout() { BackgroundColor = !s.DarkMode ? Colors.White : SystemColors.ControlBackground };

            foreach (var obj in ObjectList.Values.OrderBy(x => x.GetDisplayName()))
            {
                if ((Boolean)(obj.GetType().GetProperty("Visible").GetValue(obj)))
                {
                    var pitem = new FlowsheetObjectPanelItem();
                    var bmp = (System.Drawing.Bitmap)obj.GetIconBitmap();
                    pitem.imgIcon.Image = new Bitmap(Common.ImageToByte(bmp));
                    pitem.txtName.Text = obj.GetDisplayName();
                    pitem.txtDescription.Text = obj.GetDisplayDescription();
                    pitem.MouseDown += (sender, e) =>
                    {
                        var dobj = new DataObject();
                        dobj.Image = pitem.imgIcon.Image;
                        dobj.SetString(obj.GetDisplayName(), "ObjectName");
                        pitem.DoDragDrop(dobj, DragEffects.All);
                        e.Handled = true;
                    };
                    objcontainer.Items.Add(pitem);
                }
            }

            if (Application.Instance.Platform.IsWpf) FlowsheetControl.AllowDrop = true;
            FlowsheetControl.DragDrop += (sender, e) =>
            {
                if (e.Data.GetString("ObjectName") != null)
                {
                    FlowsheetObject.AddObject(e.Data.GetString("ObjectName"), (int)(e.Location.X / FlowsheetControl.FlowsheetSurface.Zoom), (int)(e.Location.Y / FlowsheetControl.FlowsheetSurface.Zoom));
                }
            };

            var PanelObjectsLabel = new Label { Text = "  " + "Object Palette", Font = SystemFonts.Bold(), VerticalAlignment = VerticalAlignment.Bottom, TextColor = Colors.White, Height = (int)(sf * 20) };
            PanelObjectsLabel.Font = new Font(SystemFont.Bold, DWSIM.UI.Shared.Common.GetEditorFontSize());

            var PanelObjectsDescription = new Label { Text = "  " + "Drag and drop items to add them to the Flowsheet.", VerticalAlignment = VerticalAlignment.Bottom, TextColor = Colors.White, Height = (int)(sf * 20) };
            PanelObjectsDescription.Font = new Font(SystemFont.Default, DWSIM.UI.Shared.Common.GetEditorFontSize());

            var PanelObjects = new TableLayout { Rows = { PanelObjectsLabel, PanelObjectsDescription, new Scrollable() { Content = objcontainer } }, Spacing = new Size(5, 5), BackgroundColor = BGColor };
            PanelObjects.BackgroundColor = !s.DarkMode ? BGColor : SystemColors.ControlBackground;

            split0.Panel2 = PanelObjects;
            split0.Orientation = Orientation.Horizontal;
            split0.FixedPanel = SplitterFixedPanel.Panel2;
            split0.Panel2.Width = FlowsheetObjectPanelItem.width + 25;

            btnShowHideObjectPalette.Click += (sender, e) =>
            {
                split0.Panel2.Visible = !split0.Panel2.Visible;
            };

            TabContainer = new TabControl();
            TabPageSpreadsheet = new TabPage { Content = SpreadsheetControl, Text = "Spreadsheet" };
            TabContainer.Pages.Add(new TabPage { Content = split0, Text = "Flowsheet" });
            TabContainer.Pages.Add(new TabPage { Content = MaterialStreamListControl, Text = "Material Streams" });
            TabContainer.Pages.Add(TabPageSpreadsheet);
            TabContainer.Pages.Add(new TabPage { Content = ScriptListControl, Text = "Scripts" });
            TabContainer.Pages.Add(new TabPage { Content = ResultsControl, Text = "Results" });

            var split = new Eto.Forms.Splitter();
            split.Panel1 = TabContainer;
            split.Panel2 = SetupLogWindow();
            split.Orientation = Orientation.Vertical;
            split.FixedPanel = SplitterFixedPanel.Panel2;
            split.Panel2.Height = (int)(sf * 100);

            // main container

            Content = split;

            // context menus

            selctxmenu = new ContextMenu();
            deselctxmenu = new ContextMenu();

            // flowsheet mouse up

            FlowsheetControl.FlowsheetSurface.InputReleased += (sender, e) =>
            {
                if (GlobalSettings.Settings.EditOnSelect)
                {
                    var sobj = FlowsheetObject.GetSelectedFlowsheetSimulationObject("");
                    if (sobj != null)
                    {
                        EditObject_New(sobj);
                    }
                }
            };

            FlowsheetControl.MouseUp += (sender, e) =>
            {
                if (e.Buttons == MouseButtons.Alternate)
                {
                    if (Application.Instance.Platform.IsMac) FlowsheetControl.FlowsheetSurface.InputRelease();
                    if (FlowsheetControl.FlowsheetSurface.SelectedObject != null)
                    {
                        var obj = FlowsheetControl.FlowsheetSurface.SelectedObject;
                        switch (obj.ObjectType)
                        {
                            case Interfaces.Enums.GraphicObjects.ObjectType.GO_Table:
                            case Interfaces.Enums.GraphicObjects.ObjectType.GO_MasterTable:
                            case Interfaces.Enums.GraphicObjects.ObjectType.GO_SpreadsheetTable:
                                selctxmenu.Items.Clear();
                                var itemtype = new ButtonMenuItem { Text = "Data Table", Enabled = false };
                                selctxmenu.Items.Add(itemtype);

                                var menuitem0 = new ButtonMenuItem { Text = "Edit", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "EditProperty_96px.png")) };
                                menuitem0.Click += (sender2, e2) =>
                                {
                                    EditSelectedObjectProperties();
                                };

                                selctxmenu.Items.Add(menuitem0);

                                var item7 = new ButtonMenuItem { Text = "Copy Data to Clipboard", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-copy_2_filled.png")) };

                                item7.Click += (sender2, e2) =>
                                {
                                    new Clipboard().Text = obj.GetType().GetProperty("ClipboardData").GetValue(obj).ToString();
                                };

                                selctxmenu.Items.Add(item7);

                                var delitem = new ButtonMenuItem { Text = "Delete", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "Delete_96px.png")) };
                                delitem.Click += (sender2, e2) =>
                                {
                                    if (MessageBox.Show(this, "Confirm object removal?", "Delete Object", MessageBoxButtons.YesNo, MessageBoxType.Question, MessageBoxDefaultButton.No) == DialogResult.Yes)
                                    {
                                        FlowsheetObject.DeleteSelectedObject(this, new EventArgs(), obj, false, false);
                                    }
                                };
                                selctxmenu.Items.Add(delitem);
                                break;
                            case Interfaces.Enums.GraphicObjects.ObjectType.GO_Text:
                            case Interfaces.Enums.GraphicObjects.ObjectType.GO_Image:
                            case Interfaces.Enums.GraphicObjects.ObjectType.GO_Chart:
                                selctxmenu.Items.Clear();
                                var itemtype2 = new ButtonMenuItem { Text = "Misc Object", Enabled = false };
                                selctxmenu.Items.Add(itemtype2);

                                var menuitem02 = new ButtonMenuItem { Text = "Edit", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "EditProperty_96px.png")) };
                                menuitem02.Click += (sender2, e2) =>
                                {
                                    EditSelectedObjectProperties();
                                };

                                selctxmenu.Items.Add(menuitem02);

                                var item7a = new ButtonMenuItem { Text = "Copy Data to Clipboard", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-copy_2_filled.png")) };

                                item7a.Click += (sender2, e2) =>
                                {
                                    new Clipboard().Text = obj.GetType().GetProperty("ClipboardData").GetValue(obj).ToString();
                                };

                                selctxmenu.Items.Add(item7a);

                                var delitem2 = new ButtonMenuItem { Text = "Delete", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "Delete_96px.png")) };
                                delitem2.Click += (sender2, e2) =>
                                {
                                    if (MessageBox.Show(this, "Confirm object removal?", "Delete Object", MessageBoxButtons.YesNo, MessageBoxType.Question, MessageBoxDefaultButton.No) == DialogResult.Yes)
                                    {
                                        FlowsheetObject.DeleteSelectedObject(this, new EventArgs(), obj, false, false);
                                    }
                                };
                                selctxmenu.Items.Add(delitem2);
                                break;
                            default:
                                SetupSelectedContextMenu();
                                break;
                        }

                        selctxmenu.Show(FlowsheetControl);
                    }
                    else
                    {
                        SetupDeselectedContextMenu();
                        deselctxmenu.Show(FlowsheetControl);
                    }
                }
            };

            // additional events

            Closing += Flowsheet_Closing;

            Closed += (sender, e) =>
            {
                SaveUserUnits();
                FlowsheetObject.ProcessScripts(Interfaces.Enums.Scripts.EventType.SimulationClosed, Interfaces.Enums.Scripts.ObjectType.Simulation, "");
            };

            Shown += Flowsheet_Shown;

            FlowsheetObject.HighLevelSolve = () => SolveFlowsheet();

        }

        public void SolveFlowsheet()
        {
            FlowsheetObject.UpdateSpreadsheet(() =>
            {
                Spreadsheet.EvaluateAll();
                Spreadsheet.WriteAll();
            });
            FlowsheetObject.FinishedSolving = (() =>
            {
                Application.Instance.AsyncInvoke(() =>
                {
                    ResultsControl.UpdateList();
                    MaterialStreamListControl.UpdateList();
                    UpdateEditorPanels();
                });
            });
            FlowsheetObject.SolveFlowsheet(false);
            FlowsheetObject.UpdateSpreadsheet(() =>
            {
                Spreadsheet.EvaluateAll();
            });
        }

        void Flowsheet_Shown(object sender, EventArgs e)
        {

            FlowsheetControl.FlowsheetSurface.ZoomAll((int)(FlowsheetControl.Width * GlobalSettings.Settings.DpiScale), (int)(FlowsheetControl.Height * GlobalSettings.Settings.DpiScale));
            FlowsheetControl.FlowsheetSurface.ZoomAll((int)(FlowsheetControl.Width * GlobalSettings.Settings.DpiScale), (int)(FlowsheetControl.Height * GlobalSettings.Settings.DpiScale));
            FlowsheetControl.Invalidate();

            ScriptListControl.UpdateList();

            if (Application.Instance.Platform.IsWpf)
            {

                TabContainer.SelectedIndexChanged += (sender2, e2) =>
                {
                    if (TabSwitch)
                    {
                        Task.Delay(100).ContinueWith((t) =>
                        {
                            TabSwitch = false;
                            Application.Instance.Invoke(() =>
                            {
                                TabContainer.SelectedIndex = 0;
                                this.Enabled = true;
                            });
                        });
                    }
                };

                //this.Enabled = false;
                TabContainer.SelectedPage = TabPageSpreadsheet;

            }

            if (newsim)
            {
                var sswiz = new SimulationSetupWizard(this.FlowsheetObject);
                sswiz.DisplayPage0(this);
            }

        }

        void Flowsheet_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBox.Show(this, "ConfirmFlowsheetExit".Localize(), "FlowsheetExit".Localize(), MessageBoxButtons.YesNo, MessageBoxType.Question, MessageBoxDefaultButton.No) == DialogResult.No)
            {
                e.Cancel = true;
            }
        }

        void SaveSimulation(string path, bool backup = false)
        {


            if (System.IO.Path.GetExtension(path).ToLower() == ".dwxmz")
            {
                Application.Instance.Invoke(() => ScriptListControl.UpdateScripts());

                path = Path.ChangeExtension(path, ".dwxmz");

                string xmlfile = Path.ChangeExtension(Path.GetTempFileName(), "xml");

                using (var fstream = new FileStream(xmlfile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    FlowsheetObject.SaveToXML().Save(fstream);
                }

                var i_Files = new List<string>();
                if (File.Exists(xmlfile))
                    i_Files.Add(xmlfile);

                ZipOutputStream strmZipOutputStream = default(ZipOutputStream);

                strmZipOutputStream = new ZipOutputStream(File.Create(path));

                strmZipOutputStream.SetLevel(9);

                if (FlowsheetObject.Options.UsePassword)
                    strmZipOutputStream.Password = FlowsheetObject.Options.Password;

                string strFile = null;

                foreach (string strFile_loopVariable in i_Files)
                {
                    strFile = strFile_loopVariable;
                    FileStream strmFile = File.OpenRead(strFile);
                    byte[] abyBuffer = new byte[strmFile.Length];

                    strmFile.Read(abyBuffer, 0, abyBuffer.Length);
                    ZipEntry objZipEntry = new ZipEntry(Path.GetFileName(strFile));

                    objZipEntry.DateTime = DateTime.Now;
                    objZipEntry.Size = strmFile.Length;
                    strmFile.Close();
                    strmZipOutputStream.PutNextEntry(objZipEntry);
                    strmZipOutputStream.Write(abyBuffer, 0, abyBuffer.Length);

                }

                strmZipOutputStream.Finish();
                strmZipOutputStream.Close();

                File.Delete(xmlfile);
            }
            else if (System.IO.Path.GetExtension(path).ToLower() == ".xml")
            {
                using (var fstream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    FlowsheetObject.SaveToMXML().Save(fstream);
                }
            }

            if (!backup)
            {
                FlowsheetObject.Options.FilePath = path;
                Title = FlowsheetObject.Options.SimulationName + " [" + FlowsheetObject.Options.FilePath + "]";
                FlowsheetObject.ShowMessage("Simulation file successfully saved to '" + path + "'.", Interfaces.IFlowsheet.MessageType.Information);
                FlowsheetObject.ProcessScripts(Interfaces.Enums.Scripts.EventType.SimulationSaved, Interfaces.Enums.Scripts.ObjectType.Simulation, "");
            }
            else
            {
                FlowsheetObject.ShowMessage("Backup file successfully saved to '" + path + "'.", Interfaces.IFlowsheet.MessageType.Information);
            }

        }

        void LoadObjects()
        {

            var dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            var calculatorassembly = System.Reflection.Assembly.LoadFile(Path.Combine(dir, "DWSIM.Thermodynamics.dll"));
            var unitopassembly = System.Reflection.Assembly.LoadFile(Path.Combine(dir, "DWSIM.UnitOperations.dll"));
            List<Type> availableTypes = new List<Type>();

            availableTypes.AddRange(calculatorassembly.GetTypes().Where(x => x.GetInterface("DWSIM.Interfaces.ISimulationObject") != null ? true : false));
            availableTypes.AddRange(unitopassembly.GetTypes().Where(x => x.GetInterface("DWSIM.Interfaces.ISimulationObject") != null ? true : false));

            List<ListItem> litems = new List<ListItem>();

            foreach (var item in availableTypes.OrderBy(x => x.Name))
            {
                if (!item.IsAbstract)
                {
                    var obj = (Interfaces.ISimulationObject)Activator.CreateInstance(item);
                    ObjectList.Add(obj.GetDisplayName(), obj);
                }
            }

        }

        Eto.Forms.Container SetupLogWindow()
        {

            var label = new Label { Text = "  " + "Information/Log Panel", Font = SystemFonts.Bold(), VerticalAlignment = VerticalAlignment.Bottom, TextColor = Colors.White, Height = (int)(sf*20) };
            label.Font = new Font(SystemFont.Bold, DWSIM.UI.Shared.Common.GetEditorFontSize());

            var outtxt = new RichTextArea();
            outtxt.Font = new Font(SystemFont.Default, DWSIM.UI.Shared.Common.GetEditorFontSize());
            outtxt.ReadOnly = true;
            outtxt.SelectionBold = true;

            var container = new TableLayout { Rows = { label, outtxt }, Spacing = new Size(5, 5) };

            container.BackgroundColor = BGColor;

            var ctxmenu0 = new ContextMenu();

            var menuitem0 = new ButtonMenuItem { Text = "Clear List" };

            menuitem0.Click += (sender, e) =>
            {
                outtxt.Text = "";
            };

            ctxmenu0.Items.Add(menuitem0);
            
            outtxt.MouseUp += (sender, e) =>
            {
                if (e.Buttons == MouseButtons.Alternate)
                {
                    ctxmenu0.Show(outtxt);
                }
            };

            FlowsheetObject.SetMessageListener((string text, Interfaces.IFlowsheet.MessageType mtype) =>
            {
                Application.Instance.AsyncInvoke(() =>
                {
                    var item = "[" + DateTime.Now.ToString() + "] " + text;
                    try
                    {
                        outtxt.Append(item, true);
                        outtxt.Selection = new Range<int>(outtxt.Text.Length - item.Length, outtxt.Text.Length -1);
                        switch (mtype)
                        {
                            case Interfaces.IFlowsheet.MessageType.Information:
                                if (s.DarkMode)
                                {
                                    outtxt.SelectionForeground = Colors.SteelBlue;
                                }
                                else
                                {
                                    outtxt.SelectionForeground = Colors.Blue;
                                }
                                break;
                            case Interfaces.IFlowsheet.MessageType.GeneralError:
                                if (s.DarkMode)
                                {
                                    outtxt.SelectionForeground = Colors.Salmon;
                                }
                                else
                                {
                                    outtxt.SelectionForeground = Colors.Red;
                                }
                                break;
                            case Interfaces.IFlowsheet.MessageType.Warning:
                                if (s.DarkMode)
                                {
                                    outtxt.SelectionForeground = Colors.Orange;
                                }
                                else
                                {
                                    outtxt.SelectionForeground = Colors.DarkOrange;
                                }
                                break;
                            case Interfaces.IFlowsheet.MessageType.Tip:
                                if (s.DarkMode)
                                {
                                    outtxt.SelectionForeground = Colors.White;
                                }
                                else
                                {
                                    outtxt.SelectionForeground = Colors.Black;
                                }
                                break;
                            case Interfaces.IFlowsheet.MessageType.Other:
                                if (s.DarkMode)
                                {
                                    outtxt.SelectionForeground = Colors.White;
                                }
                                else
                                {
                                    outtxt.SelectionForeground = Colors.Black;
                                }
                                break;
                            default:
                                break;
                        }
                        outtxt.SelectionBold = true;
                        outtxt.Append("\n", true);
                        outtxt.Selection = new Range<int>(outtxt.Text.Length);
                    }
                    catch { }

                });
            });

            return container;

        }

        void SetupSelectedContextMenu()
        {

            selctxmenu.Items.Clear();

            var obj = FlowsheetObject.GetSelectedFlowsheetSimulationObject(null);

            var item0 = new ButtonMenuItem { Text = obj.GraphicObject.Tag, Enabled = false };

            var item1 = new CheckMenuItem { Text = "Toggle Active/Inactive", Checked = obj.GraphicObject.Active };

            item1.CheckedChanged += (sender, e) =>
            {
                obj.GraphicObject.Active = item1.Checked;
                obj.GraphicObject.Status = item1.Checked ? Interfaces.Enums.GraphicObjects.Status.Idle : Interfaces.Enums.GraphicObjects.Status.Inactive;
            };

            var item3 = new ButtonMenuItem { Text = "Calculate", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-play.png")) };
            item3.Click += (sender, e) => FlowsheetObject.SolveFlowsheet(false, obj);

            var item4 = new ButtonMenuItem { Text = "Debug", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "Console_96px.png")) };
            item4.Click += (sender, e) =>
            {
                DebugObject();
            };

            var selobj = FlowsheetControl.FlowsheetSurface.SelectedObject;

            var menuitem0 = new ButtonMenuItem { Text = "Edit/View", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "EditProperty_96px.png")) };
            menuitem0.Click += (sender, e) =>
            {
                var simobj = FlowsheetObject.GetSelectedFlowsheetSimulationObject(null);
                if (simobj == null) return;
                EditObject_New(simobj);
            };

            var item5 = new ButtonMenuItem { Text = "Clone", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "Copy_96px.png")) };
            item5.Click += (sender, e) =>
            {
                var isobj = FlowsheetObject.AddObject(obj.GraphicObject.ObjectType, obj.GraphicObject.X + 50, obj.GraphicObject.Y + 50, obj.GraphicObject.Tag + "_CLONE");
                var id = isobj.Name;
                ((Interfaces.ICustomXMLSerialization)isobj).LoadData(((Interfaces.ICustomXMLSerialization)obj).SaveData());
                isobj.Name = id;
                if (obj.GraphicObject.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream)
                {
                    foreach (var phase in ((DWSIM.Thermodynamics.Streams.MaterialStream)isobj).Phases.Values)
                    {
                        foreach (var comp in FlowsheetObject.SelectedCompounds.Values)
                        {
                            phase.Compounds[comp.Name].ConstantProperties = comp;
                        }
                    }
                }
            };

            var item6 = new ButtonMenuItem { Text = "Delete", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "Delete_96px.png")) };

            item6.Click += (sender, e) =>
            {
                DeleteObject();
            };

            var item7 = new ButtonMenuItem { Text = "Copy Data to Clipboard", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-copy_2_filled.png")) };

            item7.Click += (sender, e) =>
            {
                //copy all simulation properties from the selected object to clipboard
                try
                {
                    var sobj = FlowsheetControl.FlowsheetSurface.SelectedObject;
                    ((SharedClasses.UnitOperations.BaseClass)FlowsheetObject.SimulationObjects[sobj.Name]).CopyDataToClipboard((DWSIM.SharedClasses.SystemsOfUnits.Units)FlowsheetObject.FlowsheetOptions.SelectedUnitSystem, FlowsheetObject.FlowsheetOptions.NumberFormat);
                }
                catch (Exception ex)
                {
                    FlowsheetObject.ShowMessage("Error copying data to clipboard: " + ex.ToString(), Interfaces.IFlowsheet.MessageType.GeneralError);
                }
            };

            selctxmenu.Items.AddRange(new MenuItem[] { item0, item1, new SeparatorMenuItem(), menuitem0, item7, new SeparatorMenuItem(), item3, item4, new SeparatorMenuItem(), item5, item6 });

            if (obj.GraphicObject.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream)
            {
                bool cancopy;
                if (!obj.GraphicObject.InputConnectors[0].IsAttached)
                {
                    cancopy = true;
                }
                else
                {
                    if (obj.GraphicObject.InputConnectors[0].AttachedConnector.AttachedFrom.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.OT_Recycle)
                    {
                        cancopy = true;
                    }
                    else
                    {
                        cancopy = false;
                    }
                }
                if (cancopy)
                {
                    var aitem1 = new ButtonMenuItem { Text = "Copy Data From...", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "Copy_96px.png")) };
                    foreach (var mstr in FlowsheetObject.SimulationObjects.Values.Where((x) => x is Thermodynamics.Streams.MaterialStream))
                    {
                        if (mstr.GraphicObject.Tag != obj.GraphicObject.Tag)
                        {
                            var newtsmi = new ButtonMenuItem { Text = mstr.GraphicObject.Tag };
                            newtsmi.Click += (sender, e) =>
                            {
                                var obj1 = FlowsheetObject.SimulationObjects[obj.Name];
                                var obj2 = FlowsheetObject.GetSelectedFlowsheetSimulationObject(newtsmi.Text);
                                ((Thermodynamics.Streams.MaterialStream)obj1).Assign((Thermodynamics.Streams.MaterialStream)obj2);
                                SolveFlowsheet();
                            };
                            if (mstr.GraphicObject.Calculated) aitem1.Items.Add(newtsmi);
                        }
                    }
                    selctxmenu.Items.Insert(5, aitem1);
                }
                var aitem2 = new ButtonMenuItem { Text = "Split Stream", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-line_spliting_filled.png")) };
                aitem2.Click += (sender, e) =>
                {
                    try
                    {

                        var stream = FlowsheetControl.FlowsheetSurface.SelectedObject;
                        var isobj = FlowsheetObject.AddObject(obj.GraphicObject.ObjectType, obj.GraphicObject.X + 20, obj.GraphicObject.Y, obj.GraphicObject.Tag + "_CLONE");
                        var id = isobj.Name;
                        ((Interfaces.ICustomXMLSerialization)isobj).LoadData(((Interfaces.ICustomXMLSerialization)obj).SaveData());
                        isobj.Name = id;
                        foreach (var phase in ((DWSIM.Thermodynamics.Streams.MaterialStream)isobj).Phases.Values)
                        {
                            foreach (var comp in FlowsheetObject.SelectedCompounds.Values)
                            {
                                phase.Compounds[comp.Name].ConstantProperties = comp;
                            }
                        }
                        isobj.GraphicObject.Status = stream.Status;
                        Interfaces.IGraphicObject objfrom;
                        int fromidx;
                        if (stream.InputConnectors[0].IsAttached)
                        {
                            objfrom = stream.InputConnectors[0].AttachedConnector.AttachedFrom;
                            fromidx = stream.InputConnectors[0].AttachedConnector.AttachedFromConnectorIndex;
                            FlowsheetObject.DisconnectObjects(objfrom, stream);
                            FlowsheetObject.ConnectObjects(objfrom, isobj.GraphicObject, fromidx, 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        FlowsheetObject.ShowMessage("Error splitting Material Stream: " + ex.ToString(), Interfaces.IFlowsheet.MessageType.GeneralError);
                    }
                };
                selctxmenu.Items.Insert(5, aitem2);
            }

            return;

        }

        private void DebugObject()
        {
            var obj = FlowsheetObject.GetSelectedFlowsheetSimulationObject(null);
            if (obj == null) return;
            var txt = new TextArea { ReadOnly = true, Wrap = true, Font = s.RunningPlatform() == s.Platform.Mac ? new Font("Menlo", s.ResultsReportFontSize) : Fonts.Monospace(s.ResultsReportFontSize) };
            txt.Text = "Please wait, debugging object...";
            var form1 = DWSIM.UI.Shared.Common.CreateDialog(txt, "Debugging" + " " + obj.GraphicObject.Tag + "...", 500, 600);
            Task.Factory.StartNew(() => { return obj.GetDebugReport(); }).ContinueWith(t => { Application.Instance.Invoke(() => { txt.Text = t.Result; }); }, TaskContinuationOptions.ExecuteSynchronously);
            form1.ShowModal(this);
        }

        void SetupDeselectedContextMenu()
        {

            deselctxmenu.Items.Clear();

            var item0 = new ButtonMenuItem { Text = "Add New Object", Image = new Bitmap(Eto.Drawing.Bitmap.FromResource(imgprefix + "icons8-workflow.png")) };

            int currposx = (int)Mouse.Position.X - Location.X;
            int currposy = (int)Mouse.Position.Y - Location.Y;

            foreach (var item in ObjectList.Values)
            {
                var menuitem = new ButtonMenuItem { Text = item.GetDisplayName() };
                menuitem.Click += (sender2, e2) =>
                {
                    var z = FlowsheetControl.FlowsheetSurface.Zoom;
                    FlowsheetObject.AddObject(item.GetDisplayName(), (int)(currposx / z), (int)(currposy / z), "");
                    FlowsheetControl.Invalidate();
                };
                item0.Items.Add(menuitem);
            }

            deselctxmenu.Items.AddRange(new MenuItem[] { item0 });

            return;

        }

        void EditConnections()
        {
            var obj = FlowsheetObject.GetSelectedFlowsheetSimulationObject(null);
            if (obj == null) return;
            var cont = UI.Shared.Common.GetDefaultContainer();
            UI.Shared.Common.CreateAndAddLabelRow(cont, "Object Connections".Localize());
            UI.Shared.Common.CreateAndAddDescriptionRow(cont, "ConnectorsEditorDescription".Localize());
            new DWSIM.UI.Desktop.Editors.ConnectionsEditor(obj, cont);
            var form = UI.Shared.Common.GetDefaultEditorForm(obj.GraphicObject.Tag + " - Edit Connections", 500, 500, cont);
            form.ShowInTaskbar = true;
            form.Topmost = true;
            form.Show();
            form.Width += 1;
        }

        void EditAppearance()
        {
            var obj = FlowsheetObject.GetSelectedFlowsheetSimulationObject(null);
            if (obj == null) return;
            if (obj.GraphicObject is ShapeGraphic)
            {
                var form = UI.Shared.Common.GetDefaultEditorForm(obj.GraphicObject.Tag + " - Edit Appearance", 500, 500, new ObjectAppearanceEditorView(FlowsheetObject, (ShapeGraphic)obj.GraphicObject));
                form.ShowInTaskbar = true;
                form.Topmost = true;
                form.Show();
                form.Width += 1;
            }
        }

        private void EditSelectedObjectProperties()
        {
            Interfaces.IGraphicObject selobj;

            selobj = FlowsheetControl.FlowsheetSurface.SelectedObject;

            if (selobj != null)
            {
                if (selobj.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.GO_Table)
                {
                    var editor = new DWSIM.UI.Desktop.Editors.Tables.PropertyTableEditor { Table = (TableGraphic)selobj };
                    editor.ShowInTaskbar = true;
                    editor.Topmost = true;
                    editor.Show();
                }
                else if (selobj.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.GO_SpreadsheetTable)
                {
                    var editor = new DWSIM.UI.Desktop.Editors.Tables.SpreadsheetTableEditor { Table = (SpreadsheetTableGraphic)selobj };
                    editor.ShowInTaskbar = true;
                    editor.Topmost = true;
                    editor.Show();
                }
                else if (selobj.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.GO_MasterTable)
                {
                    var editor = new DWSIM.UI.Desktop.Editors.Tables.MasterPropertyTableEditor { Table = (MasterTableGraphic)selobj };
                    editor.ShowInTaskbar = true;
                    editor.Topmost = true;
                    editor.Show();
                }
                else if (selobj.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.GO_Chart)
                {
                    var editor = new DWSIM.UI.Desktop.Editors.Charts.ChartObjectEditor((OxyPlotGraphic)selobj);
                    editor.ShowInTaskbar = true;
                    editor.Topmost = true;
                    editor.Show();
                }
                else if (selobj.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.GO_Text)
                {
                    var txtobj = (TextGraphic)selobj;
                    var dyn1 = new DynamicLayout();
                    var fontsizes = new List<string>() { "6", "7", "8", "9", "10", "11", "12", "13", "14", "16", "18", "20", "22", "24" };
                    dyn1.CreateAndAddDropDownRow("Font size", fontsizes, fontsizes.IndexOf(txtobj.Size.ToString("N0")), (sender, e) => txtobj.Size = double.Parse(fontsizes[sender.SelectedIndex]));
                    var container = new TableLayout { Padding = new Padding(10), Spacing = new Size(5, 5) };
                    container.Rows.Add(new TableRow(dyn1));
                    var txt = new TextArea { Text = txtobj.Text };
                    txt.TextChanged += (sender2, e2) =>
                    {
                        txtobj.Text = txt.Text;
                    };
                    container.Rows.Add(new TableRow(txt));
                    var editor = UI.Shared.Common.GetDefaultEditorForm("Edit Text Object", 500, 500, container, false);
                    editor.ShowInTaskbar = true;
                    editor.Topmost = true;
                    editor.Show();
                }
            }
        }

        private void ViewSelectedObjectResults()
        {

            var obj = FlowsheetObject.GetSelectedFlowsheetSimulationObject(null);
            if (obj == null) return;

            var report = obj.GetReport(FlowsheetObject.Options.SelectedUnitSystem, System.Globalization.CultureInfo.CurrentCulture, FlowsheetObject.Options.NumberFormat);
            var container = new TableLayout();
            new DWSIM.UI.Desktop.Editors.Results(obj, container);
            var form = UI.Shared.Common.GetDefaultEditorForm(obj.GraphicObject.Tag + " - View Results", 500, 500, container, true);
            form.ShowInTaskbar = true;
            form.Show();
            form.Width += 1;

        }

        private void DeleteObject()
        {
            var obj = FlowsheetObject.GetSelectedFlowsheetSimulationObject(null);
            if (obj == null) return;
            if (MessageBox.Show(this, "Confirm object removal?", "Delete Object", MessageBoxButtons.YesNo, MessageBoxType.Question, MessageBoxDefaultButton.No) == DialogResult.Yes)
            {
                var editor = EditorHolder.Pages.Where(x => (string)x.Content.Tag == obj.Name).FirstOrDefault();
                if (editor != null)
                {
                    EditorHolder.Pages.Remove(editor);
                }
                FlowsheetObject.DeleteSelectedObject(this, new EventArgs(), obj.GraphicObject, false, false);
            }
        }

        private void SaveBackupCopy()
        {
            try
            {
                string backupdir = "";
                if (GlobalSettings.Settings.RunningPlatform() == GlobalSettings.Settings.Platform.Mac)
                {
                    backupdir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Documents", "DWSIM Application Data", "Backup") + Path.DirectorySeparatorChar;
                }
                else
                {
                    backupdir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DWSIM Application Data", "Backup") + Path.DirectorySeparatorChar;
                }
                if (!Directory.Exists(backupdir)) Directory.CreateDirectory(backupdir);
                if (GlobalSettings.Settings.EnableBackupCopies)
                {
                    if (FlowsheetObject.Options.FilePath != "")
                    {
                        backupfilename = Path.GetFileName(FlowsheetObject.Options.FilePath);
                    }
                    var savefile = Path.Combine(backupdir, backupfilename);
                    SaveSimulation(savefile, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving backup file: " + ex.ToString());
                FlowsheetObject.ShowMessage("Error saving backup file: " + ex.Message.ToString(), Interfaces.IFlowsheet.MessageType.GeneralError);
            }
        }

        private void SaveUserUnits()
        {

            var userunits = new List<DWSIM.SharedClasses.SystemsOfUnits.Units>();
            var toadd = new List<DWSIM.SharedClasses.SystemsOfUnits.Units>();

            try
            {
                userunits = Newtonsoft.Json.JsonConvert.DeserializeObject<List<DWSIM.SharedClasses.SystemsOfUnits.Units>>(GlobalSettings.Settings.UserUnits);
            }
            catch { }

            foreach (var unit in FlowsheetObject.AvailableSystemsOfUnits)
            {
                foreach (var unit2 in userunits)
                {
                    if (unit.Name == unit2.Name)
                    {
                        unit2.LoadData(((DWSIM.SharedClasses.SystemsOfUnits.Units)unit).SaveData());
                    }
                }
            }

            var names = userunits.Select((x) => x.Name).ToList();
            var defaults = new string[] { "SI", "CGS", "ENG", "C1", "C2", "C3", "C4", "C5" };

            foreach (var unit in FlowsheetObject.AvailableSystemsOfUnits)
            {
                if (!defaults.Contains(unit.Name) && !names.Contains(unit.Name))
                {
                    userunits.Add((DWSIM.SharedClasses.SystemsOfUnits.Units)unit);
                }
            }

            GlobalSettings.Settings.UserUnits = Newtonsoft.Json.JsonConvert.SerializeObject(userunits, Newtonsoft.Json.Formatting.Indented).Replace("\"", "\'");

        }

        private void EditObject_New(Interfaces.ISimulationObject obj)
        {

            var existingeditor = EditorHolder.Pages.Where(x => x.Content.Tag.ToString() == obj.Name).FirstOrDefault();

            if (existingeditor != null)
            {
                EditorHolder.SelectedPage = (DocumentPage)existingeditor;
            }
            else
            {
                var editor = new ObjectEditorContainer(obj);
                var editorc = new DocumentPage(editor) { Closable = true, Text = obj.GraphicObject.Tag };
                EditorHolder.Pages.Add(editorc);
                EditorHolder.SelectedPage = editorc;
                if (EditorHolder.Pages.Count > 6)
                {
                    try
                    {
                        EditorHolder.Pages.Remove(EditorHolder.Pages.First());
                    }
                    catch { }
                }
            }

            SplitterFlowsheet.Invalidate();

        }

        private void UpdateEditorPanels()
        {
            foreach (DocumentPage item in EditorHolder.Pages)
            {
                ((ObjectEditorContainer)item.Content).Update();
            }
        }

    }
}

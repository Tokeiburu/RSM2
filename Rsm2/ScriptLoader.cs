using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.IO;
using GRF.Image;
using GRF.System;
using GRF.Threading;
using Microsoft.CSharp;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;
using Utilities;
using Utilities.Extension;
using Utilities.Hash;
using Action = System.Action;
using Debug = Utilities.Debug;

namespace ActEditor.Core {
	/// <summary>
	/// The ScriptLoader class manages the scripts used by the software.
	/// </summary>
	public class ScriptLoader : IDisposable {
		public const string OutputPath = "Scripts";
		public const string OverrideIndex = "__IndexOverride";
		internal static string[] ScriptNames = new string[] { "script_sample", "script0_magnify", "script0_magnifyAll", "script1_replace_color", "script1_replace_color_all", "script2_expand", "script3_mirror_frame", "script4_generate_single_sprite", "script5_remove_unused_sprites" };
		internal static string[] Libraries = new string[] {"GRF.dll", "Utilities.dll", "TokeiLibrary.dll", "ErrorManager.dll"};
		private static ConfigAsker _librariesConfiguration;
		private readonly FileSystemWatcher _fsw;
		private readonly object _lock = new object();
		private readonly int _procId;
		private DockPanel _dockPanel;
		private Menu _menu;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptLoader" /> class.
		/// </summary>
		public ScriptLoader() {
			_fsw = new FileSystemWatcher();
			_procId = Process.GetCurrentProcess().Id;

			TemporaryFilesManager.UniquePattern(_procId + "_script_{0:0000}");

			string path = GrfPath.Combine(OutputPath);

			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			_fsw.Path = path;

			_fsw.Filter = "*.cs";
			_fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
			_fsw.Changed += _fileChanged;
			_fsw.Created += _fileChanged;
			_fsw.Renamed += _fileChanged;
			_fsw.Deleted += _fileChanged;
			_fsw.EnableRaisingEvents = true;
		}

		/// <summary>
		/// Gets the ConfigAsker for the compiled libraries
		/// </summary>
		public static ConfigAsker LibrariesConfiguration {
			get { return _librariesConfiguration ?? (_librariesConfiguration = new ConfigAsker(GrfPath.Combine(GrfEditorConfiguration.ProgramDataPath, OutputPath, "scripts.dat"))); }
		}

		/// <summary>
		/// Recompiles the scripts.
		/// </summary>
		public void RecompileScripts() {
			// Deleting the config asker's properties will
			// force a full reload of all the scripts.
			LibrariesConfiguration.DeleteKeys("");
			ReloadScripts();
		}

		/// <summary>
		/// Reloads the libraries.
		/// </summary>
		public void ReloadLibraries() {
			try {
				Libraries.ToList().ForEach(v => Debug.Ignore(() => File.WriteAllBytes(GrfPath.Combine(GrfEditorConfiguration.ProgramDataPath, OutputPath, v), ApplicationManager.GetResource(v, true))));
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		/// <summary>
		/// Adds a script object in the menu.
		/// </summary>
		/// <param name="actScript">The act script.</param>
		/// <param name="actEditor">The act editor.</param>
		/// <param name="menu">The menu.</param>
		/// <param name="dockPanel">The dock panel, this value can be null.</param>
		public void AddScriptsToMenu(IActScript actScript, ActEditorWindow actEditor, Menu menu, FrameworkElement dockPanel) {
			_setupMenuItem(actScript, menu, actEditor, _generateScriptMenu(actEditor, actScript), null);
			_setupSize(menu, dockPanel);
		}

		public void DeleteDlls() {
			foreach (string dll in Directory.GetFiles(GrfPath.Combine(GrfEditorConfiguration.ProgramDataPath, OutputPath), "*.dll")) {
				if (!Libraries.Contains(Path.GetFileName(dll)) && !File.Exists(dll.ReplaceExtension(".cs"))) {
					GrfPath.Delete(dll);
				}
			}
		}

		/// <summary>
		/// Gets the script object from an assembly path.
		/// </summary>
		/// <param name="assemblyPath">The assembly path.</param>
		/// <returns></returns>
		public static IActScript GetScriptObjectFromAssembly(string assemblyPath) {
			Assembly assembly = Assembly.LoadFile(assemblyPath);
			object o = assembly.CreateInstance("Scripts.Script");

			if (o == null) throw new Exception("Couldn't instantiate the script object. Type not found?");

			IActScript actScript = o as IActScript;

			if (actScript == null) throw new Exception("Couldn't instantiate the script object. Type not found?");

			return actScript;
		}

		/// <summary>
		/// Compiles the specified script.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <param name="dll">The path of the new DLL.</param>
		/// <returns>The result of the compilation</returns>
		internal static CompilerResults Compile(string script, out string dll) {
			if (File.Exists(script.ReplaceExtension(".dll"))) {
				GrfPath.Delete(script.ReplaceExtension(".dll"));
			}

			Dictionary<string, string> providerOptions = new Dictionary<string, string> {
				{"CompilerVersion", "v3.5"}
			};
			CSharpCodeProvider provider = new CSharpCodeProvider(providerOptions);

			string newPath = script.ReplaceExtension(".dll");

			CompilerParameters compilerParams = new CompilerParameters {
				GenerateExecutable = false,
				OutputAssembly = newPath,
			};

			foreach (AssemblyName name in Assembly.GetExecutingAssembly().GetReferencedAssemblies()) {
				compilerParams.ReferencedAssemblies.Add(name.Name + ".dll");
			}

			var res = provider.CompileAssemblyFromFile(compilerParams, script);
			dll = newPath;
			return res;
		}

		/// <summary>
		/// Verifies that example scripts are in the Scripts folder and that they are compiled.
		/// </summary>
		public static void VerifyExampleScriptsInstalled() {
			string path = GrfPath.Combine(GrfEditorConfiguration.ProgramDataPath, OutputPath);

			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			try {
				Libraries.ToList().ForEach(v => Debug.Ignore(() => File.WriteAllBytes(GrfPath.Combine(GrfEditorConfiguration.ProgramDataPath, OutputPath, v), ApplicationManager.GetResource(v, true))));
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}

			foreach (string resource in ScriptNames) {
				bool modified = false;

				foreach (string file in new string[] {resource + ".cs", resource + ".dll"}) {
					string filePath = GrfPath.Combine(path, file);

					if (!File.Exists(filePath)) {
						File.WriteAllBytes(filePath, ApplicationManager.GetResource(file));
						modified = true;
					}
				}

				if (modified) {
					LibrariesConfiguration["[" + Path.GetFileName(resource + ".cs").GetHashCode() + "]"] = new Md5Hash().ComputeHash(File.ReadAllBytes(GrfPath.Combine(path, resource + ".cs"))) + "," + new Md5Hash().ComputeHash(File.ReadAllBytes(GrfPath.Combine(path, resource + ".dll")));
				}
			}
		}

		private void _fileChanged(object sender, FileSystemEventArgs e) {
			try {
				// Raising events is turned off to avoid 
				// receiving the event twice.
				_fsw.EnableRaisingEvents = false;
				ReloadScripts();
			}
			finally {
				_fsw.EnableRaisingEvents = true;
			}
		}

		/// <summary>
		/// Setups the margin of the dock panel for the Undo and Redo buttons.
		/// </summary>
		/// <param name="menu">The menu.</param>
		/// <param name="dockPanel">The dock panel.</param>
		private static void _setupSize(ItemsControl menu, FrameworkElement dockPanel) {
			if (dockPanel == null) return;

			double length = 0;
			var items = menu.Items.Cast<MenuItem>().ToList();

			if (items.Count > 0 && !items.Last().IsLoaded) {
				items.Last().Loaded += delegate {
					foreach (MenuItem mi in menu.Items) {
						length += mi.DesiredSize.Width;
					}

					dockPanel.Margin = new Thickness(length, 0, 0, 0);
				};
			}
			else {
				foreach (MenuItem mi in menu.Items) {
					length += mi.DesiredSize.Width;
				}

				dockPanel.Margin = new Thickness(length, 0, 0, 0);
			}
		}

		/// <summary>
		/// Setups the margin of the dock panel for the Undo and Redo buttons.
		/// </summary>
		/// <param name="dockPanel">The dock panel.</param>
		/// <param name="toAdd">To add.</param>
		private static void _setupSize(ItemsControl menu, FrameworkElement dockPanel, ICollection<MenuItem> toAdd) {
			double length = 0;
			menu.Dispatch(delegate {
				foreach (var mi in toAdd) {
					menu.Items.Add(mi);
				}

				if (toAdd.Count > 0 && !toAdd.Last().IsLoaded) {
					toAdd.Last().Loaded += delegate {
						foreach (MenuItem mi in menu.Items) {
							length += mi.DesiredSize.Width;
						}

						dockPanel.Margin = new Thickness(length, 0, 0, 0);
					};
				}
				else {
					foreach (MenuItem mi in menu.Items) {
						length += mi.DesiredSize.Width;
					}

					dockPanel.Margin = new Thickness(length, 0, 0, 0);
				}
			});
		}

		/// <summary>
		/// Setups the menu item for both the menu and the script menu item.
		/// </summary>
		/// <param name="actScript">The act script.</param>
		/// <param name="menu">The menu bar.</param>
		/// <param name="actEditor">The act editor.</param>
		/// <param name="scriptMenu">The script menu.</param>
		/// <param name="toAdd">List of menu items to add in the menu.</param>
		private void _setupMenuItem(IActScript actScript, Menu menu, ActEditorWindow actEditor, UIElement scriptMenu, List<MenuItem> toAdd) {
			MenuItem menuItem = _retrieveConcernedMenuItem(actScript, menu, toAdd);

			menuItem.SubmenuOpened += delegate {
				int actionIndex = -1;
				int frameIndex = -1;
				int[] selectedLayers = new int[] {};

				var tab = actEditor.GetSelectedTab();

				if (tab == null) {
					return;
				}

				if (tab.Act != null) {
					actionIndex = tab._frameSelector.SelectedAction;
					frameIndex = tab._frameSelector.SelectedFrame;
					selectedLayers = tab.SelectionEngine.CurrentlySelected.OrderBy(p => p).ToArray();
				}

				scriptMenu.IsEnabled = actScript.CanExecute(tab.Act, actionIndex, frameIndex, selectedLayers);
			};

			string[] parameters = _getParameter(actScript, OverrideIndex);

			if (parameters != null && parameters.Length > 0) {
				int ival;

				if (Int32.TryParse(parameters[0], out ival)) {
					menuItem.Items.Insert(ival, scriptMenu);
				}
			}
			else {
				menuItem.Items.Add(scriptMenu);
			}
		}

		/// <summary>
		/// Retrieves a parameter.
		/// </summary>
		/// <param name="actScript">The act script.</param>
		/// <param name="parameter">The parameter to look for.</param>
		/// <returns>Returns the parameter if it's found; null otherwise.</returns>
		private static string[] _getParameter(IActScript actScript, string parameter) {
			string res = actScript.DisplayName as string;

			if (res == null) return null;

			int indexOfParam = res.IndexOf(parameter, 0, StringComparison.OrdinalIgnoreCase);

			if (indexOfParam > -1) {
				int indexOfEndParam = res.IndexOf("__", indexOfParam + parameter.Length, StringComparison.Ordinal);

				if (indexOfEndParam > -1) {
					string[] parameters = res.Substring(indexOfParam + parameter.Length, indexOfEndParam - (indexOfParam + parameter.Length)).Split(new string[] {","}, StringSplitOptions.RemoveEmptyEntries);

					return parameters;
				}
			}

			return null;
		}

		/// <summary>
		/// Gets the display string of a menu item's header.
		/// </summary>
		/// <param name="menuItem">The menu item.</param>
		/// <returns>Returns the string of a menu item's header.</returns>
		private static string _getString(HeaderedItemsControl menuItem) {
			Label header = menuItem.Header as Label;
			return header != null ? header.Content.ToString() : menuItem.Header.ToString();
		}

		/// <summary>
		/// Retrieves the menu item associated with the act script object's group property.
		/// If it is not found, it is automatically added.
		/// </summary>
		/// <param name="actScript">The act script.</param>
		/// <param name="menu">The menu.</param>
		/// <param name="toAdd">To add.</param>
		/// <returns></returns>
		private static MenuItem _retrieveConcernedMenuItem(IActScript actScript, Menu menu, List<MenuItem> toAdd) {
			if (actScript.Group.Contains("/") && toAdd == null) {
				// Script is requesting a submenu group
				string[] groups = actScript.Group.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);

				List<MenuItem> menuItems = menu.Items.Cast<MenuItem>().ToList();
				MenuItem menuItem = null;
				ItemCollection collection = menu.Items;

				foreach (string group in groups) {
					menuItem = menuItems.FirstOrDefault(p => _getString(p) == group);

					if (menuItem == null) {
						if (group == groups[0])
							menuItem = new MenuItem {Header = new Label {Content = group, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(-5, 0, -5, 0)}};
						else
							menuItem = new MenuItem {Header = group};

						collection.Add(menuItem);
					}

					menuItems = menuItem.Items.OfType<MenuItem>().ToList();
					collection = menuItem.Items;
				}

				return menuItem;
			}

			{
				MenuItem menuItem = menu.Items.Cast<MenuItem>().FirstOrDefault(p => _getString(p) == actScript.Group);

				if (toAdd != null) {
					if (menuItem == null) {
						menuItem = toAdd.FirstOrDefault(p => _getString(p) == actScript.Group);
					}
				}

				if (menuItem == null) {
					menuItem = new MenuItem {Header = new Label {Content = actScript.Group, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(-5, 0, -5, 0)}};

					if (toAdd != null) {
						toAdd.Add(menuItem);
					}
					else {
						menu.Items.Add(menuItem);
					}
				}

				return menuItem;
			}
		}

		/// <summary>
		/// Generates the script menu from the act script's display name property.
		/// </summary>
		/// <param name="actEditor">The act editor.</param>
		/// <param name="actScript">The act script.</param>
		/// <returns>The menu item for the script's display name property.</returns>
		private MenuItem _generateScriptMenu(ActEditorWindow actEditor, IActScript actScript) {
			MenuItem scriptMenu = new MenuItem();

			if (actScript.DisplayName is string) {
				string res = actScript.DisplayName.ToString();
				int indexOfEnd = res.IndexOf("__%", 0, StringComparison.Ordinal);

				if (indexOfEnd > -1)
					scriptMenu.Header = res.Substring(indexOfEnd + 3);
				else
					scriptMenu.Header = res;
			}
			else {
				scriptMenu.Header = actScript.DisplayName;
			}

			if (actScript.InputGesture != null) scriptMenu.InputGestureText = actScript.InputGesture.Split(new char[] { ':' }).FirstOrDefault();
			if (actScript.Image != null) scriptMenu.Icon = new Image {Source = GetImage(actScript.Image)};

			Action action = delegate {
				try {
					int actionIndex = -1;
					int frameIndex = -1;
					int[] selectedLayers = new int[] { };

					var tab = actEditor.GetSelectedTab();

					if (tab == null) {
						return;
					}

					if (tab.Act != null) {
						actionIndex = tab._frameSelector.SelectedAction;
						frameIndex = tab._frameSelector.SelectedFrame;
						selectedLayers = tab.SelectionEngine.CurrentlySelected.OrderBy(p => p).ToArray();
					}

					if (actScript.CanExecute(tab.Act, actionIndex, frameIndex, selectedLayers)) {
						int commandCount = -1;

						if (tab.Act != null) {
							commandCount = tab.Act.Commands.CommandIndex;
						}

						actScript.Execute(tab.Act, actionIndex, frameIndex, selectedLayers);

						if (tab.Act != null) {
							if (tab.Act.Commands.CommandIndex == commandCount) {
								return;
							}
						}

						tab._frameSelector.Update();
					}
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err);
				}
			};

			if (actScript.InputGesture != null) {
				foreach (var gesture in actScript.InputGesture.Split(':')) {
					ApplicationShortcut.Link(ApplicationShortcut.FromString(gesture, ((actScript.DisplayName is string) ? actScript.DisplayName.ToString() : gesture + "_cmd")), action, actEditor);
				}
			}

			scriptMenu.Click += (s, e) => action();

			return scriptMenu;
		}

		private void _addFromScript(string script, string localCopy, List<MenuItem> toAdd) {
			try {
				string dll;
				var results = Compile(localCopy, out dll);

				if (results.Errors.Count != 0)
					throw new Exception(String.Join("\r\n", results.Errors.Cast<CompilerError>().ToList().Select(p => p.ToString()).ToArray()));

				LibrariesConfiguration["[" + Path.GetFileName(script).GetHashCode() + "]"] = new Md5Hash().ComputeHash(File.ReadAllBytes(script)) + "," + new Md5Hash().ComputeHash(File.ReadAllBytes(dll));

				GrfPath.Delete(localCopy);
				GrfPath.Delete(script.ReplaceExtension(".dll"));
				Debug.Ignore(() => File.Copy(dll, script.ReplaceExtension(".dll")));

				_addScriptFromAssembly(dll, toAdd);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _addScriptFromAssembly(string assemblyPath, List<MenuItem> toAdd) {
			Assembly assembly = Assembly.LoadFile(assemblyPath);
			object o = assembly.CreateInstance("Scripts.Script");

			if (o == null) throw new Exception("Couldn't instantiate the script object. Type not found?");

			IActScript actScript = o as IActScript;

			if (actScript == null) throw new Exception("Couldn't instantiate the script object. Type not found?");

			_addActScript(actScript, toAdd);
		}

		private void _addActScript(IActScript actScript, List<MenuItem> toAdd) {
			_menu.Dispatch(() => _setupMenuItem(actScript, _menu, _actEditor, _generateScriptMenu(_actEditor, actScript), toAdd));
		}

		/// <summary>
		/// Retrieves the image from an image path (given by the act script object's properties).
		/// </summary>
		/// <param name="image">The image.</param>
		/// <returns></returns>
		public static ImageSource GetImage(string image) {
			var im = ApplicationManager.GetResourceImage(image);

			if (im != null) {
				return WpfImaging.FixDPI(im);
			}

			var path = GrfPath.Combine(GrfEditorConfiguration.ProgramDataPath, OutputPath, image);

			if (File.Exists(path)) {
				byte[] data = File.ReadAllBytes(path);
				GrfImage grfImage = new GrfImage(ref data);
				return grfImage.Cast<BitmapSource>();
			}

			return null;
		}

		protected virtual void Dispose(bool disposing) {
			if (disposing) {
				if (_fsw != null) _fsw.Dispose();
			}
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.VCProjectEngine;

namespace TundraExts
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[Guid(PackageGuids.GuidTundraPackageString)]
	//[ProvideOptionPageAttribute(typeof(OptionsPageGeneral), "Tundra", "General", 100, 101, true, new string[] { "General options for Tundra Extension" })]
	//[ProvideProfileAttribute(typeof(OptionsPageGeneral), "Tundra", "General Options", 100, 101, true, DescriptionResourceID = 100)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
	[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
	public sealed class TundraPackage : AsyncPackage
	{
		public TundraPackage()
		{
			// Inside this method you can place any initialization code that does not require
			// any Visual Studio service because at this point the package object is created but
			// not sited yet inside Visual Studio environment. The place to do all the other
			// initialization is the Initialize method.
		}

		#region Package Members

		/// <summary>
		/// Initialization of the package; this method is called right after the package is sited, so this is the place
		/// where you can put all the initialization code that rely on services provided by VisualStudio.
		/// </summary>
		protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            BuildOutputPane = GetPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid);

            DTE = (DTE2)GetService(typeof(DTE));
			CommandService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
			SolutionService = GetService(typeof(SVsSolution)) as IVsSolution;

			if (CommandService != null)
			{
				// Initialize commands
				Commands.TundraBuild.Initialize(this);
			}
			else
				TundraOutputPane.OutputString("Failed to register commands!\n");
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				DeletePane(OutputPaneGuid);

			base.Dispose(disposing);
		}

		#endregion

		public static VCConfiguration FindVCConfiguration(VCProject project, Configuration active_cfg)
		{
			foreach (VCConfiguration vc_cfg in project.Configurations as IVCCollection)
			{
				// todo: This is hacky, but vc_cfg.Platform throws sometimes. Why?
				var split = vc_cfg.Name.Split(new char[] { '|' });
				//if (vc_cfg.Platform.Name == active_cfg.PlatformName && vc_cfg.ConfigurationName == active_cfg.ConfigurationName)
				if (split[1] == active_cfg.PlatformName && split[0] == active_cfg.ConfigurationName)
					return vc_cfg;
			}

			return null;
		}

		public static Project GetProject(Document doc)
		{
			if (doc != null)
			{
				var pi = doc.ProjectItem as ProjectItem;
				if (pi != null)
					return pi.ContainingProject;
			}

			return null;
		}

		public Project GetBuildProject()
		{
			foreach (var proj in LoadedProjects)
			{
				var p = GetProject(proj);
				if (p != null && p.Name == "Build This Solution")
					return p;
			}

			return null;
		}

		public static VCNMakeTool GetNMakeTool(VCProject project, VCConfiguration config)
		{
			if (config != null)
			{
				var tools = config.Tools as IVCCollection;
				if (tools != null)
					return tools.Item("NMake Tool");
			}

			return null;
		}

		public static Match GetBuildCommandLine(Project project, Configuration config)
		{
			VCProject vcProject = project.Object as VCProject;
			VCConfiguration vcConfig = FindVCConfiguration(vcProject, config);
			var nmake = GetNMakeTool(vcProject, vcConfig);
			if (nmake != null)
			{
				string cmdline = nmake.BuildCommandLine;
				return TundraBuildRegex.Match(cmdline);
			}

			return null;
		}

		public static bool IsTundraProject(Project project)
		{
			if (project == null)
				return false;

			Console.Write(project.Name.ToString());

			var vc_proj = project.Object as VCProject;
			if (null == vc_proj || (vc_proj.keyword != "MakefileProj" && vc_proj.keyword != "Win32Proj"))
				return false;

			var activeConfig = project.ConfigurationManager.ActiveConfiguration;
			if (activeConfig != null)
			{
				var config = FindVCConfiguration(vc_proj, activeConfig);
				VCNMakeTool nmakeTool = GetNMakeTool(vc_proj, config);
				if (nmakeTool != null)
				{
					Match m = TundraBuildRegex.Match(nmakeTool.BuildCommandLine);
					return m.Success;
				}
			}

			return false;
		}

		public IEnumerable<IVsProject> LoadedProjects
		{
			get
			{
				IEnumHierarchies enumerator = null;
				Guid guid = Guid.Empty;
				SolutionService.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION, ref guid, out enumerator);
				IVsHierarchy[] hierarchy = new IVsHierarchy[1] { null };
				uint fetched = 0;
				for (enumerator.Reset(); enumerator.Next(1, hierarchy, out fetched) == VSConstants.S_OK && fetched == 1;)
					yield return (IVsProject)hierarchy[0];
			}
		}

		public static Project GetProject(IVsProject proj)
		{
			var hier = (IVsHierarchy)proj;
			object ext = null;
			if (hier.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ExtObject, out ext) == VSConstants.S_OK)
				return ext as Project;
			return null;
		}

		public bool IsTundraSolution()
		{
			foreach (var it in LoadedProjects)
			{
				var proj = GetProject(it);
				if (proj != null)
				{
					if (IsTundraProject(proj))
						return true;
				}
			}

			return false;
		}

		public static Regex TundraBuildRegex = new Regex("^(\"?.*?tundra2\\.exe\"?)\\s+-C\\s+(\"?[^\"]+\"?).*?([\\w_]+-[\\w_]+-[\\w_]+-[\\w_]+)(.*)$");
		private static Guid OutputPaneGuid = new Guid("34C41266-BF10-488D-A1AE-A008FC9B2FC2");

		public DTE2 DTE { get; private set; }
		public OleMenuCommandService CommandService { get; private set; }
		public IVsSolution SolutionService { get; private set; }

		IVsOutputWindowPane m_tundraOutputPane;
		public IVsOutputWindowPane TundraOutputPane
		{
			get
			{
				if (m_tundraOutputPane == null)
					m_tundraOutputPane = CreatePane(OutputPaneGuid, "Tundra", true, false);
				return m_tundraOutputPane;
			}
		}

		IVsOutputWindowPane m_buildPane;
		public IVsOutputWindowPane BuildOutputPane
		{
			get
			{
				return m_buildPane != null ? m_buildPane : TundraOutputPane;
			}

			private set
			{
				m_buildPane = value;
			}
		}

		internal IVsOutputWindowPane GetPane(Guid paneGuid)
		{
			IVsOutputWindow service = (IVsOutputWindow)GetService(typeof(SVsOutputWindow));
			IVsOutputWindowPane pane = null;
			service.GetPane(ref paneGuid, out pane);
			return pane;
		}

		internal IVsOutputWindowPane CreatePane(Guid paneGuid, string title, bool visible, bool clearWithSolution)
		{
			IVsOutputWindow service = (IVsOutputWindow)GetService(typeof(SVsOutputWindow));
			service.CreatePane(ref paneGuid, title, Convert.ToInt32(visible), Convert.ToInt32(clearWithSolution));

			IVsOutputWindowPane pane;
			service.GetPane(ref paneGuid, out pane);

			return pane;
		}

		internal void DeletePane(Guid paneGuid)
		{
			IVsOutputWindow service = (IVsOutputWindow)GetService(typeof(SVsOutputWindow));
			service.DeletePane(ref paneGuid);
		}

		internal System.Diagnostics.Process CreateTundraProcess(string tundraPath, string workingDir, string arguments)
		{
			System.Diagnostics.Process p = new System.Diagnostics.Process();
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.CreateNoWindow = true;
			p.StartInfo.RedirectStandardOutput = true;
			p.StartInfo.RedirectStandardError = true;
			p.StartInfo.WorkingDirectory = workingDir;
			p.StartInfo.FileName = tundraPath;
			p.StartInfo.Arguments = string.Format("-C {0} {1}", workingDir, arguments);

			return p;
		}
	}
}

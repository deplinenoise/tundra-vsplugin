using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Text.RegularExpressions;

namespace TundraExts.Commands
{
	internal sealed class TundraBuild
	{
		private readonly TundraPackage Package;

		private static Dictionary<int, string> s_cmdNames = new Dictionary<int, string>();

		private TundraBuild(TundraPackage package)
		{
			Package = package;

			EventHandler queryStatusHandler = (object s, EventArgs e) =>
			{
				var mc = s as OleMenuCommand;
				mc.Visible = Package.LoadedProjects.GetEnumerator().MoveNext();
				if (!mc.Visible)
					return;

				mc.Text = string.Format("{0}{1}",
					s_cmdNames.ContainsKey(mc.CommandID.ID) ? s_cmdNames[mc.CommandID.ID] : mc.CommandID.ToString(),
					Package.IsTundraSolution() ? " with Tundra" : ""
					);
			};

			var compile = new OleMenuCommand((s, e) => Execute(BuildTask.Compile), new CommandID(PackageGuids.GuidTundraPackageCmdSet, PackageIds.TundraCompileId));
			s_cmdNames.Add(PackageIds.TundraCompileId, "Compile");
			compile.BeforeQueryStatus += queryStatusHandler;
			Package.CommandService.AddCommand(compile);

			var build = new OleMenuCommand((s, e) => Execute(BuildTask.Build), new CommandID(PackageGuids.GuidTundraPackageCmdSet, PackageIds.TundraBuildSolutionId));
			s_cmdNames.Add(PackageIds.TundraBuildSolutionId, "Build Solution");
			build.BeforeQueryStatus += queryStatusHandler;
			Package.CommandService.AddCommand(build);

			var rebuild = new OleMenuCommand((s, e) => Execute(BuildTask.Rebuild), new CommandID(PackageGuids.GuidTundraPackageCmdSet, PackageIds.TundraRebuildSolutionId));
			s_cmdNames.Add(PackageIds.TundraRebuildSolutionId, "Rebuild Solution");
			rebuild.BeforeQueryStatus += queryStatusHandler;
			Package.CommandService.AddCommand(rebuild);

			var clean = new OleMenuCommand((s, e) => Execute(BuildTask.Clean), new CommandID(PackageGuids.GuidTundraPackageCmdSet, PackageIds.TundraCleanSolutionId));
			s_cmdNames.Add(PackageIds.TundraCleanSolutionId, "Clean Solution");
			clean.BeforeQueryStatus += queryStatusHandler;
			Package.CommandService.AddCommand(clean);
		}

		public static void Initialize(TundraPackage package)
		{
			Instance = new TundraBuild(package);
		}

		public static TundraBuild Instance
		{
			get;
			private set;
		}

		internal enum BuildTask
		{
			Compile,
			Build,
			Rebuild,
			Clean,
		}

		public void Execute(BuildTask task)
		{
			var messagePane = Package.TundraOutputPane;
			var buildPane = Package.BuildOutputPane;

			try
			{
				Project proj = null;
				if (task == BuildTask.Compile)
					proj = TundraPackage.GetProject(Package.DTE.ActiveDocument);
				else
					proj = Package.GetBuildProject();

				bool regularCommand = false;
				if (proj != null)
				{
					Match m = TundraPackage.GetBuildCommandLine(proj, proj.ConfigurationManager.ActiveConfiguration);
					if (m != null && m.Success)
					{
						Package.DTE.ExecuteCommand("File.SaveAll");

						if (task == BuildTask.Compile && !Package.DTE.ActiveDocument.Saved)
							Package.DTE.ActiveDocument.Save();

						try
						{
							var tundraPath = m.Groups[1].ToString();
							var dir = m.Groups[2].ToString().Trim(new char[] { '"' });
							var config = m.Groups[3].ToString();

							string projects = string.Empty;
							string arguments = string.Empty;
							int projectCount = 0;
							if (task == BuildTask.Compile)
							{
								projects = proj.Name.ToString();
								projectCount = 1;
								arguments = string.Format("\"{0}\" {1}", Package.DTE.ActiveDocument.FullName, config);
							}
							else
							{
								var projs = m.Groups[4].ToString().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
								projects = string.Join(" ", projs);
								projectCount = projs.Length;
								string arg = string.Empty;
								if (task == BuildTask.Clean)
									arg = "--clean";
								else if (task == BuildTask.Rebuild)
									arg = "--rebuild";
								arguments = string.Format("{0} {1} {2}", arg, config, m.Groups[4].ToString());
							}

							var p = Package.CreateTundraProcess(tundraPath, dir, arguments);
							if (p == null)
								throw new Exception("Couldn't create process");

							p.EnableRaisingEvents = true;
							p.OutputDataReceived += (s, e) => buildPane.OutputString(e.Data + "\n");
							p.ErrorDataReceived += (s, e) => buildPane.OutputString(e.Data + "\n");
							p.Exited += (s, e) =>
							{
								if (!p.HasExited)
									throw new Exception("Tundra process should've exited already!");
								if (p.ExitCode == 0)
									buildPane.OutputString(string.Format("========== {0}: {1} succeeded ==========\n", task, projectCount));
								else
									buildPane.OutputString(string.Format("========== {0}: Failed: {1} ==========\n", task, p.ExitCode));
								p.Dispose();
							};

							buildPane.Clear();
							buildPane.OutputString(string.Format("------ {0} started: Project{1}: {2}, Configuration: {3} ------\n", task, projectCount != 1 ? "(s)" : "", projects, config));
							buildPane.Activate();
							Package.DTE.ToolWindows.OutputWindow.Parent.Activate();

							p.Start();
							p.BeginOutputReadLine();
							p.BeginErrorReadLine();
						}
						catch (Exception ex)
						{
							messagePane.OutputString(string.Format("Failed to launch Tundra: {0}\n", ex.Message));
						}
					}
					else
						regularCommand = true;
				}
				else
					regularCommand = true;

				if (regularCommand)
				{
					string command = string.Empty;
					switch (task)
					{
						case BuildTask.Compile:
							command = "Build.Compile";
							break;
						case BuildTask.Build:
							command = "Build.BuildSolution";
							break;
						case BuildTask.Rebuild:
							command = "Build.RebuildSolution";
							break;
						case BuildTask.Clean:
							command = "Build.CleanSolution";
							break;
					}
					if (!string.IsNullOrEmpty(command))
						Package.DTE.ExecuteCommand(command);
				}
			}
			catch (Exception ex)
			{
				messagePane.OutputString(string.Format("Failed with exception: {0}\n", ex.Message));
				messagePane.OutputString(string.Format("{0}\n", ex.StackTrace));
			}
		}
	}
}

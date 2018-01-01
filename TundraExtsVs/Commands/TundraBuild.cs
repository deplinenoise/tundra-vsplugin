using EnvDTE;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.ComponentModel.Design;
using System.Text.RegularExpressions;

namespace TundraExts.Commands
{
	internal sealed class TundraBuild
	{
		private readonly TundraPackage Package;

		private TundraBuild(TundraPackage package)
		{
			Package = package;

			var mi = new MenuCommand((s, e) => Execute(BuildTask.Compile), new CommandID(PackageGuids.GuidTundraPackageCmdSet, PackageIds.TundraCompileId));
			Package.CommandService.AddCommand(mi);

			var build = new MenuCommand((s, e) => Execute(BuildTask.Build), new CommandID(PackageGuids.GuidTundraPackageCmdSet, PackageIds.TundraBuildSolutionId));
			Package.CommandService.AddCommand(build);

			var rebuild = new MenuCommand((s, e) => Execute(BuildTask.Rebuild), new CommandID(PackageGuids.GuidTundraPackageCmdSet, PackageIds.TundraRebuildSolutionId));
			Package.CommandService.AddCommand(rebuild);

			var clean = new MenuCommand((s, e) => Execute(BuildTask.Clean), new CommandID(PackageGuids.GuidTundraPackageCmdSet, PackageIds.TundraCleanSolutionId));
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

							buildPane.Clear();
							buildPane.OutputString(string.Format("------ {0} started: Project{1}: {2}, Configuration: {3} ------\n", task, projectCount != 1 ? "(s)" : "", projects, config));

							int result = Package.LaunchTundra(tundraPath, dir, arguments);

							if (result == 0)
								buildPane.OutputString(string.Format("========== {0}: {1} succeeded ==========\n", task, projectCount));
							else
								buildPane.OutputString(string.Format("========== {0}: Failed: {1} ==========\n", task, result));
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

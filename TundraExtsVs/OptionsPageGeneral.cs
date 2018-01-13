using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TundraExts
{
	[Guid(PackageGuids.GuidOptionsPageGeneralString)]
	public class OptionsPageGeneral : DialogPage
	{
		#region Constructors

		public OptionsPageGeneral()
		{
			Tundra = "There are currently no settings.";
		}

		#endregion

		#region Properties

		[ReadOnly(true)]
		//[Category("Misc")]
		//[Description("Tundra")]
		public string Tundra { get; }

		#endregion Properties

		#region Event Handlers

		protected override void OnActivate(CancelEventArgs e)
		{
			//int result = VsShellUtilities.ShowMessageBox(Site, Resources.MessageOnActivateEntered, null /*title*/, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
			//if (result == (int)VSConstants.MessageBoxResult.IDCANCEL)
			//	e.Cancel = true;
			base.OnActivate(e);
		}

		protected override void OnClosed(EventArgs e)
		{
		}

		protected override void OnDeactivate(CancelEventArgs e)
		{
		}

		protected override void OnApply(PageApplyEventArgs e)
		{
			base.OnApply(e);
		}

		#endregion Event Handlers
	}
}

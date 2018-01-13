using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace TundraExts
{
	class SolutionEventsSink : IVsSolutionEvents
	{
		public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
		{
			return VSConstants.S_OK;
		}

		public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
		{
			if (AfterOpenSolution != null)
				return AfterOpenSolution(fNewSolution != 0);
			return VSConstants.S_OK;
		}

		public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
		{
			bool cancel = pfCancel != 0;
			int ret = VSConstants.S_OK;
			if (QueryCloseSolution != null)
				ret = QueryCloseSolution(ref cancel);
			pfCancel = cancel ? 1 : 0;
			return ret;
		}

		public int OnBeforeCloseSolution(object pUnkReserved)
		{
			if (BeforeCloseSolution != null)
				return BeforeCloseSolution();
			return VSConstants.S_OK;
		}

		public int OnAfterCloseSolution(object pUnkReserved)
		{
			if (AfterCloseSolution != null)
				return AfterCloseSolution();
			return VSConstants.S_OK;
		}

		public uint Cookie { private set; get; }

		public delegate int AfterOpenSolutionHandler(bool isNew);
		public delegate int QueryCloseSolutionHandler(ref bool cancel);
		public delegate int CloseSolutionHandler();

		public event AfterOpenSolutionHandler AfterOpenSolution;
		public event QueryCloseSolutionHandler QueryCloseSolution;
		public event CloseSolutionHandler BeforeCloseSolution;
		public event CloseSolutionHandler AfterCloseSolution;

		public bool Hook(IVsSolution service)
		{
			uint cookie;
			int res = service.AdviseSolutionEvents(this, out cookie);
			Cookie = cookie;
			return res == VSConstants.S_OK;
		}

		public bool Unhook(IVsSolution service)
		{
			return service.UnadviseSolutionEvents(Cookie) == VSConstants.S_OK;
		}
	}
}

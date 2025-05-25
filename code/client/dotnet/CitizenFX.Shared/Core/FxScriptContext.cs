using System.Runtime.InteropServices;
using System.Security;

namespace CitizenFX.Shared.Core;

[StructLayout(LayoutKind.Sequential)]
[Serializable]
[SecurityCritical]
public unsafe struct FxScriptContext
{
	public IntPtr functionDataPtr;
	public IntPtr retDataPtr;

	public int numArguments;
	public int numResults;

	public fixed byte initialArguments[32 * 8];
}

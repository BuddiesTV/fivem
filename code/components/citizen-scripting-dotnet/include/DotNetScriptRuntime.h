#pragma once

#include "StdInc.h"

#include <ScriptEngine.h>
#include <fxScripting.h>
#include <om/OMComponent.h>
#include <Profiler.h>
#include "corehost/coreclr_delegates.h"

#include <CoreConsole.h>

typedef void (*triggerRuntimeEventDelegate)(char* eventName, char* eventPayload, uint32_t payloadSize, char* eventSource);
typedef void (*tickRuntimeDelegate)(uint64_t currentSchedulerTime);
typedef fx::TNativeHandler* NativeHandler;

namespace fx::dotnet
{
class DotNetScriptRuntime : public OMClass<DotNetScriptRuntime, IScriptRuntime, IScriptFileHandlingRuntime, IScriptTickRuntime, IScriptEventRuntime,
							IScriptRefRuntime, IScriptStackWalkingRuntime, IScriptWarningRuntime>
{
private:
	std::string m_resourceName;

	IScriptHost* m_scriptHost = nullptr;
	IScriptHostWithResourceData* m_resourceHost = nullptr;
	IScriptHostWithManifest* m_manifestHost = nullptr;

	triggerRuntimeEventDelegate g_triggerRuntimeEventDelegate = nullptr;
	tickRuntimeDelegate g_tickRuntimeDelegate = nullptr;

	int m_instanceId;

	fx::OMPtr<IScriptRuntimeHandler> m_handler;
	fx::Resource* m_parentObject = nullptr;

public:
	DotNetScriptRuntime()
	{
		m_instanceId = rand();
	}

	OMPtr<IScriptHost> GetScriptHost() const
	{
		return m_scriptHost;
	}

	const char* GetResourceName() const
	{
		return m_resourceName.c_str();
	}

	bool ReadAssembly(char* assemblyName, void** assemblyData, int* dataSize);
	static bool CORECLR_DELEGATE_CALLTYPE ReadAssemblyWrapper(void* runtimePointer, char* assemblyName, void** assemblyData, int* dataSize);
	static void CORECLR_DELEGATE_CALLTYPE Print(char* channel, char* message);
	static void CORECLR_DELEGATE_CALLTYPE SetEventHandler(void* runtimePointer, triggerRuntimeEventDelegate triggerRuntimeEventFnPtr);
	static void CORECLR_DELEGATE_CALLTYPE SetTickHandler(void* runtimePointer, tickRuntimeDelegate tickRuntimeDelegateFnPtr);
	static void* CORECLR_DELEGATE_CALLTYPE GetNative(uint64_t nativeHash);
	static void CORECLR_DELEGATE_CALLTYPE InvokeNative(NativeHandler nativeHandler, ScriptContext* context, uint64_t nativeHash);
	static void CORECLR_DELEGATE_CALLTYPE InvokeFunctionReference(void* runtimePointer, std::string refId, char* argsSerialized, uint32_t argsSize);

public:
	NS_DECL_ISCRIPTRUNTIME;
	NS_DECL_ISCRIPTFILEHANDLINGRUNTIME;
	NS_DECL_ISCRIPTTICKRUNTIME;
	NS_DECL_ISCRIPTEVENTRUNTIME;
	NS_DECL_ISCRIPTREFRUNTIME;
	NS_DECL_ISCRIPTSTACKWALKINGRUNTIME;
	NS_DECL_ISCRIPTWARNINGRUNTIME;
};

inline void DotNetScriptRuntime::Print(char* channel, char* message)
{
	console::Printf(channel, "%s", message);
}
}

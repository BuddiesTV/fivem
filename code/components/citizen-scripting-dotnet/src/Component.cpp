#include "StdInc.h"
#include "ComponentLoader.h"
#include <om/OMComponent.h>

class ComponentInstance : public OMComponentBase<Component>
{
public:
	bool Initialize() override;
	bool DoGameLoad(void* module) override;
	bool Shutdown() override;
};

bool ComponentInstance::Initialize()
{
	InitFunctionBase::RunAll();
	return true;
}

bool ComponentInstance::DoGameLoad(void* module)
{
	HookFunction::RunAll();
	return true;
}

bool ComponentInstance::Shutdown()
{
	return true;
}

extern "C" DLL_EXPORT Component* CreateComponent()
{
	return new ComponentInstance();
}

OMComponentBaseImpl* OMComponentBaseImpl::ms_instance;

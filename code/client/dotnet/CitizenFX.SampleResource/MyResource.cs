using CitizenFX.Shared;
using CitizenFX.Shared.Core;
using CitizenFX.Wrapper.Core;

namespace CitizenFX.SampleResource;

public class MyResource : CitizenResource
{
	public override void OnStart()
	{
		Citizen.RegisterEvent("onResourceStart", (string resourceName) =>
		{
			Citizen.Log($"Resource {resourceName} started.");
		});
		
		Citizen.RegisterEvent("playerConnecting", async (string playerName, dynamic setKickReason, dynamic deferrals) =>
		{
			try
			{
				deferrals.defer();
		
				await Task.Delay(0);
			
				deferrals.update($"Hello {playerName}, welcome to the server!");
				
				await Task.Delay(5000);

				deferrals.done();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
		});
		
		// Citizen.RegisterEvent("entityCreated", (uint handle) =>
		// {
		// 	Citizen.Log($"Entity with handle {handle} created.");
		// });
	}

	public override void OnStop()
	{

	}
}

---
ns: CFX
apiset: client
game: gta5
---

## FIX_VEHICLE_WHEEL

```c
void FIX_VEHICLE_WHEEL(Vehicle vehicle, int wheelIndex);
```

Puts a wheel broken off with [BREAK_OFF_VEHICLE_WHEEL](#_0xA274CADB) back on and resets its damage, leaving the rest of the vehicle as it is. Use this instead of [SET_VEHICLE_FIXED](#_0x115722B1B9C14C1C) when body damage, deformation, engine health etc. should survive.

Calling this on a wheel that is not broken off only resets that wheels damage, which is the tyre health, the suspension health and the burst flag.

## Examples

```lua
local vehicle = GetVehiclePedIsIn(PlayerPedId())

if DoesEntityExist(vehicle) then
    for i = 0, GetVehicleNumberOfWheels(vehicle) - 1 do
        if IsVehicleWheelBrokenOff(vehicle, i) then
            FixVehicleWheel(vehicle, i)
        end
    end
end
```

## Parameters

- **vehicle**: The vehicle handle.
- **wheelIndex**: The wheel index.

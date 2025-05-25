using System.Buffers;
using System.Reflection;
using System.Reflection.Emit;
using MessagePack;
using MessagePack.Formatters;

namespace CitizenFX.Wrapper.Core.MessagePack;

public class CitizenMessagePackFormatter : IMessagePackFormatter<object?>
{
	private const byte ExtensionTypeCode = 0x0b;

	// ReSharper disable once MemberCanBePrivate.Global 
	// Needs to be public to get accessed outside of this class
	public delegate void ParamsDelegate(params object[] args);

	public void Serialize(ref MessagePackWriter writer, object? value, MessagePackSerializerOptions options)
	{
		PrimitiveObjectFormatter.Instance.Serialize(ref writer, value, options);
	}

	public object? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		if (reader.NextMessagePackType == MessagePackType.Extension)
		{
			var extension = reader.ReadExtensionFormat();

			if (extension.TypeCode == ExtensionTypeCode)
			{
				var functionReference = System.Text.Encoding.UTF8.GetString(extension.Data.ToArray());

				var invokeFunctionReferenceMethod = typeof(Citizen).GetMethod(
					"InvokeFunctionReference",
					[typeof(string), typeof(object[])]);

				var dynamicMethod = new DynamicMethod(
					"InvokeFunctionReference_" + Guid.NewGuid().ToString("N"),
					typeof(void),
					[typeof(object[])],
					typeof(Citizen).Module,
					true);

				var il = dynamicMethod.GetILGenerator();
				il.Emit(OpCodes.Ldstr, functionReference);
				il.Emit(OpCodes.Ldarg_0);
				if (invokeFunctionReferenceMethod != null) il.Emit(OpCodes.Call, invokeFunctionReferenceMethod);
				il.Emit(OpCodes.Ret);

				var funcDelegate = (Action<object[]>)dynamicMethod.CreateDelegate(typeof(Action<object[]>));

				return new ParamsDelegate((params object[] args) => { funcDelegate.Invoke(args); });
			}
		}
		else if (reader.NextMessagePackType == MessagePackType.Map)
		{
			return ExpandoObjectFormatter.Instance.Deserialize(ref reader, options);
		}

		return PrimitiveObjectFormatter.Instance.Deserialize(ref reader, options);
	}
}

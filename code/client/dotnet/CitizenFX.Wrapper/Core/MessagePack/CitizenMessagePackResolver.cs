using MessagePack;
using MessagePack.Formatters;

namespace CitizenFX.Wrapper.Core.MessagePack;

public class CitizenMessagePackResolver : IFormatterResolver
{
	public static readonly CitizenMessagePackResolver Instance = new();
	private static readonly CitizenMessagePackFormatter Formatter = new();

	private CitizenMessagePackResolver()
	{
	}

	public IMessagePackFormatter<T>? GetFormatter<T>()
	{
		if (typeof(T) == typeof(object)) return (object)Formatter as IMessagePackFormatter<T>;

		return null;
	}
}

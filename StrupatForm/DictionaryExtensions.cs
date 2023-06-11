using System.Runtime.InteropServices;

namespace StrupatForm;

static class DictionaryExtensions
{
	public static void Add<TKey, TValue, TValues>(this Dictionary<TKey, TValues> dictionary, TKey key, TValue value)
		where TKey : notnull
		where TValues : ICollection<TValue>, new()
	{
		ref var valueRef = ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary, key, out var exists);
		if (!exists)
			valueRef = new TValues();
		valueRef!.Add(value);
	}

	public static void Remove<TKey, TValue, TValues>(this Dictionary<TKey, TValues> dictionary, TKey key, TValue value)
		where TKey : notnull
		where TValues : ICollection<TValue>, new()
	{
		if (!dictionary.TryGetValue(key, out var values))
			return;
		values.Remove(value);
		if (values.Count == 0)
			dictionary.Remove(key);
	}
}

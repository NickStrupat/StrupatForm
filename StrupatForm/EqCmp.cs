namespace StrupatForm;

sealed class EqCmp<T> : IEqualityComparer<T>
{
	private readonly Func<T, T, Boolean> equals;
	private readonly Func<T, Int32> getHashCode;
	public EqCmp(Func<T, T, Boolean> equals, Func<T, Int32> getHashCode)
	{
		this.equals = equals;
		this.getHashCode = getHashCode;
	}
	public Boolean Equals(T? x, T? y)
	{
		if (typeof(T).IsValueType)
			return equals(x!, y!);
		if (ReferenceEquals(x, y))
			return true;
		if (x is null || y is null)
			return false;
		return equals(x, y);
	}

	public Int32 GetHashCode(T obj) => getHashCode(obj);

	public static EqCmp<T> Create<TP>(Func<T, TP> prop)
		where TP : IEquatable<TP> =>
		new(
			(x, y) => prop(x).Equals(prop(y)),
			x => prop(x).GetHashCode()
		);

	public static EqCmp<T> Create<TP1, TP2>(Func<T, TP1> prop1, Func<T, TP2> prop2)
		where TP1 : IEquatable<TP1>
		where TP2 : IEquatable<TP2> =>
		new(
			(x, y) => prop1(x).Equals(prop1(y)) && prop2(x).Equals(prop2(y)),
			x => HashCode.Combine(prop1(x), prop2(x))
		);
}

internal static class EqualsCache<T> where T : notnull
{
	private static readonly Boolean IsEquatable =
		typeof(T).IsAssignableTo(typeof(IEquatable<T>));

	public static readonly Func<T, T, Boolean> EqualsFunc = IsEquatable
		? (Func<T, T, Boolean>) typeof(EquatableCache<>)
			.MakeGenericType(typeof(T))
			.GetField(nameof(EquatableCache<Boolean>.EqualsFunc))
			!.GetValue(null)!
		: (x, y) => x.Equals(y);

	static class EquatableCache<TEq> where TEq : IEquatable<TEq>
	{
		public static readonly Func<TEq, TEq, Boolean> EqualsFunc = (x, y) => x.Equals(y);
	}
}

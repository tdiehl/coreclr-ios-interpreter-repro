namespace RangedTypes;

public abstract class Reference : IComparable<Reference>
{
	public int CompareTo(Reference? other) => 0;
}

public interface IHasStart<TPoint>
	where TPoint : Reference, IComparable<TPoint>
{
	TPoint Start { get; }
}

// The crashing shape:
//  - TPoint is constrained by another type parameter (TPoint : TRef) *and* by IComparable<TPoint>,
//    and
//  - TRef is constrained by an interface over TPoint.
public abstract class Generic<TRef, TPoint>
	where TRef : Reference, IHasStart<TPoint>
	where TPoint : TRef, IComparable<TPoint>
{
	// A single member access through the TRef constraint is enough. This is the method that crashes when compiled
	// by the runtime as Generic<__Canon, __Canon>.Run(__Canon).
	public string Run(TRef reference) => reference.Start.ToString() ?? "";
}

public abstract class Item : Reference, IHasStart<Position>
{
	public abstract Position Start { get; }
}

public class Position : Item, IComparable<Position>
{
	public int CompareTo(Position? other) => 0;

	public override Position Start => this;
}

public sealed class Impl : Generic<Item, Position>
{
	public static Impl Instance { get; } = new();
}

public static class Entry
{
	public static string Run() => Impl.Instance.Run(new Position());
}

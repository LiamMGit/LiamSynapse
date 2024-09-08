namespace Synapse.Server.Extras;

/// <summary>
///     Container for extension functions for the System.Collections.Generic.IList{T} and System.Collections.IList
///     interfaces that inserts elements lists that are presumed to be already sorted such that sort ordering is preserved
/// </summary>
/// <author>Jackson Dunstan, http://JacksonDunstan.com/articles/3189</author>
/// <license>MIT</license>
public static class SortedListExtensions
{
	/// <summary>
	///     Insert a value into an IList{T} that is presumed to be already sorted such that sort
	///     ordering is preserved
	/// </summary>
	/// <param name="list">List to insert into</param>
	/// <param name="value">Value to insert</param>
	/// <typeparam name="T">Type of element to insert and type of elements in the list</typeparam>
	public static void InsertIntoSortedList<T>(this IList<T> list, T value)
        where T : IComparable<T>
    {
        InsertIntoSortedList(list, value, Compare);
    }

	/// <summary>
	///     Insert a value into an IList{T} that is presumed to be already sorted such that sort
	///     ordering is preserved
	/// </summary>
	/// <param name="list">List to insert into</param>
	/// <param name="value">Value to insert</param>
	/// <param name="comparison">Comparison to determine sort order with</param>
	/// <typeparam name="T">Type of element to insert and type of elements in the list</typeparam>
	public static void InsertIntoSortedList<T>(
        this IList<T> list,
        T value,
        Comparison<T> comparison
    )
    {
        int startIndex = 0;
        int endIndex = list.Count;
        while (endIndex > startIndex)
        {
            int windowSize = endIndex - startIndex;
            int middleIndex = startIndex + (windowSize / 2);
            T middleValue = list[middleIndex];
            int compareToResult = comparison(middleValue, value);
            switch (compareToResult)
            {
                case 0:
                    list.Insert(middleIndex, value);
                    return;
                case < 0:
                    startIndex = middleIndex + 1;
                    break;
                default:
                    endIndex = middleIndex;
                    break;
            }
        }

        list.Insert(startIndex, value);
    }

	private static int Compare<T>(T lhs, T rhs)
		where T : IComparable<T>
	{
		return lhs.CompareTo(rhs);
	}
}

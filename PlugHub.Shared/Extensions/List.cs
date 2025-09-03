namespace PlugHub.Shared.Extensions
{
    public static class ListExtensions
    {
        /// <summary>
        /// Performs a topological sort on the given source collection using the specified dependency selector.
        /// </summary>
        /// <typeparam name="T">The type of elements to sort. Must be non-nullable.</typeparam>
        /// <param name="source">The collection of nodes to sort.</param>
        /// <param name="getDependencies">A function that returns the dependencies for a given node.</param>
        /// <returns>A list of nodes sorted in topological order.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a cyclic dependency is detected.</exception>
        public static IList<T> TopologicalSort<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> getDependencies) where T : notnull
        {
            List<T> sorted = [];
            List<T> nodes = [.. source];

            Dictionary<T, HashSet<T>> dependencies = nodes.ToDictionary(
                node => node,
                node => new HashSet<T>(getDependencies(node) ?? []));

            while (dependencies.Count > 0)
            {
                List<T> independents = [.. dependencies.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key)];

                if (independents.Count == 0)
                {
                    throw new InvalidOperationException("Cyclic dependency detected.");
                }

                foreach (T? node in independents)
                {
                    sorted.Add(node);
                    dependencies.Remove(node);
                }

                foreach (KeyValuePair<T, HashSet<T>> kv in dependencies)
                {
                    kv.Value.ExceptWith(independents);
                }
            }

            return sorted;
        }
    }
}

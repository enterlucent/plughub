namespace PlugHub.Shared.Attributes
{
    public enum DescriptorSortContext
    {
        None = 0,
        Forward = 1,
        Reverse = 2,
    }

    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public sealed class DescriptorProviderAttribute(string descriptorAccessorName, bool isSystemOnly = false, DescriptorSortContext sortContext = DescriptorSortContext.Reverse) : Attribute
    {
        public string DescriptorAccessorName { get; } = descriptorAccessorName;
        public bool DescriptorIsSystemOnly { get; } = isSystemOnly;
        public DescriptorSortContext SortContext { get; } = sortContext;
    }
}
namespace PlugHub.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public sealed class DescriptorProviderAttribute(string descriptorAccessorName, bool isOrdered = true, bool isSystemOnly = false) : Attribute
    {
        public string DescriptorAccessorName { get; } = descriptorAccessorName;
        public bool DescriptorIsOrdered { get; } = isOrdered;
        public bool DescriptorIsSystemOnly { get; } = isSystemOnly;
    }
}
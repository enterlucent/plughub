namespace PlugHub.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public sealed class DescriptorProviderAttribute(string descriptorAccessorName, bool descriptorIsOrdered = true) : Attribute
    {
        public string DescriptorAccessorName { get; } = descriptorAccessorName;
        public bool DescriptorIsOrdered { get; } = descriptorIsOrdered;
    }
}
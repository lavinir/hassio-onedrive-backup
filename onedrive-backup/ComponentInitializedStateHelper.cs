namespace onedrive_backup
{
    public class ComponentInitializedStateHelper
    {
        private HashSet<string> _components = new HashSet<string>();

        public bool IsInitialized(string component)
        {
            return _components.Contains(component);
        }

        public void SetAsInitialized(string component)
        {
            _components.Add(component);
        }
    }
}

namespace MLAPI.Engine
{
    public abstract class ObjectManager
    {
        public abstract T[] FindObjectsOfType<T>() where T : ObjectComponent;
        public abstract void DontDestroyOnLoad(PhysicalObject physicalObject);
        public abstract PhysicalObject Instantiate(PhysicalObject prefab);
        public abstract PhysicalObject Instantiate(PhysicalObject prefab, Vector3 position, Quaternion rotation);
        public abstract void Destroy(PhysicalObject physicalObject);
    }
}

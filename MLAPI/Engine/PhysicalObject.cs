using System;

namespace MLAPI.Engine
{
    public abstract class PhysicalObject
    {
        public abstract string Name { get; set; }

        public abstract Vector3 Position { get; set; }
        public abstract Quaternion Rotation { get; set; }

        public abstract PhysicalObject Parent { get; set; }

        public abstract T GetComponent<T>() where T : ObjectComponent;
        public abstract T GetComponentInParent<T>() where T : ObjectComponent;

        public abstract T[] GetComponentsInChildren<T>(bool includeInactive) where T : ObjectComponent;
        public abstract T GetComponentInChildren<T>() where T : ObjectComponent;
    }
}

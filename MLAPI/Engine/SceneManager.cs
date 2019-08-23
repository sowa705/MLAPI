using System;

namespace MLAPI.Engine
{
    public abstract class SceneManager
    {
        public abstract Scene GetActiveScene();
        public abstract AsyncProgress LoadSceneAsync(string sceneName);
        public abstract void LoadScene(string sceneName);
        public abstract Scene GetSceneByName(string name);
        public abstract void SetActiveScene(Scene scene);
        public abstract void MovePhysicalObjectToScene(PhysicalObject physicalObject, Scene scene);

        public class AsyncProgress
        {
            public bool IsCompleted;
            public event Action OnCompleted;
        }
    }
}

using System.Collections.Generic;
using System;
using System.IO;
using MLAPI.Configuration;
using MLAPI.Exceptions;
using MLAPI.Logging;
using MLAPI.Messaging;
using MLAPI.Security;
using MLAPI.Serialization.Pooled;
using MLAPI.Spawning;
using MLAPI.Engine;
using static MLAPI.Engine.SceneManager;

namespace MLAPI.SceneManagement
{
    /// <summary>
    /// Main class for managing network scenes
    /// </summary>
    public static class NetworkSceneManager
    {
        /// <summary>
        /// Delegate for when the scene has been switched
        /// </summary>
        public delegate void SceneSwitchedDelegate();
        /// <summary>
        /// Event that is invoked when the scene is switched
        /// </summary>
        public static event SceneSwitchedDelegate OnSceneSwitched;

        internal static readonly HashSet<string> registeredSceneNames = new HashSet<string>();
        internal static readonly Dictionary<string, uint> sceneNameToIndex = new Dictionary<string, uint>();
        internal static readonly Dictionary<uint, string> sceneIndexToString = new Dictionary<uint, string>();
        internal static readonly Dictionary<Guid, SceneSwitchProgress> sceneSwitchProgresses = new Dictionary<Guid, SceneSwitchProgress>();
        private static Scene lastScene;
        private static string nextSceneName;
        private static bool isSwitching = false;
        internal static uint currentSceneIndex = 0;
        internal static Guid currentSceneSwitchProgressGuid = new Guid();
        internal static bool isSpawnedObjectsPendingInDontDestroyOnLoad = false;

        internal static void SetCurrentSceneIndex()
        {
            if (!sceneNameToIndex.ContainsKey(GameEngine.SceneManager.GetActiveScene().Name))
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("The current scene (" + GameEngine.SceneManager.GetActiveScene().Name + ") is not regisered as a network scene.");
                return;
            }
            currentSceneIndex = sceneNameToIndex[GameEngine.SceneManager.GetActiveScene().Name];
            CurrentActiveSceneIndex = currentSceneIndex;
        }

        internal static uint CurrentActiveSceneIndex { get; private set; } = 0;

        /// <summary>
        /// Adds a scene during runtime.
        /// The index is REQUIRED to be unique AND the same across all instances.
        /// </summary>
        /// <param name="sceneName">Scene name.</param>
        /// <param name="index">Index.</param>
        public static void AddRuntimeSceneName(string sceneName, uint index)
        {
            if (!NetworkingManager.Singleton.NetworkConfig.AllowRuntimeSceneChanges)
            {
                throw new NetworkConfigurationException("Cannot change the scene configuration when AllowRuntimeSceneChanges is false");
            }

            registeredSceneNames.Add(sceneName);
            sceneIndexToString.Add(index, sceneName);
            sceneNameToIndex.Add(sceneName, index);
        }

        /// <summary>
        /// Switches to a scene with a given name. Can only be called from Server
        /// </summary>
        /// <param name="sceneName">The name of the scene to switch to</param>
        public static SceneSwitchProgress SwitchScene(string sceneName)
        {
            if (!NetworkingManager.Singleton.IsServer)
            {
                throw new NotServerException("Only server can start a scene switch");
            }
            else if (isSwitching)
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Scene switch already in progress");
                return null;
            }
            else if (!registeredSceneNames.Contains(sceneName))
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("The scene " + sceneName + " is not registered as a switchable scene.");
                return null;
            }

            SpawnManager.ServerDestroySpawnedSceneObjects(); //Destroy current scene objects before switching.
            isSwitching = true;
            lastScene = GameEngine.SceneManager.GetActiveScene();

            SceneSwitchProgress switchSceneProgress = new SceneSwitchProgress();
            sceneSwitchProgresses.Add(switchSceneProgress.guid, switchSceneProgress);
            currentSceneSwitchProgressGuid = switchSceneProgress.guid;

            // Move ALL networked objects to the temp scene
            MoveObjectsToDontDestroyOnLoad();

            isSpawnedObjectsPendingInDontDestroyOnLoad = true;

            // Switch scene
            AsyncProgress sceneLoad = GameEngine.SceneManager.LoadSceneAsync(sceneName);
            nextSceneName = sceneName;

            sceneLoad.OnCompleted += () => { OnSceneLoaded(switchSceneProgress.guid, null); };

            switchSceneProgress.SetSceneLoadOperation(sceneLoad);

            return switchSceneProgress;
        }

        // Called on client
        internal static void OnSceneSwitch(uint sceneIndex, Guid switchSceneGuid, Stream objectStream)
        {
            if (!sceneIndexToString.ContainsKey(sceneIndex) || !registeredSceneNames.Contains(sceneIndexToString[sceneIndex]))
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Server requested a scene switch to a non registered scene");
                return;
            }
            else if (GameEngine.SceneManager.GetActiveScene().Name == sceneIndexToString[sceneIndex])
            {
                return; //This scene is already loaded. This usually happends at first load
            }

            lastScene = GameEngine.SceneManager.GetActiveScene();

            // Move ALL networked objects to the temp scene
            MoveObjectsToDontDestroyOnLoad();

            isSpawnedObjectsPendingInDontDestroyOnLoad = true;

            string sceneName = sceneIndexToString[sceneIndex];

            AsyncProgress sceneLoad = GameEngine.SceneManager.LoadSceneAsync(sceneName);
            nextSceneName = sceneName;

            sceneLoad.OnCompleted += () =>
            {
                OnSceneLoaded(switchSceneGuid, objectStream);
            };
        }

        internal static void OnFirstSceneSwitchSync(uint sceneIndex, Guid switchSceneGuid)
        {
            if (!sceneIndexToString.ContainsKey(sceneIndex) || !registeredSceneNames.Contains(sceneIndexToString[sceneIndex]))
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Server requested a scene switch to a non registered scene");
                return;
            }
            else if (GameEngine.SceneManager.GetActiveScene().Name == sceneIndexToString[sceneIndex])
            {
                return; //This scene is already loaded. This usually happends at first load
            }

            lastScene = GameEngine.SceneManager.GetActiveScene();
            string sceneName = sceneIndexToString[sceneIndex];
            nextSceneName = sceneName;
            CurrentActiveSceneIndex = sceneNameToIndex[sceneName];

            isSpawnedObjectsPendingInDontDestroyOnLoad = true;
            GameEngine.SceneManager.LoadScene(sceneName);

            using (PooledBitStream stream = PooledBitStream.Get())
            {
                using (PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteByteArray(switchSceneGuid.ToByteArray());
                    InternalMessageSender.Send(NetworkingManager.Singleton.ServerClientId, MLAPIConstants.MLAPI_CLIENT_SWITCH_SCENE_COMPLETED, "MLAPI_INTERNAL", stream, SecuritySendFlags.None, null);
                }
            }

            isSwitching = false;
        }

        private static void OnSceneLoaded(Guid switchSceneGuid, Stream objectStream)
        {
            CurrentActiveSceneIndex = sceneNameToIndex[nextSceneName];
            Scene nextScene = GameEngine.SceneManager.GetSceneByName(nextSceneName);
            GameEngine.SceneManager.SetActiveScene(nextScene);

            // Move all objects to the new scene
            MoveObjectsToScene(nextScene);

            isSpawnedObjectsPendingInDontDestroyOnLoad = false;

            currentSceneIndex = CurrentActiveSceneIndex;

            if (NetworkingManager.Singleton.IsServer)
            {
                OnSceneUnloadServer(switchSceneGuid);
            }
            else
            {
                OnSceneUnloadClient(switchSceneGuid, objectStream);
            }
        }

        private static void OnSceneUnloadServer(Guid switchSceneGuid)
        {
            // Justification: Rare alloc, could(should?) reuse
            List<NetworkedObject> newSceneObjects = new List<NetworkedObject>();

            {
                NetworkedObject[] networkedObjects = GameEngine.ObjectManager.FindObjectsOfType<NetworkedObject>();

                for (int i = 0; i < networkedObjects.Length; i++)
                {
                    if (networkedObjects[i].IsSceneObject == null)
                    {
                        SpawnManager.SpawnNetworkedObjectLocally(networkedObjects[i], SpawnManager.GetNetworkObjectId(), true, false, null, null, false, 0, false, true);

                        newSceneObjects.Add(networkedObjects[i]);
                    }
                }
            }


            for (int j = 0; j < NetworkingManager.Singleton.ConnectedClientsList.Count; j++)
            {
                if (NetworkingManager.Singleton.ConnectedClientsList[j].ClientId != NetworkingManager.Singleton.ServerClientId)
                {
                    using (PooledBitStream stream = PooledBitStream.Get())
                    {
                        using (PooledBitWriter writer = PooledBitWriter.Get(stream))
                        {
                            writer.WriteUInt32Packed(CurrentActiveSceneIndex);
                            writer.WriteByteArray(switchSceneGuid.ToByteArray());

                            uint sceneObjectsToSpawn = 0;
                            for (int i = 0; i < newSceneObjects.Count; i++)
                            {
                                if (newSceneObjects[i].observers.Contains(NetworkingManager.Singleton.ConnectedClientsList[j].ClientId))
                                    sceneObjectsToSpawn++;
                            }

                            writer.WriteUInt32Packed(sceneObjectsToSpawn);

                            for (int i = 0; i < newSceneObjects.Count; i++)
                            {
                                if (newSceneObjects[i].observers.Contains(NetworkingManager.Singleton.ConnectedClientsList[j].ClientId))
                                {
                                    writer.WriteBool(newSceneObjects[i].IsPlayerObject);
                                    writer.WriteUInt64Packed(newSceneObjects[i].NetworkId);
                                    writer.WriteUInt64Packed(newSceneObjects[i].OwnerClientId);

                                    NetworkedObject parent = null;

                                    if (!newSceneObjects[i].AlwaysReplicateAsRoot && newSceneObjects[i].PhysicalObject.Parent != null)
                                    {
                                        parent = newSceneObjects[i].PhysicalObject.Parent.GetComponent<NetworkedObject>();
                                    }

                                    if (parent == null)
                                    {
                                        writer.WriteBool(false);
                                    }
                                    else
                                    {
                                        writer.WriteBool(true);
                                        writer.WriteUInt64Packed(parent.NetworkId);
                                    }

                                    if (NetworkingManager.Singleton.NetworkConfig.UsePrefabSync)
                                    {
                                        writer.WriteUInt64Packed(newSceneObjects[i].PrefabHash);

                                        writer.WriteVector3Packed(newSceneObjects[i].PhysicalObject.Position);

                                        writer.WriteRotationPacked(newSceneObjects[i].PhysicalObject.Rotation);
                                    }
                                    else
                                    {
                                        writer.WriteUInt64Packed(newSceneObjects[i].NetworkedInstanceId);
                                    }

                                    if (NetworkingManager.Singleton.NetworkConfig.EnableNetworkedVar)
                                    {
                                        newSceneObjects[i].WriteNetworkedVarData(stream, NetworkingManager.Singleton.ConnectedClientsList[j].ClientId);
                                    }
                                }
                            }
                        }

                        InternalMessageSender.Send(NetworkingManager.Singleton.ConnectedClientsList[j].ClientId, MLAPIConstants.MLAPI_SWITCH_SCENE, "MLAPI_INTERNAL", stream, SecuritySendFlags.None, null);
                    }
                }
            }

            //Tell server that scene load is completed
            if (NetworkingManager.Singleton.IsHost)
            {
                OnClientSwitchSceneCompleted(NetworkingManager.Singleton.LocalClientId, switchSceneGuid);
            }

            isSwitching = false;

            if (OnSceneSwitched != null)
            {
                OnSceneSwitched();
            }
        }

        private static void OnSceneUnloadClient(Guid switchSceneGuid, Stream objectStream)
        {
            if (NetworkingManager.Singleton.NetworkConfig.UsePrefabSync)
            {
                SpawnManager.DestroySceneObjects();

                using (PooledBitReader reader = PooledBitReader.Get(objectStream))
                {
                    uint newObjectsCount = reader.ReadUInt32Packed();

                    for (int i = 0; i < newObjectsCount; i++)
                    {
                        bool isPlayerObject = reader.ReadBool();
                        ulong networkId = reader.ReadUInt64Packed();
                        ulong owner = reader.ReadUInt64Packed();
                        bool hasParent = reader.ReadBool();
                        ulong? parentNetworkId = null;

                        if (hasParent)
                        {
                            parentNetworkId = reader.ReadUInt64Packed();
                        }

                        ulong prefabHash = reader.ReadUInt64Packed();

                        Vector3? position = null;
                        Quaternion? rotation = null;
                        if (reader.ReadBool())
                        {
                            position = new Vector3(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                            rotation = Quaternion.Euler(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                        }

                        NetworkedObject networkedObject = SpawnManager.CreateLocalNetworkedObject(false, 0, prefabHash, parentNetworkId, position, rotation);
                        SpawnManager.SpawnNetworkedObjectLocally(networkedObject, networkId, true, isPlayerObject, owner, objectStream, false, 0, true, false);
                    }
                }
            }
            else
            {
                NetworkedObject[] networkedObjects = GameEngine.ObjectManager.FindObjectsOfType<NetworkedObject>();

                SpawnManager.ClientCollectSoftSyncSceneObjectSweep(networkedObjects);

                using (PooledBitReader reader = PooledBitReader.Get(objectStream))
                {
                    uint newObjectsCount = reader.ReadUInt32Packed();

                    for (int i = 0; i < newObjectsCount; i++)
                    {
                        bool isPlayerObject = reader.ReadBool();
                        ulong networkId = reader.ReadUInt64Packed();
                        ulong owner = reader.ReadUInt64Packed();
                        bool hasParent = reader.ReadBool();
                        ulong? parentNetworkId = null;

                        if (hasParent)
                        {
                            parentNetworkId = reader.ReadUInt64Packed();
                        }

                        ulong instanceId = reader.ReadUInt64Packed();

                        NetworkedObject networkedObject = SpawnManager.CreateLocalNetworkedObject(true, instanceId, 0, parentNetworkId, null, null);
                        SpawnManager.SpawnNetworkedObjectLocally(networkedObject, networkId, true, isPlayerObject, owner, objectStream, false, 0, true, false);
                    }
                }
            }

            using (PooledBitStream stream = PooledBitStream.Get())
            {
                using (PooledBitWriter writer = PooledBitWriter.Get(stream))
                {
                    writer.WriteByteArray(switchSceneGuid.ToByteArray());
                    NetworkedObject networkedObject = null;
                    InternalMessageSender.Send(NetworkingManager.Singleton.ServerClientId, MLAPIConstants.MLAPI_CLIENT_SWITCH_SCENE_COMPLETED, "MLAPI_INTERNAL", stream, SecuritySendFlags.None, networkedObject);
                }
            }

            isSwitching = false;

            if (OnSceneSwitched != null)
            {
                OnSceneSwitched();
            }
        }

        internal static bool HasSceneMismatch(uint sceneIndex)
        {
            return GameEngine.SceneManager.GetActiveScene().Name != sceneIndexToString[sceneIndex];
        }

        // Called on server
        internal static void OnClientSwitchSceneCompleted(ulong clientId, Guid switchSceneGuid)
        {
            if (switchSceneGuid == Guid.Empty)
            {
                //If Guid is empty it means the client has loaded the start scene of the server and the server would never have a switchSceneProgresses created for the start scene.
                return;
            }
            if (!sceneSwitchProgresses.ContainsKey(switchSceneGuid))
            {
                return;
            }

            sceneSwitchProgresses[switchSceneGuid].AddClientAsDone(clientId);
        }


        internal static void RemoveClientFromSceneSwitchProgresses(ulong clientId)
        {
            foreach (SceneSwitchProgress switchSceneProgress in sceneSwitchProgresses.Values)
            {
                switchSceneProgress.RemoveClientAsDone(clientId);
            }
        }

        private static void MoveObjectsToDontDestroyOnLoad()
        {
            // Move ALL networked objects to the temp scene
            List<NetworkedObject> objectsToKeep = SpawnManager.SpawnedObjectsList;

            for (int i = 0; i < objectsToKeep.Count; i++)
            {
                //In case an object has been set as a child of another object it has to be unchilded in order to be moved from one scene to another.
                if (objectsToKeep[i].PhysicalObject.Parent != null)
                {
                    objectsToKeep[i].PhysicalObject.Parent = null;
                }

                GameEngine.ObjectManager.DontDestroyOnLoad(objectsToKeep[i].PhysicalObject);
            }
        }

        private static void MoveObjectsToScene(Scene scene)
        {
            // Move ALL networked objects to the temp scene
            List<NetworkedObject> objectsToKeep = SpawnManager.SpawnedObjectsList;

            for (int i = 0; i < objectsToKeep.Count; i++)
            {
                //In case an object has been set as a child of another object it has to be unchilded in order to be moved from one scene to another.
                if (objectsToKeep[i].PhysicalObject.Parent != null)
                {
                    objectsToKeep[i].PhysicalObject.Parent = null;
                }

                GameEngine.SceneManager.MovePhysicalObjectToScene(objectsToKeep[i].PhysicalObject, scene);
            }
        }
    }
}

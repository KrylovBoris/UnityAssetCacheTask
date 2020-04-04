using System.Collections.Generic;

namespace AssetCacheImplementation
{
    /// <summary>
    /// ����� � ��������� ��� 
    /// </summary>
    internal class Cache
    {
        //���� ����, ��� ��������� ��������� ���������� ����������
        private bool _isComplete;

        //�������, ���� �������� ��� ��������� ������� � ������
        private readonly Dictionary<ulong, SceneEntity> _cachedObjects;
        private readonly Dictionary<string, Asset> _cachedAssets;

        //���� �� ��������������� �����
        public string PathToFile { get; }
        
        //����, ������������ ������ ���� ��������� fileID �������� �����
        public List<ulong> FileIds
        {
            get
            {
                return new List<ulong>(_cachedObjects.Keys);
            }
        }
        //����, ������������ ������ ���� ��������� GUID
        public List<string> GuIds
        {
            get
            {
                return new List<string>(_cachedAssets.Keys);
            }
        }

        public Cache(string path)
        {
            PathToFile = path;
            _cachedObjects = new Dictionary<ulong, SceneEntity>();
            _cachedAssets = new Dictionary<string, Asset>();
        }

        #region Cache entities

        // �����, �������������� ���������� ������ �����
        private class SceneEntity
        {
            public ulong FileId { get; private set; }
            public string EntityType { get; protected set; }

            //������ ���������������, �� ������� ��������� ������ ������
            protected List<ulong> refsToOtherEntities;

            public SceneEntity(ulong id)
            {
                FileId = id;
                EntityType = "Unspecified";
                refsToOtherEntities = new List<ulong>();
            }

            /// <summary>
            /// �����, ���������� ��� ������� �����
            /// </summary>
            /// <param name="newType">����� ���</param>
            public void SpecifyType(string newType)
            {
                EntityType = newType;
            }

            public void AddLinkToOtherEntity(ulong fileId)
            {
                refsToOtherEntities.Add(fileId);
            }

            /// <summary>
            /// ������� ������ �� �������� ������
            /// </summary>
            /// <param name="fileId">������������� �������, �� ������� ��������� ������</param>
            /// <returns>������� ��� ������ ������ ��������� �� ������ � ��������������� fileID</returns>
            public virtual int CountReferences(ulong fileId)
            {
                int count = 0;
                foreach(var reference in refsToOtherEntities)
                {
                    count += (reference == fileId) ? 1 : 0;
                }
                return count;
            }

            public IEnumerable<ulong> GetAllReferences()
            {
                return refsToOtherEntities;
            }
        }
        
        // �����, �������������� ������� ������
        private class GameObject : SceneEntity
        {
            //������ �����������
            private readonly List<ulong> _components;

            public GameObject(ulong id) : base(id)
            {
                EntityType = "GameObject";
                _components = new List<ulong>();
            }

            public void AddComponent(ulong componentFileId)
            {
                _components.Add(componentFileId);
            }

            public IEnumerable<ulong> GetComponents()
            {
                return _components;
            }

            public bool HasComponents
            {
                get
                {
                    return _components.Count > 0;
                }
            }

            public bool HasComponent(ulong fileID)
            {
                return _components.Contains(fileID);
            }
        }

        // �����, �������������� �����
        private class Asset
        {
            //������ ��������, ����������� �� ������ �����
            private readonly List<ulong> _referencedBySceneEntities;
            
            public string Guid { get; private set; }
            //FileID
            public ulong AssetTypeId { get; private set; }
            public int UsageCount => _referencedBySceneEntities.Count;

            public Asset(string guid, ulong parentFileId, ulong typeFileId)
            {
                Guid = guid;
                AssetTypeId = typeFileId;
                _referencedBySceneEntities = new List<ulong>(){parentFileId};
            }

            public void AddReference(ulong parentFileId)
            {
                _referencedBySceneEntities.Add(parentFileId);
            }
            
        }

        #endregion

        #region Cache Building Methods
        public void ConstructSceneEntity(ulong fileId)
        {
            if (!_cachedObjects.ContainsKey(fileId))
            {
                _cachedObjects.Add(fileId, new SceneEntity(fileId));
            }
        }

        public void ConstructAsset(string guid, ulong referencedBy, ulong fileId)
        {
            if (_cachedAssets.ContainsKey(guid))
            {
                _cachedAssets[guid].AddReference(referencedBy);
            }
            else
            {
                _cachedAssets.Add(guid, new Asset(guid, referencedBy, fileId));
            }
        }

        public void ConstructGameObject(ulong fileId)
        {
            if (_cachedObjects.ContainsKey(fileId))
            {
                _cachedObjects[fileId] = new GameObject(fileId);
            }
            else
            {
                _cachedObjects.Add(fileId, new GameObject(fileId));
            }
        }

        public void AddLinkToSceneEntity(ulong linkRecieverFileId, ulong linkTarget)
        {
            if (!_cachedObjects.ContainsKey(linkRecieverFileId))
            {
                _cachedObjects.Add(linkRecieverFileId, new SceneEntity(linkRecieverFileId));
            }
            _cachedObjects[linkRecieverFileId].AddLinkToOtherEntity(linkTarget);
        }

        public void SpecifySceneEntityType(ulong fileId, string type)
        {
            var typeCrop = type.Substring(0, type.IndexOf(":"));
            if (typeCrop == "GameObject")
            {
                ConstructGameObject(fileId);
            }
            else
            {
                _cachedObjects[fileId].SpecifyType(typeCrop);
            }
        }

        public void AddComponentToGameObject(ulong gameObjectFileId, ulong component)
        {
            if (!_cachedObjects.ContainsKey(gameObjectFileId))
            {
                _cachedObjects.Add(gameObjectFileId, new GameObject(gameObjectFileId));
            }
            if (!_cachedObjects.ContainsKey(component))
            {
                _cachedObjects.Add(component, new SceneEntity(component));
            }

            var gameObject = (GameObject) _cachedObjects[gameObjectFileId];
            gameObject.AddComponent(component);
            
        }

        public void FinishBuilding()
        {
            _isComplete = true;
        }

        #endregion

        #region Cache Validation Methods

        //��� ������ ���������� ��� �������� ����, ��� ��� ��������� ���������
        public bool IsValid()
        {
            return DoAllGameObjectsHaveComponents() && _isComplete && AreAllTypesSpecified();
        }
        private bool AreAllTypesSpecified()
        {
            var result = true;
            foreach (var entity in _cachedObjects.Values)
            {
                result &= entity.EntityType != "Unspecified";
            }
            return result;
        }
        private bool DoAllGameObjectsHaveComponents()
        {
            var result = true;
            foreach (var entity in _cachedObjects.Values)
            {
                if (entity is GameObject)
                    result &= ((GameObject)entity).HasComponents;
            }
            return result;
        }

        #endregion

        #region Public Interface
        //��������� ��� �������������� � ���������� ����
        public bool IsGameObject(ulong fileId)
        {
            return _cachedObjects[fileId] is GameObject;
        }

        public bool HasGameObjectComponent(ulong gameObject, ulong component)
        {
            return ((GameObject)_cachedObjects[gameObject]).HasComponent(component);
        }

        public int GetGuidUsage(string guid)
        {
            return (!_cachedAssets.ContainsKey(guid)) ? 0 : _cachedAssets[guid].UsageCount;
        }

        public ulong GetAssetFileId(string guid)
        {
            return _cachedAssets[guid].AssetTypeId;
        }

        public IEnumerable<ulong> GetComponents(ulong fileId)
        {
            return ((GameObject)_cachedObjects[fileId]).GetComponents();
        }

        public int FindUsages(ulong fileId)
        {
            int count = 0;
            foreach(var entity in _cachedObjects.Values)
            {
                count += entity.CountReferences(fileId);
            }
            return count;
        }

        public IEnumerable<ulong> GetFileIdsReferencesBy(ulong referencesSource)
        {
            return _cachedObjects[referencesSource].GetAllReferences();
        }

        #endregion
    }
}